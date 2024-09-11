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
	private StageSettings Stage => StageSettings.Instance;

	public override void _Ready()
	{
		StageSettings.RegisterPlayer(this); // Update global reference
		Stage.UpdateRingCount(Skills.StartingRingCount, StageSettings.MathModeEnum.Replace); // Start with the proper ring count
		Stage.Connect(StageSettings.SignalName.LevelCompleted, new Callable(this, MethodName.OnLevelCompleted));
		Stage.Connect(StageSettings.SignalName.LevelDemoStarted, new Callable(this, MethodName.OnLevelDemoStarted));

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

		GetParent<CheckpointTrigger>().Activate(); // Save initial checkpoint
	}

	public override void _PhysicsProcess(double _)
	{
		Controller.ProcessInputs();
		StateMachine.ProcessPhysics();

		UpdateOrientation();
		UpdateLockoutTimer();
		UpdateInvincibility();
		UpdateRecenter();

		Skills.ProcessPhysics();
		Lockon.ProcessPhysics();
		Animator.ProcessPhysics();
		PathFollower.Resync();
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
		Velocity = GetMovementDirection() * MoveSpeed + UpDirection * VerticalSpeed;
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

	public new bool IsOnWall { get; set; }
	// Checks for walls forward and backwards (only in the direction the player is moving).
	public void CheckWall()
	{
		IsOnWall = false;
		Vector3 velocity = GetMovementDirection();
		if (Mathf.IsZeroApprox(MoveSpeed)) // No movement
		{
			DebugManager.DrawRay(CollisionPosition, velocity * CollisionSize.X, Colors.White);
			return;
		}

		// REFACTOR TODO? velocity *= Mathf.Sign(MoveSpeed);
		float castLength = CollisionSize.X + CollisionPadding + (Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta);

		RaycastHit wallHit = this.CastRay(CollisionPosition, velocity * castLength, CollisionMask, false, GetCollisionExceptions());
		DebugManager.DrawRay(CollisionPosition, velocity * castLength, wallHit ? Colors.Red : Colors.White);

		if (!ValidateWallCast(wallHit))
			return;

		float wallDelta = ExtensionMethods.DeltaAngleRad(ExtensionMethods.CalculateForwardAngle(wallHit.normal.RemoveVertical(), IsOnGround ? PathFollower.Up() : Vector3.Up), MovementAngle);
		if (wallDelta >= Mathf.Pi * .75f) // Process wall collision 
		{
			if (IsJumpDashing &&
				wallHit.collidedObject.IsInGroup("splash jump") &&
				SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SplashJump))
			{
				// Perform a splash jump
				// REFACTOR TODO Lockon.StopHomingAttack();
				Effect.PlaySplashJumpFX();
				Animator.SplashJumpAnimation();
				VerticalSpeed = Runtime.CalculateJumpPower(Stats.JumpHeight * .5f);
				return;
			}

			// Cancel speed break
			if (Skills.IsSpeedBreakActive)
			{
				float pathDelta = ExtensionMethods.DeltaAngleRad(PathFollower.BackAngle, ExtensionMethods.CalculateForwardAngle(wallHit.normal));
				if (pathDelta >= Mathf.Pi * .25f) // Snap to path direction
				{
					MovementAngle = PathFollower.ForwardAngle;
					return;
				}

				Skills.CallDeferred(CharacterSkillManager.MethodName.ToggleSpeedBreak);
			}

			// Kill speed when jump dashing into a wall to prevent splash jump from becoming obsolete
			if (IsJumpDashing && wallHit.collidedObject.IsInGroup("splash jump"))
			{
				MoveSpeed = 0;
				VerticalSpeed = Mathf.Clamp(VerticalSpeed, -Mathf.Inf, 0);
			}

			// Running into wall head-on
			if (wallDelta >= Mathf.Pi * .8f)
			{
				if (wallHit.distance <= CollisionSize.X + CollisionPadding)
					MoveSpeed = 0; // Kill speed
				else if (wallHit.distance <= CollisionSize.X + CollisionPadding + (MoveSpeed * PhysicsManager.physicsDelta))
					MoveSpeed *= .9f; // Slow down drastically

				IsOnWall = true;
				return;
			}
		}

		if (!IsMovingBackward && IsOnGround) // Reduce MoveSpeed when running against walls
		{
			float speedClamp = Mathf.Clamp(1.0f - (wallDelta / Mathf.Pi * .4f), 0f, 1f); // Arbitrary formula that works well
			if (Stats.GroundSettings.GetSpeedRatio(MoveSpeed) > speedClamp)
				MoveSpeed *= speedClamp;
		}
	}

	private bool ValidateWallCast(RaycastHit hit) => hit && hit.collidedObject.IsInGroup("wall");

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
	public bool IsLockoutDisablingActions => IsLockoutActive && ActiveLockoutData.disableActions;
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
		/* REFACTOR TODO?
		if (ActionState != ActionStates.Damaged)
			ResetActionState();
		*/

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
	public bool IsJumpDashing { get; set; }
	public bool IsJumpDashOrHomingAttack => IsJumpDashing || Lockon.IsHomingAttacking;
	public bool IsAccelerationJumping { get; set; }
	public bool DisableAccelerationJump { get; set; }
	public bool DisableDamage { get; set; }
	public bool AllowSidle { get; set; }
	/// <summary> True while the player is defeated but hasn't respawned yet. </summary>
	public bool IsDefeated { get; set; }
	public bool AllowLandingSkills { get; set; }

	[ExportGroup("States")]
	[Export]
	private CountdownState countdownState;
	public void StartCountdown() => StateMachine.ChangeState(countdownState);


	public bool IsBackflipping { get; set; }

	[Signal]
	public delegate void LaunchFinishedEventHandler();
	[Export]
	public LaunchState launchState;
	public void StartLauncher(LaunchSettings settings)
	{
		if (!launchState.UpdateSettings(settings)) // Failed to start launcher state
			return;

		StateMachine.ChangeState(launchState);
		launchState.UpdateSettings(settings); // Failed to start launcher state
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

	[Export]
	private BounceState bounceState;
	public bool IsBouncing { get; set; }
	public void StartBounce(bool isUpwardBounce = true)
	{
		IsBouncing = true;
		bounceState.IsUpwardBounce = isUpwardBounce;
		StateMachine.ChangeState(bounceState);
	}

	[Export]
	private DriftState driftState;
	public bool IsDrifting => driftState.Trigger != null;
	public void StartDrift(DriftTrigger trigger)
	{
		driftState.Trigger = trigger;
		StateMachine.ChangeState(driftState);
	}

	[Signal]
	public delegate void KnockbackEventHandler();
	[Export]
	private KnockbackState knockbackState;
	public void StartKnockback(KnockbackSettings settings = new())
	{
		EmitSignal(SignalName.Knockback); // Emit signal FIRST so external controllers can be alerted

		if (IsInvincible && !settings.ignoreInvincibility) return;
		knockbackState.Settings = settings;
		StateMachine.ChangeState(knockbackState);
	}

	public void TakeDamage()
	{
		if (!Stage.IsLevelIngame) return;

		AllowLandingSkills = false; // Disable landing skills
									// REFACTOR TODO SetActionState(ActionStates.Damaged);

		// No rings; Respawn
		if (Stage.CurrentRingCount == 0)
		{
			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.PearlRespawn) && Skills.IsSoulGaugeCharged)
			{
				// Lose soul power and continue
				Skills.ModifySoulGauge(-CharacterSkillManager.MinimumSoulPower);
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

	public bool IsInvincible => invincibilityTimer != 0 || DisableDamage; // REFACTOR TODO ActionState == ActionStates.Teleport;
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
	#endregion
}