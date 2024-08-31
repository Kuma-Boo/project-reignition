using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[Signal]
	public delegate void LaunchFinishedEventHandler();
	[Signal]
	public delegate void KnockbackEventHandler();
	[Signal]
	public delegate void DefeatedEventHandler();
	[Signal]
	public delegate void ExternalControlCompletedEventHandler();

	[Export]
	public PlayerStateMachine StateMachine { get; private set; }
	[Export]
	public PlayerInputController Controller { get; private set; }
	[Export]
	public PlayerStatsController Stats { get; private set; }
	[Export]
	public PlayerStateController State { get; private set; }
	[Export]
	public PlayerSkillController Skills { get; private set; }
	[Export]
	public PlayerLockonController Lockon { get; private set; }
	[Export]
	public PlayerAnimator Animator { get; private set; }
	[Export]
	public PlayerEffect Effect { get; private set; }
	[Export]
	public PlayerPathController PathFollower { get; private set; }
	[Export]
	public PlayerCameraController Camera { get; private set; }

	public override void _Ready()
	{
		StageSettings.RegisterPlayer(this); // Update global reference

		Stats.Initialize();
		State.Initialize(this);
		Lockon.Initialize(this);
		Animator.Initialize(this);
		Effect.Initialize(this);
		PathFollower.Initialize(this);
		Camera.Initialize(this);

		// Initialize state machine last to ensure components are ready		
		StateMachine.Initialize(this);
	}

	public override void _PhysicsProcess(double _)
	{
		Controller.ProcessInputs();
		StateMachine.ProcessPhysics();
		State.ProcessPhysics();
		Lockon.ProcessPhysics();
		Animator.ProcessPhysics();
		PathFollower.Resync();

		if (GetTree().Paused)
			return;
		Camera.ProcessPhysics();
	}

	public override void _Process(double _)
	{
		Camera.ProcessFrame();
	}

	/// <summary> Player's horizontal movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }
	/// <summary> Player's vertical speed -- only effective when not on the ground. </summary>
	public float VerticalSpeed { get; set; }
	public bool IsMovingBackward { get; set; }

	/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs. </summary>
	public float MovementAngle { get; set; }
	public float PathTurnInfluence => PathFollower.DeltaAngle * Camera.ActiveSettings.pathControlInfluence;
	public Vector3 GetMovementDirection()
	{
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		return PathFollower.Forward().Rotated(UpDirection, deltaAngle);
	}

	/// <summary> How much is the slope currently influencing the player? </summary>
	public float SlopeRatio { get; private set; }
	/// <summary> Slopes that are shallower than Mathf.PI * threshold are ignored. </summary>
	private readonly float SlopeThreshold = .1f;
	public void AddSlopeSpeed(bool isSliding = false)
	{
		SlopeRatio = 0;
		if (State.ActiveLockoutData?.ignoreSlopes == true) return; // Lockout is ignoring slopes
		if (Mathf.IsZeroApprox(MoveSpeed)) return; // Idle/Backstepping isn't affected by slopes

		// Calculate slope influence
		SlopeRatio = PathFollower.Forward().Dot(Vector3.Up);
		if (Mathf.Abs(SlopeRatio) <= SlopeThreshold) return;

		SlopeRatio = Mathf.Lerp(-Stats.slopeInfluence, Stats.slopeInfluence, (SlopeRatio * .5f) + .5f);
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AllRounder) && SlopeRatio > 0) // SKILL Ignore upward slopes
			SlopeRatio = 0;

		// Slope speeds are ignored when sliding downhill and already moving faster than the max slideSpeed + slopeInfluence
		if (isSliding && SlopeRatio < 0 && MoveSpeed >= Stats.SlideSettings.Speed)
			return;

		if (Controller.IsHoldingDirection(Controller.GetTargetMovementAngle(), PathFollower.ForwardAngle)) // Accelerating
		{
			if (SlopeRatio < 0f) // Downhill
				MoveSpeed += Stats.GroundSettings.Traction * Mathf.Abs(SlopeRatio) * PhysicsManager.physicsDelta; // Uncapped
			else if (Stats.GroundSettings.GetSpeedRatioClamped(MoveSpeed) < 1f) // Uphill; Reduce acceleration (Only when not at top speed)
				MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, Stats.GroundSettings.Traction * SlopeRatio * PhysicsManager.physicsDelta);
			return;
		}

		if (SlopeRatio < 0f) // Re-apply some speed when moving downhill
			MoveSpeed = Mathf.MoveToward(MoveSpeed, Stats.GroundSettings.Speed, Stats.GroundSettings.Friction * Mathf.Abs(SlopeRatio) * PhysicsManager.physicsDelta);
		else // Increase friction when moving uphill
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, Stats.GroundSettings.Friction * SlopeRatio * PhysicsManager.physicsDelta);
	}

	public void ApplyMovement()
	{
		Vector3 movementVelocity = Vector3.Zero;
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		Vector3 movementDirection = PathFollower.GlobalBasis.Z.Rotated(UpDirection, deltaAngle);
		movementVelocity += movementDirection * MoveSpeed;
		movementVelocity += UpDirection * VerticalSpeed;
		Velocity = movementVelocity;

		MoveAndSlide();
	}

	/// <summary> Size to use for collision checks. </summary>
	[Export]
	public Vector2 CollisionSize;
	/// <summary> Center of collision calculations </summary>
	public Vector3 CenterPosition
	{
		get => GlobalPosition + (UpDirection * .4f);
		set => GlobalPosition = value - (UpDirection * .4f);
	}
	public Vector3 CollisionPosition
	{
		get => GlobalPosition + (UpDirection * CollisionSize.Y);
		set => GlobalPosition = value - (UpDirection * CollisionSize.Y);
	}
	private const float CollisionPadding = .02f;

	public bool IsOnGround { get; set; }
	private readonly int GroundWhiskerAmount = 8;
	public bool CheckGround()
	{
		RaycastHit groundHit = CheckGroundRaycast();
		if (State.ExternalController != null) // Exit early when externally controlled
			return groundHit;

		if (groundHit) // Successful ground hit
		{
			if (!IsOnGround && VerticalSpeed < 0) // Landing on the ground
			{
				UpDirection = groundHit.normal;
				IsOnGround = true;
				return true;
			}

			float snapDistance = groundHit.distance - CollisionSize.Y;
			GlobalPosition -= UpDirection * snapDistance; // Remain snapped to the ground
			UpDirection = UpDirection.Lerp(groundHit.normal, .2f + (.4f * Stats.GroundSettings.GetSpeedRatio(MoveSpeed))).Normalized(); // Update world direction
			return IsOnGround;
		}

		if (IsOnGround) // REFACTOR TODO Move to state?
		{
			IsOnGround = false;
			Animator.IsFallTransitionEnabled = true;
		}

		return false;
	}

	public RaycastHit CheckGroundRaycast()
	{
		bool limitAngle = State.ExternalController != null;

		Vector3 castOrigin = CollisionPosition;
		float castLength = CollisionSize.Y + (CollisionPadding * 2.0f);
		if (IsOnGround)
			castLength += Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta; // Attempt to remain stuck to the ground when moving quickly
		else if (VerticalSpeed < 0)
			castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;

		Vector3 checkOffset = Vector3.Zero;
		RaycastHit groundHit = new();
		Vector3 castVector = this.Down() * castLength;
		int raysHit = 0;

		// Whisker casts (For smoother collision)
		float interval = Mathf.Tau / GroundWhiskerAmount;
		Vector3 castOffset = this.Forward() * ((CollisionSize.Y * .5f) - CollisionPadding);
		for (int i = 0; i < GroundWhiskerAmount; i++)
		{
			castOffset = castOffset.Rotated(this.Down(), interval);
			RaycastHit hit = this.CastRay(castOrigin + castOffset, castVector, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(castOrigin + castOffset, castVector, hit ? Colors.Red : Colors.White);
			if (ValidateGroundCast(ref hit, limitAngle))
			{
				if (!groundHit)
					groundHit = hit;
				else
					groundHit.Add(hit);
				checkOffset += castOffset;
				raysHit++;
			}
		}

		if (groundHit)
		{
			groundHit.Divide(raysHit);
			Effect.UpdateGroundType(groundHit.collidedObject);
		}

		return groundHit;
	}

	/// <summary> Checks whether raycast collider is tagged properly. </summary>
	private bool ValidateGroundCast(ref RaycastHit hit, bool limitAngle)
	{
		if (!hit)
			return new();

		if (!hit.collidedObject.IsInGroup("floor") ||
			(limitAngle && hit.normal.AngleTo(UpDirection) > Mathf.Pi * .4f))
		{
			return new();
		}

		// REFACTOR TODO Check if this is the code block that causes janky falling collisions
		if (!IsOnGround &&
			hit.collidedObject.IsInGroup("wall") &&
			hit.normal.AngleTo(Vector3.Up) > Mathf.Pi * .2f) // Use Vector3.Up for objects tagged as a wall
		{
			return new();
		}

		return hit;
	}

	/// <summary> Orientates Root to world direction, then rotates the gimbal on the y-axis </summary>
	public void UpdateOrientation()
	{
		// Untested! This may end up breaking in certain scenarios
		GlobalRotation = Vector3.Zero;
		Vector3 cross = Vector3.Left.Rotated(Vector3.Up, UpDirection.Flatten().AngleTo(Vector2.Down));
		GlobalRotate(cross, -UpDirection.SignedAngleTo(Vector3.Up, cross));
	}

	public void UpdateUpDirection(bool quickReset = true, Vector3 upDirection = new())
	{
		// Calculate target up direction
		if (upDirection.IsEqualApprox(Vector3.Zero))
		{
			upDirection = Vector3.Up;
			if (Camera.ActiveSettings.followPathTilt) // Use PathFollower.Up when on a tilted path.
				upDirection = PathFollower.Up();
		}

		// Calculate reset factor
		float resetFactor = .2f;
		if (!quickReset)
			resetFactor = VerticalSpeed > 0 ? .01f : VerticalSpeed * .2f / Runtime.MaxGravity;

		UpDirection = UpDirection.Lerp(upDirection, Mathf.Clamp(resetFactor, 0f, 1f)).Normalized();
	}
}