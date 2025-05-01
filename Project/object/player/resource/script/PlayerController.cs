using Godot;
using Project.Core;
using Project.Gameplay.Objects;
using Project.Gameplay.Triggers;
using System.Collections.Generic;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[ExportGroup("Components")]
	[Export]
	public PlayerStateMachine StateMachine { get; private set; }
	[Export]
	public PlayerInputController Controller { get; private set; }
	[Export]
	public PlayerStatsController Stats { get; private set; }
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
	private StageSettings Stage => StageSettings.Instance;

	public override void _Ready()
	{
		StageSettings.RegisterPlayer(this); // Update global reference
		Stage.UpdateRingCount(Skills.StartingRingCount, StageSettings.MathModeEnum.Replace); // Start with the proper ring count
		Stage.LevelCompleted += OnLevelCompleted;
		Stage.LevelDemoStarted += OnLevelDemoStarted;

		Controller.Initialize(this);
		Stats.Initialize();
		Skills.Initialize(this);
		Lockon.Initialize(this);
		Animator.Initialize(this);
		Effect.Initialize(this);
		PathFollower.Initialize(this);
		Camera.Initialize(this);

		// Initialize state machine last to ensure components are ready		
		StateMachine.Initialize(this);

		ChangeHitbox("RESET");
		ResetCheckpointOrientation();
		SnapToGround();
		GetParent<CheckpointTrigger>().Activate(); // Save initial checkpoint
	}

	public override void _PhysicsProcess(double _)
	{
		Lockon.ProcessPhysics();
		Controller.ProcessInputs();
		StateMachine.ProcessPhysics();

		UpdateOrientation();
		UpdateLockoutTimer();
		UpdateInvincibility();
		UpdateRecenter();

		Skills.ProcessPhysics();
		Animator.ProcessPhysics();
		PathFollower.Resync();

		ExternalVelocity = Vector3.Zero; // Reset external velocity after updating player
	}

	/// <summary> Player's horizontal movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }
	/// <summary> Player's vertical speed -- only effective when not on the ground. </summary>
	public float VerticalSpeed { get; set; }
	public bool IsMovingBackward { get; set; }
	/// <summary> For movement that doesn't affect animations (e.x. wind). Reset every frame after it's applied. </summary>
	public Vector3 ExternalVelocity { get; set; }

	/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs. </summary>
	public float MovementAngle { get; set; }
	public float PathTurnInfluence => PathFollower.DeltaAngle * Camera.ActiveSettings.pathControlInfluence;
	public Vector3 GetMovementDirection()
	{
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		return PathFollower.ForwardAxis.Rotated(UpDirection, deltaAngle);
	}

	/// <summary> How much is the slope currently influencing the player? </summary>
	public float SlopeRatio { get; private set; }
	/// <summary> Slopes that are shallower than Mathf.PI * threshold are ignored. </summary>
	private readonly float SlopeThreshold = .1f;
	public void AddSlopeSpeed(bool isSliding = false)
	{
		SlopeRatio = 0;
		if (ActiveLockoutData?.ignoreSlopes == true) return; // Lockout is ignoring slopes
		if (Mathf.IsZeroApprox(MoveSpeed)) return; // Idle/Backstepping isn't affected by slopes

		// Calculate slope influence
		SlopeRatio = PathFollower.ForwardAxis.Dot(Vector3.Up);
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

	public void ApplyMovement() => ApplyMovement(GetMovementDirection());
	public void ApplyMovement(Vector3 overrideDirection)
	{
		Velocity = (overrideDirection * MoveSpeed) + (UpDirection * VerticalSpeed) + ExternalVelocity;
		MoveAndSlide();
	}

	#region Physics
	[Signal]
	public delegate void LandedOnGroundEventHandler();
	/// <summary> Size to use for collision checks. </summary>
	[ExportGroup("Physics")]
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
	public RaycastHit GroundHit { get; private set; }
	private readonly int GroundWhiskerAmount = 8;
	public bool CheckGround()
	{
		RaycastHit groundHit = CheckGroundRaycast();
		if (ExternalController != null) // Exit early when externally controlled
			return groundHit;

		if (groundHit) // Successful ground hit
		{
			if (!IsOnGround && VerticalSpeed <= 0) // Landing on the ground
			{
				UpDirection = groundHit.normal;
				IsOnGround = true;
				EmitSignal(SignalName.LandedOnGround);
			}

			float snapDistance = groundHit.distance - CollisionSize.Y;
			GlobalPosition -= UpDirection * snapDistance; // Remain snapped to the ground
			UpDirection = UpDirection.Lerp(groundHit.normal, .2f + (.4f * Stats.GroundSettings.GetSpeedRatio(MoveSpeed))).Normalized(); // Update world direction

			return IsOnGround;
		}

		if (IsOnGround)
		{
			IsOnGround = false;
			Animator.IsFallTransitionEnabled = true;
		}

		return false;
	}

	public RaycastHit CheckGroundRaycast()
	{
		Vector3 castOrigin = CollisionPosition;
		float castLength = CollisionSize.Y + (CollisionPadding * 2.0f);
		if (IsOnGround)
			castLength += Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta; // Attempt to remain stuck to the ground when moving quickly
		else if (VerticalSpeed < 0)
			castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;

		if (!ExternalVelocity.IsZeroApprox() && IsOnGround)
			castLength += ExternalVelocity.Length() * PhysicsManager.physicsDelta;

		Vector3 checkOffset = Vector3.Zero;
		GroundHit = new();
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
			if (!ValidateGroundCast(ref hit))
				continue;

			GroundHit = RaycastHit.Add(GroundHit, hit);
			raysHit++;
			checkOffset += castOffset;
		}

		if (GroundHit)
		{
			GroundHit = RaycastHit.Divide(GroundHit, raysHit);
			Effect.UpdateGroundType(GroundHit.collidedObject);
			DebugManager.DrawRay(GroundHit.point, GroundHit.normal * 5f, Colors.Orange);
		}

		return GroundHit;
	}

	/// <summary> Checks whether raycast collider is tagged properly. </summary>
	private bool ValidateGroundCast(ref RaycastHit hit)
	{
		if (!hit)
			return new();

		if (!hit.collidedObject.IsInGroup("floor") ||
			(ExternalController == null && hit.normal.AngleTo(UpDirection) > Mathf.Pi * .4f))
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

	/// <summary> Attempts to snap the player to the ground. </summary>
	public void SnapToGround(float distance = 100.0f)
	{
		KinematicCollision3D collision = MoveAndCollide(-UpDirection * distance, true);
		if (collision == null) return;

		IsOnGround = true;
		MoveAndCollide(-UpDirection * distance);
		Animator.SnapToGround();
	}

	public new bool IsOnWall { get; set; }
	public RaycastHit WallRaycastHit { get; set; }
	/// <summary> Checks for walls forward and backwards (only in the direction the player is moving). </summary>
	public void CheckWall(Vector3 castDirection = new(), bool reduceSpeedDuringHeadonCollision = true)
	{
		if (Controller.IsStrafeModeActive)
			CheckStrafeWall();

		IsOnWall = false;

		if (castDirection.IsZeroApprox())
			castDirection = GetMovementDirection();
		float castLength = CollisionSize.X + CollisionPadding + (Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta);

		WallRaycastHit = this.CastRay(CollisionPosition, castDirection * castLength, CollisionMask, false, GetCollisionExceptions());
		DebugManager.DrawRay(CollisionPosition, castDirection * castLength, WallRaycastHit ? Colors.Red : Colors.White);

		if (!ValidateWallCast(WallRaycastHit))
		{
			WallRaycastHit = new();
			return;
		}

		float wallDelta = ExtensionMethods.DeltaAngleRad(ExtensionMethods.CalculateForwardAngle(WallRaycastHit.normal.RemoveVertical(), IsOnGround ? PathFollower.Up() : Vector3.Up), MovementAngle);
		if (wallDelta >= Mathf.Pi * .8f) // Process head-on collision
		{
			// Cancel speed break
			if (Skills.IsSpeedBreakActive && !WallRaycastHit.collidedObject.IsInGroup("level wall"))
			{
				float pathDelta = ExtensionMethods.DeltaAngleRad(PathFollower.BackAngle, ExtensionMethods.CalculateForwardAngle(WallRaycastHit.normal));
				if (pathDelta >= Mathf.Pi * .25f) // Snap to path direction
				{
					MovementAngle = PathFollower.ForwardAngle;
					return;
				}

				Skills.CallDeferred(PlayerSkillController.MethodName.ToggleSpeedBreak);
			}

			if (reduceSpeedDuringHeadonCollision)
			{
				if (WallRaycastHit.distance <= CollisionSize.X + CollisionPadding)
					MoveSpeed = 0; // Kill speed
				else if (WallRaycastHit.distance <= CollisionSize.X + CollisionPadding + (MoveSpeed * PhysicsManager.physicsDelta))
					MoveSpeed *= .9f; // Slow down drastically
			}

			IsOnWall = true;
			return;
		}

		if (Controller.IsStrafeModeActive || IsMovingBackward || !IsOnGround)
			return;

		// Reduce MoveSpeed when running against walls
		float speedClamp = Mathf.Clamp(1.0f - (wallDelta / Mathf.Pi * .4f), 0f, 1f); // Arbitrary formula that works well
		if (Stats.GroundSettings.GetSpeedRatio(MoveSpeed) > speedClamp)
			MoveSpeed *= speedClamp;
	}

	/// <summary> Checks Sonic's side, then realigns to PathFollower if necessary. </summary>
	private void CheckStrafeWall()
	{
		float angle = ExtensionMethods.SignedDeltaAngleRad(PathFollower.ForwardAngle, MovementAngle);
		Vector3 castDirection = Animator.Left() * Mathf.Sign(angle);
		float castLength = CollisionSize.X + CollisionPadding + (Mathf.Sin(Mathf.Abs(angle)) * Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta); ;

		RaycastHit wallHit = this.CastRay(CollisionPosition, castDirection * castLength, CollisionMask, false, GetCollisionExceptions());
		DebugManager.DrawRay(CollisionPosition, castDirection * castLength, wallHit ? Colors.Red : Colors.White);

		if (ValidateWallCast(wallHit))
			MovementAngle = PathFollower.ForwardAngle;
	}

	private bool ValidateWallCast(RaycastHit hit) => hit && hit.collidedObject.IsInGroup("wall");

	/// <summary> Checks for ceilings and crushers. </summary>
	public bool CheckCeiling()
	{
		// Start from below the floor and cast through the player to ensure object detection
		Vector3 castOrigin = GlobalPosition - (UpDirection * CollisionPadding);
		float castLength = (CollisionSize.Y + CollisionPadding) * 2.0f;
		if (VerticalSpeed > 0)
			castLength += VerticalSpeed * PhysicsManager.physicsDelta;

		Vector3 castVector = UpDirection * castLength;
		if (IsBackflipping) // Improve collision detection when backflipping
			castVector += GetMovementDirection() * MoveSpeed * PhysicsManager.physicsDelta;

		RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, CollisionMask, false, GetCollisionExceptions());
		DebugManager.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

		if (!ceilingHit)
			return false;

		// Check if the player is being crushed
		if (!IsBackflipping && ceilingHit.collidedObject.IsInGroup("crusher") && GroundHit)
		{
			// Prevent clipping through the ground
			AddCollisionExceptionWith(ceilingHit.collidedObject);
			StartKnockback(new()
			{
				ignoreInvincibility = true,
			});

			return true;
		}

		if (!ceilingHit.collidedObject.IsInGroup("ceiling"))
			return false;

		GlobalTranslate(ceilingHit.point - (CollisionPosition + (UpDirection * CollisionSize.Y)));

		float maxVerticalSpeed = 0;
		// Workaround for backflipping into slanted ceilings
		if (IsBackflipping)
		{
			float ceilingAngle = ceilingHit.normal.AngleTo(Vector3.Down);

			if (ceilingAngle > Mathf.Pi * .1f) // Only slanted ceilings need this workaround
			{
				float deltaAngle = ExtensionMethods.DeltaAngleRad(PathFollower.ForwardAngle, ExtensionMethods.CalculateForwardAngle(ceilingHit.normal, IsOnGround ? PathFollower.Up() : Vector3.Up));
				if (deltaAngle > Mathf.Pi * .1f) // Wall isn't aligned to the path
					return false;

				// Slide down the wall if it's aligned with the path direction
				maxVerticalSpeed = -Mathf.Sin(ceilingAngle) * MoveSpeed;
			}
		}

		if (VerticalSpeed > maxVerticalSpeed)
			VerticalSpeed = maxVerticalSpeed;

		return false;
	}

	/// <summary> Orientates Root to world direction, then rotates the gimbal on the y-axis. </summary>
	public void UpdateOrientation(bool allowExternalOrientation = false)
	{
		if (!allowExternalOrientation && ExternalController != null) return;

		// Untested! This may end up breaking in certain scenarios
		GlobalRotation = Vector3.Zero;
		float angle = UpDirection.Flatten().AngleTo(Vector2.Down);
		Vector3 cross = Vector3.Left.Rotated(Vector3.Up, angle);
		GlobalRotate(cross, -UpDirection.SignedAngleTo(Vector3.Up, cross));
	}

	public void UpdateUpDirection(bool quickReset = false, Vector3 upDirection = new())
	{
		// Calculate target up direction
		if (Camera.ActiveSettings.followPathTilt) // Always use PathFollower.Up when on a tilted path
			upDirection = PathFollower.Up();
		else if (upDirection.IsEqualApprox(Vector3.Zero))
			upDirection = Vector3.Up;

		// Calculate reset factor
		float resetFactor = .2f;
		if (!quickReset)
			resetFactor = VerticalSpeed > 0 ? .01f : VerticalSpeed * .2f / Runtime.MaxGravity;

		UpDirection = UpDirection.Lerp(upDirection, Mathf.Clamp(resetFactor, 0f, 1f)).Normalized();
	}

	[Export] private AnimationPlayer hitboxAnimator;
	public void ChangeHitbox(StringName hitboxAnimation)
	{
		hitboxAnimator.Play(hitboxAnimation);
		hitboxAnimator.Advance(0);
		hitboxAnimator.Play(hitboxAnimation);
	}
	[Signal]
	public delegate void AttackStateChangedEventHandler();
	/// <summary> Keeps track of how much attack the player will deal. </summary>
	public AttackStates AttackState
	{
		get => attackState;
		set
		{
			attackState = value;
			EmitSignal(SignalName.AttackStateChanged);
		}
	}
	private AttackStates attackState;
	public enum AttackStates
	{
		None, // Player is not attacking
		Weak, // Player will deal a single point of damage 
		Strong, // Double Damage -- Perfect homing attacks
		OneShot, // Destroy enemies immediately (i.e. Speedbreak and Crest of Fire)
	}
	public void ResetAttackState() => attackState = AttackStates.None;

	private float lockoutTimer;
	public bool IsLockoutActive => ActiveLockoutData != null;
	public bool IsLockoutOverridingMovementAngle => IsLockoutActive && ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free;
	public bool IsLockoutDisablingAction(LockoutResource.ActionFlags flag) => IsLockoutActive && ActiveLockoutData.disableActionFlags.HasFlag(flag);
	public LockoutResource ActiveLockoutData { get; private set; }

	private readonly List<LockoutResource> lockoutDataList = [];

	/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
	public void AddLockoutData(LockoutResource resource)
	{
		if (!lockoutDataList.Contains(resource))
		{
			lockoutDataList.Add(resource); // Add the new lockout data
			if (lockoutDataList.Count >= 2) // List only needs to be sorted if there are multiple elements on it
				lockoutDataList.Sort(new LockoutResource.Comparer());

			if (ActiveLockoutData?.priority == -1) // Remove current lockout?
				RemoveLockoutData(ActiveLockoutData);

			if (resource.priority == -1) // Exclude from priority, take over immediately
				SetLockoutData(resource);
			else
				ProcessCurrentLockoutData();
		}
		else if (ActiveLockoutData == resource) // Reset lockout timer
		{
			lockoutTimer = 0;
		}
	}

	/// <summary> Removes a ControlLockoutResource from the list. </summary>
	public void RemoveLockoutData(LockoutResource resource)
	{
		if (!lockoutDataList.Contains(resource)) return;
		lockoutDataList.Remove(resource);
		ProcessCurrentLockoutData();
	}

	/// <summary> Recalculates the active lockout data. Called whenever the lockout list is modified. </summary>
	private void ProcessCurrentLockoutData()
	{
		if (IsLockoutActive && lockoutDataList.Count == 0) // Disable lockout
			SetLockoutData(null);
		else if (ActiveLockoutData != lockoutDataList[^1]) // Change to current data (Highest priority, last on the list)
			SetLockoutData(lockoutDataList[^1]);
	}

	private void SetLockoutData(LockoutResource resource)
	{
		ActiveLockoutData = resource;

		if (resource != null) // Reset flags
		{
			lockoutTimer = 0;
			isRecentered = false;
		}
	}

	private void UpdateLockoutTimer()
	{
		if (!IsLockoutActive || Mathf.IsZeroApprox(ActiveLockoutData.length))
			return;

		lockoutTimer = Mathf.MoveToward(lockoutTimer, ActiveLockoutData.length, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(lockoutTimer, ActiveLockoutData.length))
			RemoveLockoutData(ActiveLockoutData);
	}

	private void OnLevelCompleted()
	{
		// Disable everything
		Lockon.IsMonitoring = false;
		Skills.DisableBreakSkills();

		if (Stage.LevelState == StageSettings.LevelStateEnum.Failed || Stage.Data.CompletionLockout == null)
			AddLockoutData(Runtime.Instance.DefaultCompletionLockout);
		else
			AddLockoutData(Stage.Data.CompletionLockout);
	}

	private void OnLevelDemoStarted()
	{
		MoveSpeed = 0;
		AddLockoutData(Runtime.Instance.DefaultCompletionLockout);
	}

	private bool isRecentered; // Is the recenter complete?
	private const float MinRecenterPower = .1f;
	private const float MaxRecenterPower = .2f;
	private const float AirRecenterPower = .3f;
	/// <summary> Recenters the  Only call this AFTER movement has occurred. </summary>
	private void UpdateRecenter()
	{
		if (IsHomingAttacking)
			return;

		if (!IsLockoutActive || !ActiveLockoutData.recenterPlayer) return;

		if (ExternalParent != null || IsLaunching) // Player's position is being overridden
		{
			isRecentered = false;
			return;
		}

		Vector3 recenterDirection = PathFollower.ForwardAxis.Rotated(UpDirection, Mathf.Pi * .5f);
		float currentOffset = PathFollower.LocalPlayerPositionDelta.X;
		float movementOffset = currentOffset;
		if (!isRecentered) // Smooth out recenter speed
		{
			float inputInfluence = ExtensionMethods.DotAngle(PathFollower.ForwardAngle + (Mathf.Pi * .5f), Controller.GetTargetInputAngle());
			inputInfluence *= Mathf.Sign(PathFollower.LocalPlayerPositionDelta.X);
			inputInfluence = (inputInfluence + 1) * 0.5f;
			inputInfluence = Mathf.Lerp(MinRecenterPower, MaxRecenterPower, inputInfluence);

			float recenterSpeed = MoveSpeed * inputInfluence + Mathf.Abs(VerticalSpeed * AirRecenterPower);
			movementOffset = Mathf.MoveToward(movementOffset, 0, recenterSpeed * PhysicsManager.physicsDelta);
			if (Mathf.IsZeroApprox(movementOffset))
				isRecentered = true;
			movementOffset = currentOffset - movementOffset;
		}

		GlobalPosition += movementOffset * recenterDirection; // Move towards the pathfollower
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is not PhysicsBody3D) return;

		if (GetCollisionExceptions().Contains(body as PhysicsBody3D))
		{
			GD.Print($"Stopped ignoring {body.Name}");
			RemoveCollisionExceptionWith(body);
		}
	}
	#endregion

	#region State
	public bool CanJumpDash { get; set; }
	public bool IsJumpDashing { get; set; }
	public bool IsHomingAttacking { get; set; }
	public bool IsPerfectHomingAttacking { get; set; }
	public bool IsJumpDashOrHomingAttack => IsJumpDashing || IsHomingAttacking;
	public bool IsJumping { get; set; }
	public bool IsAccelerationJumping { get; set; }
	public bool IsBackflipping { get; set; }
	public bool IsStomping { get; set; }
	public bool ForceAccelerationJump { get; set; }
	public bool DisableAccelerationJump { get; set; }
	public bool DisableDamage { get; set; }
	/// <summary> True while the player is defeated but hasn't respawned yet. </summary>
	public bool IsDefeated { get; set; }
	public bool AllowLandingSkills { get; set; }

	[ExportGroup("States")]
	[Export] private CountdownState countdownState;
	public bool IsCountdown { get; set; }
	public void StartCountdown() => StateMachine.ChangeState(countdownState);

	[Export] private ReversePathState reversePathState;
	public bool IsReversePath => StateMachine.CurrentState == reversePathState;
	public void StartReversePath() => StateMachine.ChangeState(reversePathState);

	[Signal]
	public delegate void LaunchFinishedEventHandler();
	public bool IsLaunching { get; set; }
	public Launcher ActiveLauncher => launchState.ActiveLauncher;
	[Export] private LaunchState launchState;
	public void StartLauncher(LaunchSettings settings)
	{
		if (!launchState.UpdateSettings(settings)) // Failed to start launcher state
			return;

		StateMachine.ChangeState(launchState);
	}

	[Export] private LaunchRingState launchRingState;
	public void StartLaunchRing(LaunchRing launcher)
	{
		launchRingState.Launcher = launcher;
		StateMachine.ChangeState(launchRingState);
	}

	[Export] private CatapultState catapultState;
	public bool IsCatapultActive => catapultState.Catapult != null;
	public void StartCatapult(Catapult catapult)
	{
		catapultState.Catapult = catapult;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, catapultState);
	}

	[Export] private FlyingPotState flyingPotState;
	public bool IsFlyingPotActive => flyingPotState.Pot != null;
	public void StartFlyingPot(FlyingPot pot)
	{
		flyingPotState.Pot = pot;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, flyingPotState);
	}

	[Export] private PathTravellerState pathTravellerState;
	public void StartPathTraveller(PathTraveller traveller)
	{
		pathTravellerState.Traveller = traveller;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, pathTravellerState);
	}

	[Export] private SpinJumpState spinJumpState;
	public bool IsSpinJump { get; set; }
	public void StartSpinJump(bool isShortenedJump)
	{
		spinJumpState.IsShortenedJump = isShortenedJump;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, spinJumpState);
	}

	private readonly float SpinJumpBounceAmount = 3.0f;
	public void StartSpinJumpBounce() => VerticalSpeed = Runtime.CalculateJumpPower(SpinJumpBounceAmount);

	[Export] private QuickStepState quickStepState;
	public void StartQuickStep(bool isSteppingRight)
	{
		quickStepState.IsSteppingRight = isSteppingRight;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, quickStepState);
	}

	[Export] private LightSpeedDashState lightSpeedDashState;
	public bool IsLightDashing => lightSpeedDashState.CurrentTarget != null;
	public bool StartLightSpeedDash()
	{
		if (lightSpeedDashState.GetNewTarget() != null)
			StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, lightSpeedDashState);

		return IsLightDashing;
	}

	[Export] private LightSpeedAttackState lightSpeedAttackState;
	public bool IsLightSpeedAttacking { get; set; }
	public bool StartLightSpeedAttack()
	{
		Lockon.ProcessPhysics();
		if (Lockon.IsTargetAttackable)
			StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, lightSpeedAttackState);
		IsLightSpeedAttacking = Lockon.IsTargetAttackable;

		return IsLightSpeedAttacking;
	}

	[Export] private GrindState grindState;
	public bool AllowLandingGrind { get; set; }
	public bool IsGrindstepping { get; set; }
	public bool IsGrinding { get; set; }
	public bool IsGrindRailActive => grindState.ActiveGrindRail != null;
	public bool IsRailActivationValid(GrindRail rail) => grindState.IsRailActivationValid(rail);
	public void StartGrinding(GrindRail rail)
	{
		grindState.ActiveGrindRail = rail;
		StateMachine.ChangeState(grindState);
	}

	public void UnregisterGrindrail(GrindRail rail)
	{
		if (grindState.ActiveGrindRail == rail)
			grindState.ActiveGrindRail = null;
	}

	public bool DisableSidle { get; set; }
	public bool IsSidling => sidleState.Trigger != null;
	[Export] private SidleState sidleState;
	public void StartSidle(SidleTrigger trigger)
	{
		sidleState.Trigger = trigger;
		StateMachine.ChangeState(sidleState);
	}

	public void StopSidle() => sidleState.Trigger = null;

	public void SetFoothold(Node foothold) => sidleState.ActiveFoothold = foothold;
	public void UnsetFoothold(Node foothold)
	{
		if (sidleState.ActiveFoothold == foothold)
			sidleState.ActiveFoothold = null;
	}

	[Export] private AutomationState automationState;
	public bool IsAutomationActive => automationState.Automation != null;
	public void StartAutomation(AutomationTrigger automation)
	{
		if (IsAutomationActive && automationState.Automation != automation)
			automationState.ExitState();

		automationState.Automation = automation;
		StateMachine.ChangeState(automationState);
	}

	[Export] private EventState eventState;
	public void StartEvent(EventTrigger trigger)
	{
		eventState.Trigger = trigger;
		StateMachine.CallDeferred(PlayerStateMachine.MethodName.ChangeState, eventState);
	}

	[Export] private BounceState bounceState;
	public bool IsBouncing { get; set; }
	public bool IsBounceInteruptable { get; set; }
	public void StartBounce(bool isUpwardBounce = true, float bounceScaleOverride = 1f)
	{
		IsBouncing = true;
		IsBounceInteruptable = false;
		bounceState.BounceHeightScale = bounceScaleOverride;
		bounceState.IsUpwardBounce = isUpwardBounce;
		StateMachine.ChangeState(bounceState);
	}

	[Export] private DriftState driftState;
	public bool IsDrifting => driftState.Trigger != null;
	public void StartDrift(DriftTrigger trigger)
	{
		driftState.Trigger = trigger;
		StateMachine.ChangeState(driftState);
	}

	[Export] private IvyState ivyState;
	public void StartIvy(Ivy trigger)
	{
		ivyState.Trigger = trigger;
		ivyState.UpdateHighSpeedEntry();
		StateMachine.ChangeState(ivyState);
	}

	[Export] private ZiplineState ziplineState;
	public bool IsZiplineActive => ziplineState.Trigger != null;
	public void StartZipline(Zipline trigger)
	{
		ziplineState.Trigger = trigger;
		StateMachine.ChangeState(ziplineState);
	}

	[Export] private PetrifyState petrifyState;
	public bool IsPetrified => StateMachine.CurrentState == petrifyState;
	public void StartPetrify() => StateMachine.ChangeState(petrifyState);

	[Export] private LeverState leverState;
	public void StartLever(Lever lever)
	{
		leverState.Trigger = lever;
		StateMachine.ChangeState(leverState);
	}


	[Signal]
	public delegate void KnockbackEventHandler();
	[Export] private KnockbackState knockbackState;
	public bool IsKnockback { get; set; }
	public bool StartKnockback(KnockbackSettings settings = new())
	{
		// Disable damage when not in-game
		if (!Stage.IsLevelIngame) return false;

		EmitSignal(SignalName.Knockback); // Emit signal FIRST so external controllers can be alerted

		if (IsTeleporting || IsDefeated) return false;
		if (IsInvincible && !settings.ignoreInvincibility) return false;
		if (ExternalController != null && !settings.ignoreMovementState) return false;

		UpDirection = Vector3.Up;
		knockbackState.Settings = settings;
		StateMachine.ChangeState(knockbackState);
		return true;
	}

	public void TakeDamage()
	{
		if (!Stage.IsLevelIngame) return;

		AllowLandingSkills = false; // Disable landing skills

		// No rings; Respawn
		if (Stage.CurrentRingCount == 0)
		{
			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.PearlRespawn) && Skills.IsSoulGaugeCharged)
			{
				// Lose soul power and continue
				Skills.ModifySoulGauge(-PlayerSkillController.MinimumSoulPower);
			}
			else
			{
				Effect.PlayVoice("defeat");
				StartRespawn();
				return;
			}
		}

		Effect.PlayVoice("hurt");

		int ringLoss = 20;
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingLossConvert)) // Don't lose ANY soul power when ring -> soul conversion skill is active
		{
			Effect.PlayDarkSpiralFX(); // Play a VFX instead
		}
		else if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.PearlDamage)) // Lose soul power
		{
			if (SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.PearlDamage) == 1) // Damage augment
			{
				ringLoss += 20;
				Skills.ModifySoulGauge(-Mathf.FloorToInt(Skills.SoulPower * .1f));
			}
			else
			{
				Skills.ModifySoulGauge(-Mathf.FloorToInt(Skills.SoulPower * .2f));
			}
		}
		else
		{
			Skills.ModifySoulGauge(-Mathf.FloorToInt(Skills.SoulPower * .5f));
		}

		// Add in defense lowering augments
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingLossConvert) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.RingLossConvert) == 1)
		{
			ringLoss += 20;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SpeedUp) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.SpeedUp) == 3)
		{
			ringLoss += 20;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.TractionUp) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.TractionUp) == 3)
		{
			ringLoss += 20;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AccelJumpAttack) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.AccelJumpAttack) == 1)
		{
			ringLoss += 20;
		}

		// Defense up
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingDamage))
			ringLoss -= 10;

		// Lose rings
		ringLoss = Mathf.Max(ringLoss, 0);
		Stage.UpdateRingCount(ringLoss, StageSettings.MathModeEnum.Subtract);
		Stage.IncrementDamageCount();

		// Level failed
		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Perfect)
		{
			DefeatPlayer();
			Stage.FinishLevel(false);
		}
	}

	public bool IsInvincible => invincibilityTimer != 0 || DisableDamage || IsTeleporting;
	private float invincibilityTimer;
	private const float InvincibilityLength = 3f;
	public void StartInvincibility(float timeScale = 1f)
	{
		invincibilityTimer = InvincibilityLength / timeScale;
		Animator.StartInvincibility(timeScale);
	}

	private void UpdateInvincibility()
	{
		if (Mathf.IsZeroApprox(invincibilityTimer))
			return;

		invincibilityTimer = Mathf.MoveToward(invincibilityTimer, 0, PhysicsManager.physicsDelta);
	}

	[Signal]
	public delegate void DefeatedEventHandler();
	private void DefeatPlayer()
	{
		if (IsDefeated) return;

		IsDefeated = true;
		Lockon.IsMonitoring = false;
		ChangeHitbox("disable");

		// Disable break skills
		if (Skills.IsTimeBreakActive)
			Skills.ToggleTimeBreak();
		if (Skills.IsSpeedBreakActive)
			Skills.ToggleSpeedBreak();

		EmitSignal(SignalName.Defeated);
	}

	public bool IsDebugRespawn { get; private set; }
	public void StartRespawn(bool debugRespawn = false)
	{
		IsDebugRespawn = debugRespawn;
		if (TransitionManager.IsTransitionActive || IsTeleporting || IsDefeated || !Stage.IsLevelIngame) return;

		DefeatPlayer();

		if (!IsDebugRespawn &&
			(Stage.Data.MissionType == LevelDataResource.MissionTypes.Deathless
			|| Stage.Data.MissionType == LevelDataResource.MissionTypes.Perfect))
		{
			// Level failed
			Stage.FinishLevel(false);
			return;
		}

		// Fade screen out and connect signals
		TransitionManager.StartTransition(new()
		{
			inSpeed = .5f,
			outSpeed = .5f,
			color = Colors.Black // Use Colors.Transparent for debugging
		});

		TransitionManager.instance.TransitionProcess += ProcessRespawn;
	}

	private void ProcessRespawn()
	{
		AllowLandingSkills = false; // Disable landing skills temporarily
		Skills.IsSpeedBreakEnabled = Skills.IsTimeBreakEnabled = true; // Reenable soul skills

		BonusManager.instance.CancelBonuses();

		Stage.RevertToCheckpointData();
		Stage.RespawnObjects();
		Stage.IncrementRespawnCount();
		Stage.UpdateRingCount(Skills.RespawnRingCount, StageSettings.MathModeEnum.Replace, true); // Reset ring count

		CheckpointTrigger currentCheckpoint = IsDebugRespawn ? DebugManager.Instance.DebugCheckpoint : Stage.CurrentCheckpoint;
		Teleport(currentCheckpoint);
		PathFollower.SetActivePath(currentCheckpoint.PlayerPath); // Revert path
		Camera.PathFollower.SetActivePath(currentCheckpoint.CameraPath);

		IsDefeated = false;
		IsMovingBackward = false;
		MoveSpeed = VerticalSpeed = 0;

		// Clear any collision exceptions
		foreach (Node exception in GetCollisionExceptions())
			RemoveCollisionExceptionWith(exception);

		TransitionManager.instance.TransitionProcess -= ProcessRespawn;
		FinishRespawn();
	}

	/// <summary> Final step of the respawn process. Re-enable area collider and finish transition. </summary>
	private void FinishRespawn()
	{
		ResetCheckpointOrientation();
		SnapToGround();
		ChangeHitbox("RESET");

		invincibilityTimer = 0; // Reset invincibility

		PathFollower.Resync();
		Camera.CallDeferred(PlayerCameraController.MethodName.Respawn);
		TransitionManager.FinishTransition();
	}

	private void ResetCheckpointOrientation()
	{
		UpDirection = Vector3.Up;

		if (IsDebugRespawn)
			GlobalTransform = DebugManager.Instance.DebugCheckpoint.GlobalTransform;
		else if (Stage.CurrentCheckpoint == null) // Default to parent node's position
			Transform = Transform3D.Identity;
		else
			GlobalTransform = Stage.CurrentCheckpoint.GlobalTransform;

		MovementAngle = PathFollower.ForwardAngle; // Reset movement angle
		Animator.SnapRotation(MovementAngle);
	}

	[Export] private TeleportState teleportState;
	public bool IsTeleporting { get; set; }
	public void Teleport(TeleportTrigger trigger)
	{
		teleportState.UpdateTrigger(trigger);
		StateMachine.ChangeState(teleportState);
	}

	[Signal]
	public delegate void ExternalControlStartedEventHandler();
	[Signal]
	public delegate void ExternalControlCompletedEventHandler();
	public Node ExternalController { get; private set; }
	public Node3D ExternalParent { get; private set; }
	public Vector3 ExternalOffset { get; private set; }
	private float externalSmoothing;
	public void StartExternal(Node controller, Node3D followObject = null, float smoothing = 0f)
	{
		ExternalController = controller;

		ExternalParent = followObject;
		ExternalOffset = Vector3.Zero; // Reset offset
		externalSmoothing = smoothing;
		if (ExternalParent != null && !Mathf.IsZeroApprox(smoothing)) // Smooth out transition
			ExternalOffset = GlobalPosition - ExternalParent.GlobalPosition;

		UpdateExternalControl();
		EmitSignal(SignalName.ExternalControlStarted);
	}

	public void UpdateExternalControl(bool autoResync = false)
	{
		CheckGround();

		if (ExternalParent != null)
		{
			if (ExternalParent is BoneAttachment3D) // Ensure BoneAttachments are updated
				(ExternalParent as BoneAttachment3D).OnBonePoseUpdate((ExternalParent as BoneAttachment3D).BoneIdx);

			GlobalTransform = ExternalParent.GlobalTransform;
		}

		ExternalOffset = ExternalOffset.Lerp(Vector3.Zero, externalSmoothing); // Smooth out entry
		GlobalPosition += ExternalOffset;

		if (autoResync)
			PathFollower.Resync();
		else
			PathFollower.RecalculateData();
	}

	public void StopExternal()
	{
		ExternalController = null;
		ExternalParent = null;
		UpdateOrientation();
		EmitSignal(SignalName.ExternalControlCompleted);
	}

	public void Activate()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;

		Camera.Camera.Current = true; // Reactivate camera (for cutscenes)
		Lockon.IsReticleVisible = !DebugManager.Instance.DisableReticle;

		if (Stage.IsControlTest)
			return;

		HeadsUpDisplay.Instance.Visible = true;
		Interface.PauseMenu.AllowPausing = true;
	}

	public void Deactivate()
	{
		if (Skills.IsUsingBreakSkills)
			Skills.CancelBreakSkills();

		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;

		Lockon.IsReticleVisible = false;

		if (Stage.IsControlTest)
			return;

		HeadsUpDisplay.Instance.Visible = false;
		Interface.PauseMenu.AllowPausing = false;
	}
	#endregion
}
