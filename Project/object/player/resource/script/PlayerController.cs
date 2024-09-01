using Godot;
using Project.Core;
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

	public override void _Ready()
	{
		StageSettings.RegisterPlayer(this); // Update global reference

		Controller.Initialize(this);
		Stats.Initialize();
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

		UpdateOrientation();
		UpdateLockoutTimer();
		UpdateRecenter();

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
		if (ActiveLockoutData?.ignoreSlopes == true) return; // Lockout is ignoring slopes
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

	#region Physics
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
	private readonly int GroundWhiskerAmount = 8;
	public bool CheckGround()
	{
		RaycastHit groundHit = CheckGroundRaycast();
		if (ExternalController != null) // Exit early when externally controlled
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
		bool limitAngle = ExternalController != null;

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

	/// <summary> Orientates Root to world direction, then rotates the gimbal on the y-axis. </summary>
	public void UpdateOrientation(bool allowExternalOrientation = false)
	{
		if (!allowExternalOrientation && ExternalController != null) return;

		// Untested! This may end up breaking in certain scenarios
		GlobalRotation = Vector3.Zero;
		Vector3 cross = Vector3.Left.Rotated(Vector3.Up, UpDirection.Flatten().AngleTo(Vector2.Down));
		GlobalRotate(cross, -UpDirection.SignedAngleTo(Vector3.Up, cross));
	}

	public void UpdateUpDirection(bool quickReset = true, Vector3 upDirection = new())
	{
		// Calculate target up direction
		/* REFACTOR TODO
		// Calculate reset factor
		float orientationResetFactor;
		if (ActionState == ActionStates.Stomping ||
			ActionState == ActionStates.JumpDash ||
			ActionState == ActionStates.Backflip) // Quickly reset when stomping/homing attacking
		{
			orientationResetFactor = .2f;
		}
		else if (VerticalSpeed > 0)
		{
			orientationResetFactor = .01f;
		}
		else
		{
			orientationResetFactor = VerticalSpeed * .2f / Runtime.MaxGravity;
		}
		*/

		// Calculate target up direction
		if (Camera.ActiveSettings.followPathTilt)
		{
			// Always use PathFollower.Up when on a tilted path.
			upDirection = PathFollower.Up();
		}
		else if (upDirection.IsEqualApprox(Vector3.Zero))
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

	[Export]
	private AnimationPlayer hitboxAnimator;
	public void ChangeHitbox(StringName hitboxAnimation)
	{
		hitboxAnimator.Play(hitboxAnimation);
		hitboxAnimator.Advance(0);
		hitboxAnimator.Play(hitboxAnimation);
	}
	[Signal]
	public delegate void AttackStateChangeEventHandler();
	/// <summary> Keeps track of how much attack the player will deal. </summary>
	public AttackStates AttackState
	{
		get => attackState;
		set
		{
			attackState = value;
			EmitSignal(SignalName.AttackStateChange);
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

	private bool isRecentered; // Is the recenter complete?
	private const float MinRecenterPower = .1f;
	private const float MaxRecenterPower = .2f;
	/// <summary> Recenters the  Only call this AFTER movement has occurred. </summary>
	private void UpdateRecenter()
	{
		if (!IsLockoutActive || !ActiveLockoutData.recenterPlayer) return;

		Vector3 recenterDirection = PathFollower.Forward().Rotated(UpDirection, Mathf.Pi * .5f);
		float currentOffset = PathFollower.LocalPlayerPositionDelta.X;
		float movementOffset = currentOffset;
		if (!isRecentered) // Smooth out recenter speed
		{
			float inputInfluence = ExtensionMethods.DotAngle(PathFollower.ForwardAngle + (Mathf.Pi * .5f), Controller.GetTargetInputAngle());
			inputInfluence *= Mathf.Sign(PathFollower.LocalPlayerPositionDelta.X);
			inputInfluence = (inputInfluence + 1) * 0.5f;
			inputInfluence = Mathf.Lerp(MinRecenterPower, MaxRecenterPower, inputInfluence);

			float recenterSpeed = MoveSpeed * inputInfluence;
			movementOffset = Mathf.MoveToward(movementOffset, 0, recenterSpeed * PhysicsManager.physicsDelta);
			if (Mathf.IsZeroApprox(movementOffset))
				isRecentered = true;
			movementOffset = currentOffset - movementOffset;
		}

		GlobalPosition += movementOffset * recenterDirection; // Move towards the pathfollower
	}
	#endregion

	#region State
	public bool CanJumpDash { get; set; }
	public bool DisableAccelerationJump { get; set; }
	public bool AllowSidle { get; set; }
	public bool IsInvincible { get; set; }
	public bool IsDefeated { get; set; }

	[Signal]
	public delegate void LaunchFinishedEventHandler();
	[ExportGroup("States")]
	[Export]
	public LaunchState launchState;
	public void StartLauncher(LaunchSettings settings)
	{
		if (!launchState.UpdateSettings(settings)) // Failed to start launcher state
			return;

		StateMachine.ChangeState(launchState);
	}

	[Export]
	private GrindState grindState;
	public bool AllowLandingGrind { get; set; }
	public bool IsGrindstepping { get; set; }
	public bool IsGrinding => grindState.ActiveGrindRail != null;
	public bool IsRailActivationValid(GrindRail rail) => grindState.IsRailActivationValid(rail);
	public void StartGrinding(GrindRail rail)
	{
		grindState.ActiveGrindRail = rail;
		StateMachine.ChangeState(grindState);
	}

	[Export]
	private AutomationState automationState;
	public bool IsAutomationActive => automationState.Automation != null;
	public void StartAutomation(AutomationTrigger automation)
	{
		automationState.Automation = automation;
		StateMachine.ChangeState(automationState);
	}

	// REFACTOR TODO
	[Signal]
	public delegate void KnockbackEventHandler();
	private KnockbackSettings previousKnockbackSettings;
	public void StartKnockback(KnockbackSettings knockbackSettings = new())
	{
		GD.PrintErr("Knockback hasn't been implemented yet.");
	}

	public void TakeDamage()
	{
		GD.PrintErr("Damage hasn't been implemented yet.");
	}

	public void StartInvincibility(float length = 3f)
	{
		GD.PrintErr("Invincibility hasn't been implemented yet.");
	}

	[Signal]
	public delegate void DefeatedEventHandler();
	public void StartRespawn(bool useDebugCheckpoint = false)
	{
		GD.PrintErr("Respawn hasn't been implemented yet.");
	}

	public void Teleport(TeleportTrigger tr)
	{
		GD.PrintErr("Teleport hasn't been implemented yet.");
	}

	[Signal]
	public delegate void ExternalControlStartedEventHandler();
	[Signal]
	public delegate void ExternalControlCompletedEventHandler();
	public Node ExternalController { get; private set; }
	public Node3D ExternalParent { get; private set; }
	public Vector3 ExternalOffset { get; private set; }
	private float externalSmoothing;
	public void StartExternal(Node controller, Node3D followObject = null, float smoothing = 0f, bool allowSpeedBreak = false)
	{
		ExternalController = controller;

		// REFACTOR TODO Move to states?
		Skills.IsSpeedBreakEnabled = allowSpeedBreak;

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
		GD.PushWarning("External Controllers used to check the ground. Be sure this functionality is recreated when needed.");

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
	#endregion
}

public struct KnockbackSettings
{
	/// <summary> Should the player be knocked forward? Default is false. </summary>
	public bool knockForward;
	/// <summary> Knock the player around without bouncing them into the air. </summary>
	public bool stayOnGround;
	/// <summary> Apply knockback even when invincible? </summary>
	public bool ignoreInvincibility;
	/// <summary> Don't damage the player? </summary>
	public bool disableDamage;
	/// <summary> Always apply knockback, regardless of state. </summary>
	public bool ignoreMovementState;

	/// <summary> Override default knockback amount? </summary>
	public bool overrideKnockbackSpeed;
	/// <summary> Speed to assign to player. </summary>
	public float knockbackSpeed;

	/// <summary> Override default knockback height? </summary>
	public bool overrideKnockbackHeight;
	/// <summary> Height to move player by. </summary>
	public float knockbackHeight;
}