using Godot;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for handling the player's state, physics and basic movement.
	/// </summary>
	public partial class CharacterController : CharacterBody3D
	{
		public static CharacterController instance;
		public StageSettings Stage => StageSettings.instance;

		public override void _EnterTree() => instance = this; // Always Override Singleton

		public override void _Ready()
		{
			CallDeferred(MethodName.ResetOrientation); // Start with proper orientation

			Camera.Initialize();
			Skills.Initialize();

			Path3D startingPath = Stage.CalculateStartingPath(GlobalPosition);
			PathFollower.SetActivePath(startingPath); // Attempt to autoload the stage's default path
			Camera.PathFollower.SetActivePath(startingPath);

			GetParent<Triggers.CheckpointTrigger>().Activate(); // Save initial checkpoint
			Stage.UpdateRingCount(Skills.StartingRingCount, StageSettings.MathModeEnum.Replace); // Start with the proper ring count
			Stage.Connect(StageSettings.SignalName.LevelCompleted, new Callable(this, MethodName.OnLevelCompleted));
			Stage.Connect(StageSettings.SignalName.LevelDemoStarted, new Callable(this, MethodName.OnLevelDemoStarted));

			SnapToGround();
		}

		public override void _PhysicsProcess(double _)
		{
			// Still loading
			if (Stage.LevelState == StageSettings.LevelStateEnum.Loading) return;

			UpdateStateMachine();
			UpdateOrientation();
			UpdatePhysics();

			Animator.UpdateAnimation();
			Skills.UpdateSoulSkills();
		}

		#region State Machine
		public MovementStates MovementState { get; private set; }
		public enum MovementStates
		{
			Normal, // Standard on rails movement
			External, // Cutscenes, and stage objects that override player control
			Launcher, // Springs, Ramps, etc.
		}

		public void ResetMovementState()
		{
			switch (MovementState)
			{
				case MovementStates.External:
					StopExternal();
					break;
			}

			canLandingBoost = false; // Disable landing boost temporarily
			MovementState = MovementStates.Normal;
			Skills.IsSpeedBreakEnabled = Skills.IsTimeBreakEnabled = true; // Reenable soul skills
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

		public ActionStates ActionState { get; private set; }
		public enum ActionStates // Actions that can happen in the Normal MovementState
		{
			Normal,
			Jumping,
			AccelJump,
			Crouching,
			Sliding,
			Damaged, // Being knocked back by damage
			Teleport, // Includes respawning as well
			JumpDash, // Includes homing attack
			Stomping, // Includes jump cancel
			Backflip,
			Grindstep,
		}

		public void ResetActionState() => SetActionState(ActionStates.Normal);
		private void SetActionState(ActionStates newState)
		{
			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Sliding)
			{
				ChangeHitbox("RESET");
				Animator.StopCrouching();
			}
			else if (ActionState == ActionStates.JumpDash) // Stop trail VFX
			{
				Effect.StopSpinFX();
				Effect.StopTrailFX();
				Animator.ResetState();
			}
			else if (ActionState == ActionStates.Stomping)
			{
				ChangeHitbox("RESET");
				AttackState = AttackStates.None;
				Effect.StopStompFX();
			}
			else if (ActionState == ActionStates.Grindstep)
			{
				StopGrindstep();
			}

			ActionState = newState;

			if (ActionState != ActionStates.Damaged)
				Animator.StopHurt();
		}

		private void UpdateStateMachine()
		{
			if (IsCountdownActive)
			{
				UpdateCountdown();
				return;
			}

			if (ActionState == ActionStates.Teleport) return;

			UpdateInputs();

			isCustomPhysicsEnabled = false;
			switch (MovementState)
			{
				case MovementStates.Normal:
					UpdateNormalState();
					break;
				case MovementStates.External:
					isCustomPhysicsEnabled = true; // Allow custom physics during external control
					break;
				case MovementStates.Launcher:
					UpdateLauncher();
					break;
			}

			UpdateInvincibility();
			UpdateLockoutTimer();
		}
		#endregion

		#region Controls
		public Vector2 InputVector => Input.GetVector("move_left", "move_right", "move_up", "move_down", SaveManager.Config.deadZone);
		public float InputHorizontal => Input.GetAxis("move_left", "move_right");
		public float InputVertical => Input.GetAxis("move_up", "move_down");
		private bool isAxisTapped; // Was the left stick tapped?

		/// <summary> Is the player holding in the specified direction? </summary>
		public bool IsHoldingDirection(float refAngle, bool allowNullInputs = default)
		{
			if (!allowNullInputs && InputVector.IsZeroApprox())
				return false;

			float delta = ExtensionMethods.DeltaAngleRad(GetInputAngle(), refAngle);
			return delta < Mathf.Pi * .45f;
		}

		private float GetStrafeAngle()
		{
			float targetAngle = GetInputAngle(true);
			return targetAngle + (PathFollower.DeltaAngle * .5f);
		}

		/// <summary> Returns the input angle based on the camera view. </summary>
		public float GetInputAngle(bool strafeMode = false)
		{
			if (Skills.IsSkillEquipped(SkillKey.Autorun) || strafeMode)
			{
				float baseAngle = InputVector.Y <= SaveManager.Config.deadZone ? PathFollower.ForwardAngle : PathFollower.BackAngle;
				float strafeAngle = InputVector.X * Mathf.Pi * .25f;
				if (IsMovingBackward)
					strafeAngle *= -1;
				if (ExtensionMethods.DotAngle(Camera.XformAngle, PathFollower.ForwardAngle) < 0)
					strafeAngle *= -1;

				return baseAngle - strafeAngle;
			}

			if (InputVector.IsZeroApprox()) // Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(InputVector.AngleTo(Vector2.Up)); // Target rotation angle (in radians)
		}

		/// <summary>
		/// Calculates the target movement angle based on GetInputAngle();
		/// </summary>
		private float GetTargetMovementAngle()
		{
			if (Skills.IsSpeedBreakCharging)
				return PathFollower.ForwardAngle;

			if (IsLockoutActive && ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free)
			{
				if (ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)
					return GetStrafeAngle();

				float targetAngle = Mathf.DegToRad(ActiveLockoutData.movementAngle);
				if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.Camera)
					targetAngle = Camera.TransformAngle(targetAngle);
				else if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.PathFollower)
					targetAngle = PathFollower.ForwardAngle + targetAngle + PathFollower.DeltaAngle;
				else if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.Local)
					targetAngle += MovementAngle;

				// Check if we're trying to turn around
				if (!Skills.IsSpeedBreakActive && ActiveLockoutData.allowReversing)
				{
					if (turnInstantly)
						IsMovingBackward = IsHoldingDirection(PathFollower.BackAngle);

					if (IsMovingBackward)
						targetAngle += Mathf.Pi; // Flip targetAngle when moving backwards
				}

				return targetAngle;
			}

			if (Skills.IsSpeedBreakActive)
				return GetStrafeAngle();

			return GetInputAngle();
		}

		private float jumpBufferTimer;
		private float actionBufferTimer;
		private const float ACTION_BUFFER_LENGTH = .2f; // How long to allow actions to be buffered
		private const float JUMP_BUFFER_LENGTH = .1f; // How long to allow jumps to be buffered
		private void UpdateInputs()
		{
			if (MovementState == MovementStates.External) // Ignore inputs
			{
				jumpBufferTimer = 0;
				actionBufferTimer = 0;
				return;
			}

			if (IsDefeated) return;
			if (IsLockoutActive && ActiveLockoutData.disableActions) return;

			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Input.IsActionJustPressed("button_action"))
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Input.IsActionJustPressed("button_jump"))
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private float lockoutTimer;
		public LockoutResource ActiveLockoutData { get; private set; }
		public bool IsLockoutActive => ActiveLockoutData != null;
		private readonly List<LockoutResource> lockoutDataList = new();

		/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
		public void AddLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource))
			{
				lockoutDataList.Add(resource); // Add the new lockout data
				if (lockoutDataList.Count >= 2) // List only needs to be sorted if there are multiple elements on it
					lockoutDataList.Sort(new LockoutResource.Comparer());

				if (ActiveLockoutData != null && ActiveLockoutData.priority == -1) // Remove current lockout?
					RemoveLockoutData(ActiveLockoutData);

				if (resource.priority == -1) // Exclude from priority, take over immediately
					SetLockoutData(resource);
				else
					ProcessCurrentLockoutData();
			}
			else if (ActiveLockoutData == resource) // Reset lockout timer
				lockoutTimer = 0;
		}

		/// <summary>
		/// Removes a ControlLockoutResource from the list
		/// </summary>
		public void RemoveLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource)) return;
			lockoutDataList.Remove(resource);
			ProcessCurrentLockoutData();
		}

		/// <summary>
		/// Recalculates what the active lockout data is. Called whenever the lockout list is modified.
		/// </summary>
		private void ProcessCurrentLockoutData()
		{
			if (IsLockoutActive && lockoutDataList.Count == 0) // Disable lockout
				SetLockoutData(null);
			else if (ActiveLockoutData != lockoutDataList[lockoutDataList.Count - 1]) // Change to current data (Highest priority, last on the list)
				SetLockoutData(lockoutDataList[lockoutDataList.Count - 1]);
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
		/// <summary> Recenters the player. Only call this AFTER movement has occurred. </summary>
		private void UpdateRecenter()
		{
			if (!IsLockoutActive || !ActiveLockoutData.recenterPlayer) return;

			Vector3 recenterDirection = PathFollower.Forward().Rotated(UpDirection, Mathf.Pi * .5f);
			float currentOffset = PathFollower.LocalPlayerPositionDelta.X;
			float movementOffset = currentOffset;
			if (!isRecentered) // Smooth out recenter speed
			{
				float inputInfluence = ExtensionMethods.DotAngle(PathFollower.ForwardAngle + (Mathf.Pi * .5f), GetInputAngle());
				inputInfluence *= Mathf.Sign(PathFollower.LocalPlayerPositionDelta.X);
				inputInfluence = (inputInfluence + 1) * 0.5f;
				inputInfluence = Mathf.Lerp(MinRecenterPower, MaxRecenterPower, inputInfluence);

				float recenterSpeed = Mathf.Abs(MoveSpeed) * inputInfluence;
				movementOffset = Mathf.MoveToward(movementOffset, 0, recenterSpeed * PhysicsManager.physicsDelta);
				if (Mathf.IsZeroApprox(movementOffset))
					isRecentered = true;
				movementOffset = currentOffset - movementOffset;
			}

			GlobalPosition += movementOffset * recenterDirection; // Move towards the pathfollower
		}
		#endregion

		#region External Control, Automation and Events
		[Signal]
		public delegate void ExternalControlStartedEventHandler();
		[Signal]
		public delegate void ExternalControlCompletedEventHandler();

		/// <summary> Reference to the external object currently controlling the player </summary>
		public Node ExternalController { get; private set; }
		public Node3D ExternalParent { get; private set; }
		private Vector3 externalOffset;
		private float externalSmoothing;

		/// <summary> Used during homing attacks and whenever external objects are overridding physics. </summary>
		private bool isCustomPhysicsEnabled;

		public void StartExternal(Node controller, Node3D followObject = null, float smoothing = 0f, bool allowSpeedBreak = false)
		{
			ExternalController = controller;

			ResetActionState();
			ResetMovementState();
			MovementState = MovementStates.External;

			Skills.IsSpeedBreakEnabled = allowSpeedBreak;

			ExternalParent = followObject;
			externalOffset = Vector3.Zero; // Reset offset
			externalSmoothing = smoothing;
			if (ExternalParent != null && !Mathf.IsZeroApprox(smoothing)) // Smooth out transition
				externalOffset = GlobalPosition - ExternalParent.GlobalPosition;

			ResetVelocity();
			UpdateExternalControl();

			EmitSignal(SignalName.ExternalControlStarted);
		}

		public void StopExternal()
		{
			MovementState = MovementStates.Normal; // Needs to be set to normal BEFORE orientation is reset
			ExternalParent = null;

			UpdateOrientation();
			EmitSignal(SignalName.ExternalControlCompleted);
		}

		/// <summary>
		/// Must be called after external controller has been processed. Pauses physics then calls ApplyExternalTransform.
		/// </summary>
		public void UpdateExternalControl(bool autoResync = false)
		{
			CheckGround(); // Check ground even when externally controlled

			isCustomPhysicsEnabled = true;
			externalOffset = externalOffset.Lerp(Vector3.Zero, externalSmoothing); // Smooth out entry

			CallDeferred(MethodName.ApplyExternalTransform, autoResync);
		}

		/// <summary>
		/// Moves the player to externalParent. Called in deferred mode to ensure things are synced properly.
		/// </summary>
		public void ApplyExternalTransform(bool autoResync)
		{
			if (ExternalParent != null)
			{
				if (ExternalParent is BoneAttachment3D) // Ensure BoneAttachments are updated
					(ExternalParent as BoneAttachment3D).OnBonePoseUpdate((ExternalParent as BoneAttachment3D).BoneIdx);

				GlobalTransform = ExternalParent.GlobalTransform;
			}

			GlobalPosition += externalOffset;

			if (autoResync)
				PathFollower.Resync();
			else
				PathFollower.RecalculateData();
		}
		#endregion
		#endregion

		#region Normal State
		private void UpdateNormalState()
		{
			if (ActionState == ActionStates.Damaged) // Damage action overrides all other states
			{
				UpdateDamage();
				return;
			}

			UpdateMoveSpeed();
			UpdateTurning();
			IsMovingBackward = ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.BackAngle) < Mathf.Pi * .4f; // Moving backwards

			UpdateSlopeSpd();
			UpdateActions();
		}

		public MovementSetting GroundSettings => Skills.GroundSettings;
		public MovementSetting AirSettings => Skills.AirSettings;
		public MovementSetting BackstepSettings => Skills.BackstepSettings;

		[Export]
		/// <summary> Determines how speed is lost when turning. </summary>
		public Curve turningSpeedCurve;
		[Export]
		/// <summary> Determines how input lengths are calculated. </summary>
		private Curve inputCurve;
		private float turningVelocity;

		/// <summary> Is the player moving backwards? </summary>
		public bool IsMovingBackward { get; set; }

		/// <summary> How much speed to lose when turning sharply. </summary>
		private const float TURNING_SPEED_LOSS = .02f;
		/// <summary> How quickly to turn when moving slowly. </summary>
		private const float MIN_TURN_AMOUNT = .12f;
		/// <summary> How quickly to turn when moving at top speed. </summary>
		private const float MAX_TURN_AMOUNT = .6f;
		/// <summary> How quickly to turnaround when at top speed. </summary>
		private const float RUN_TURNAROUND_SPEED = .24f;
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_TURNAROUND_ANGLE = Mathf.Pi * .75f;
		/// <summary> Updates MoveSpeed. What else do you need know? </summary>
		private void UpdateMoveSpeed()
		{
			turnInstantly = Mathf.IsZeroApprox(MoveSpeed) && !Skills.IsSpeedBreakActive; // Store this for turning function

			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Sliding || ActionState == ActionStates.Backflip) return;
			if (Animator.IsCrouchingActive)
			{
				if (Mathf.IsZeroApprox(InputVector.LengthSquared()) && turnInstantly) return;
				Animator.CrouchToMoveTransition();
			}

			// Override to speedbreak speed
			if (Skills.IsSpeedBreakActive)
			{
				if (Skills.IsSpeedBreakOverrideActive)
					MoveSpeed = ActiveMovementSettings.Interpolate(Skills.speedBreakSpeed, 1.0f);
				return;
			}

			float inputAngle = GetInputAngle();
			float inputLength = inputCurve.Sample(InputVector.Length()); // Limits top speed; Modified depending on the LockoutResource.directionOverrideMode

			float targetMovementAngle = GetTargetMovementAngle();
			float inputDot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, targetMovementAngle));

			if (IsLockoutActive)
			{
				if (ActiveLockoutData.overrideSpeed)
				{
					MoveSpeed = ActiveLockoutData.ApplySpeed(MoveSpeed, ActiveMovementSettings);
					return;
				}

				if (ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace)
				{
					if (!InputVector.IsZeroApprox() && inputDot < .2f) // Fixes player holding perpendicular to target direction
						inputLength = 0;
				}
				else if (ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)
				{
					MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, inputLength);
					return;
				}
			}

			if (Skills.IsSkillEquipped(SkillKey.Autorun)) // Always move at full power when autorun is enabled
				inputLength = 1;

			if (Mathf.IsZeroApprox(inputLength) && !Animator.IsBrakeAnimationActive) // Basic slow down
			{
				MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, 0);
			}
			else
			{
				float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);
				bool isTurningAround = deltaAngle > MAX_TURNAROUND_ANGLE || Input.IsActionPressed("button_brake");
				if (isTurningAround || Animator.IsBrakeAnimationActive) // Skid to a stop
				{
					MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, -1);
					Animator.StartBrake();
				}
				else
				{
					if (IsLockoutActive && ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.PathFollower) // Zipper exception
						inputLength *= Mathf.Clamp(inputDot + .5f, 0, 1f); // Arbitrary math to make it easier to maintain speed
					else if (inputDot < .8f) // Slow down while turning
						inputLength *= inputDot;

					if (MoveSpeed < BackstepSettings.Speed) // Accelerate faster when at low speeds
						MoveSpeed = Mathf.Lerp(MoveSpeed, ActiveMovementSettings.Speed * ActiveMovementSettings.GetSpeedRatio(BackstepSettings.Speed), .05f * inputLength);

					if (ActionState == ActionStates.AccelJump)
						MoveSpeed = GroundSettings.Interpolate(MoveSpeed, inputLength);
					else if (ActionState == ActionStates.JumpDash)
						MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, AirSettings.Friction * PhysicsManager.physicsDelta);
					else
						MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, inputLength); // Accelerate based on input strength/input direction
				}
			}

			if (MoveSpeed < 0) // Don't allow negative movespeed
			{
				MoveSpeed = Mathf.Abs(MoveSpeed);
				IsMovingBackward = !IsMovingBackward;
				turnInstantly = true;
			}
		}

		/// <summary> True when the player's MoveSpeed was zero during the previous frame. </summary>
		private bool turnInstantly;
		/// <summary> Amount to blend between free and replace modes. </summary>
		private float strafeBlend;
		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			if (ActionState == ActionStates.Backflip ||
				ActionState == ActionStates.Stomping ||
				ActionState == ActionStates.Crouching ||
				ActionState == ActionStates.Sliding)
			{
				return; // Exit early during certain actions
			}

			if (Mathf.IsZeroApprox(MoveSpeed) && Input.IsActionPressed("button_brake"))
				return;

			float pathControlAmount = PathFollower.DeltaAngle * Camera.ActiveSettings.pathControlInfluence;
			bool isPathDeltaLockoutActive = IsLockoutActive &&
				(ActiveLockoutData.spaceMode != LockoutResource.SpaceModes.Camera ||
				ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe); // Ignore path delta under certain lockout situations

			if (Skills.IsSpeedBreakActive || Skills.IsSkillEquipped(SkillKey.Autorun) || isPathDeltaLockoutActive)
				pathControlAmount = 0; // Don't use path influence during speedbreak/autorun

			float targetMovementAngle = GetTargetMovementAngle() + pathControlAmount;
			if (IsLockoutActive &&
				ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace) // Direction is being overridden
			{
				MovementAngle = targetMovementAngle;
			}

			if (ActionState == ActionStates.AccelJump)
				MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .1f);

			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			if (!turnInstantly && deltaAngle > MAX_TURNAROUND_ANGLE) // Check for turning around
			{
				if (!IsLockoutActive || ActiveLockoutData.movementMode != LockoutResource.MovementModes.Strafe)
					return;
			}

			if (turnInstantly) // Instantly set movement angle to target movement angle
			{
				turningVelocity = 0;
				MovementAngle = targetMovementAngle;
				return;
			}

			float speedRatio = GroundSettings.GetSpeedRatioClamped(MoveSpeed);
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, PathFollower.ForwardAngle);
			// Reduce sensitivity when player is running
			if (speedRatio > CharacterAnimator.RunRatio)
			{
				if (Runtime.Instance.IsUsingController &&
					IsHoldingDirection(PathFollower.ForwardAngle + pathControlAmount) &&
					!Skills.IsSkillEquipped(SkillKey.Autorun)) // Remap controls to provide more analog detail
				{
					targetMovementAngle -= inputDeltaAngle * .5f;
				}

				targetMovementAngle = ExtensionMethods.ClampAngleRange(targetMovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .25f);
			}

			// Normal turning
			float maxTurnAmount = MAX_TURN_AMOUNT;
			float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			// Is the player trying to recenter themselves?
			bool isTurningAround = IsHoldingDirection(PathFollower.ForwardAngle) && (Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle));
			if (isTurningAround)
				maxTurnAmount = RUN_TURNAROUND_SPEED;

			float turnSmoothing = Mathf.Lerp(MIN_TURN_AMOUNT, maxTurnAmount, speedRatio);

			if (IsSpeedLossActive())
			{
				// Calculate turn delta, relative to ground speed
				float speedLossRatio = speedRatio * deltaAngle / MAX_TURNAROUND_ANGLE;
				MoveSpeed -= GroundSettings.Speed * turningSpeedCurve.Sample(speedLossRatio) * TURNING_SPEED_LOSS;
				if (MoveSpeed < 0)
					MoveSpeed = 0;
			}

			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle + pathControlAmount, targetMovementAngle, ref turningVelocity, turnSmoothing);

			// Strafe implementation
			if (Skills.IsSpeedBreakActive ||
				Skills.IsSkillEquipped(SkillKey.Autorun) ||
				(IsLockoutActive && ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe))
			{
				if (InputVector.IsZeroApprox())
					strafeBlend = Mathf.MoveToward(strafeBlend, 1.0f, PhysicsManager.physicsDelta);
				else
					strafeBlend = 0;

				if (!isPathDeltaLockoutActive)
					MovementAngle += PathFollower.DeltaAngle;
				MovementAngle = Mathf.LerpAngle(MovementAngle, targetMovementAngle, strafeBlend);
			}
		}

		/// <summary> Returns true when speed loss should be applied. </summary>
		private bool IsSpeedLossActive()
		{
			// Speedbreak is overriding speed
			if (Skills.IsSpeedBreakActive) return false;

			// Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
			if (IsHoldingDirection(PathFollower.ForwardAngle, true) && GroundSettings.GetSpeedRatio(MoveSpeed) > .5f)
				return false;

			// Or when overriding speed/direction
			if (IsLockoutActive &&
			(ActiveLockoutData.overrideSpeed || ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free))
			{
				return false;
			}

			return true;
		}

		private MovementSetting ActiveMovementSettings
		{
			get
			{
				if (!IsOnGround)
					return AirSettings;
				return IsMovingBackward ? BackstepSettings : GroundSettings;
			}
		}

		/// <summary> How much should the steepest slope affect the player? </summary>
		private const float SLOPE_INFLUENCE_STRENGTH = .2f;
		/// <summary> Slopes that are shallower than Mathf.PI * threshold are ignored. </summary>
		private const float SLOPE_THRESHOLD = .2f;
		private void UpdateSlopeSpd()
		{
			if (Mathf.IsZeroApprox(MoveSpeed) || IsMovingBackward) return; // Idle/Backstepping isn't affected by slopes
			if (!IsOnGround) return; // Slope is too shallow or not on the ground
			if (IsLockoutActive && ActiveLockoutData.ignoreSlopes) return; // Lockout is ignoring slopes

			// Calculate slope influence
			float slopeInfluenceRatio = PathFollower.Forward().Dot(Vector3.Up);
			if (Mathf.Abs(slopeInfluenceRatio) <= SLOPE_THRESHOLD) return;

			slopeInfluenceRatio = Mathf.Lerp(-SLOPE_INFLUENCE_STRENGTH, SLOPE_INFLUENCE_STRENGTH, slopeInfluenceRatio);
			if (slopeInfluenceRatio > 0 && Skills.IsSkillEquipped(SkillKey.AllRounder)) // Cancel slope influence when moving upwards
				slopeInfluenceRatio = 0;

			if (IsHoldingDirection(PathFollower.ForwardAngle)) // Accelerating
			{
				if (slopeInfluenceRatio < 0f) // Downhill
					MoveSpeed += GroundSettings.Traction * Mathf.Abs(slopeInfluenceRatio) * PhysicsManager.physicsDelta; // Uncapped
				else if (GroundSettings.GetSpeedRatioClamped(MoveSpeed) < 1f) // Uphill; Reduce acceleration (Only when not at top speed)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.Traction * slopeInfluenceRatio * PhysicsManager.physicsDelta);
			}
			else if (MoveSpeed > 0f) // Decceleration (Only applied when actually moving)
			{
				if (slopeInfluenceRatio < 0f) // Re-apply some speed when moving downhill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, GroundSettings.Speed, GroundSettings.Friction * Mathf.Abs(slopeInfluenceRatio) * PhysicsManager.physicsDelta);
				else // Increase friction when moving uphill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.Friction * slopeInfluenceRatio * PhysicsManager.physicsDelta);
			}
		}

		#region Actions
		private void UpdateActions()
		{
			if (IsOnGround)
				UpdateGroundActions();
			else
				UpdateAirActions();
		}

		private void UpdateGroundActions()
		{
			if (IsLockoutActive) // Controls locked out
			{
				if (ActiveLockoutData.resetFlags.HasFlag(LockoutResource.ResetFlags.OnLand) && JustLandedOnGround) // Cancel lockout
					RemoveLockoutData(ActiveLockoutData);
				else if (ActiveLockoutData.disableActions)
					return;
			}

			if (Skills.IsSpeedBreakActive) return;

			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Sliding)
				UpdateCrouching();
			else if (actionBufferTimer != 0)
			{
				StartCrouching();
				actionBufferTimer = 0;
			}

			if (jumpBufferTimer != 0)
			{
				jumpBufferTimer = 0;

				if (IsHoldingDirection(PathFollower.BackAngle) && (!IsLockoutActive ||
				ActiveLockoutData.movementMode == LockoutResource.MovementModes.Free || ActiveLockoutData.allowReversing))
					StartBackflip();
				else
					Jump();

				if (IsLockoutActive && ActiveLockoutData.resetFlags.HasFlag(LockoutResource.ResetFlags.OnJump))
					RemoveLockoutData(ActiveLockoutData);
			}
		}

		private void UpdateAirActions()
		{
			switch (ActionState)
			{
				case ActionStates.Stomping:
					UpdateStomp();
					return;
				case ActionStates.Jumping:
					UpdateJump();
					break;
				case ActionStates.JumpDash:
					UpdateJumpDash();
					return; // Jumpdashing applies custom gravity
				case ActionStates.Backflip:
					UpdateBackflip();
					break;

				default: // Normal air actions
					if (Lockon.IsBounceLockoutActive)
					{
						Lockon.UpdateBounce();

						if (!Lockon.CanInterruptBounce)
							return;
					}

					CheckStomp();
					break;
			}

			CheckJumpDash();
			ApplyGravity(); // Always apply gravity when in the air
		}

		private void ApplyGravity()
		{
			if (Lockon.IsBounceLockoutActive) return; // Don't apply gravity when bouncing!

			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, Runtime.MAX_GRAVITY, Runtime.GRAVITY * PhysicsManager.physicsDelta);
		}

		private bool canLandingBoost;
		private void CheckLandingBoost()
		{
			if (!canLandingBoost) return;
			canLandingBoost = false; // Reset landing boost

			if (MovementState != MovementStates.Normal) return;

			// Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
			if (IsHoldingDirection(PathFollower.ForwardAngle))
			{
				Effect.StartWind();
				MovementAngle = PathFollower.ForwardAngle;
				MoveSpeed = Mathf.Max(MoveSpeed, Skills.landingDashSpeed);
			}
		}

		#region Jump
		[Export]
		public float jumpHeight;
		[Export]
		public float jumpCurve = .95f;
		public bool IsJumpClamped { get; private set; } // True after the player releases the jump button
		private bool isAccelerationJumpQueued;
		private float currentJumpTime; // Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .1f; // How fast the jump button needs to be released for an "acceleration jump"
		public void Jump(bool ignoreAccelerationJump = default)
		{
			currentJumpTime = ignoreAccelerationJump ? ACCELERATION_JUMP_LENGTH + PhysicsManager.physicsDelta : 0;
			IsJumpClamped = false;
			IsOnGround = false;
			CanJumpDash = true;
			canLandingBoost = Skills.IsSkillEquipped(SkillKey.LandDash);
			SetActionState(ActionStates.Jumping);
			VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight);

			if (IsMovingBackward || MoveSpeed < 0) // Kill speed when jumping backwards
				MoveSpeed = 0;

			Effect.PlayActionSFX(Effect.JumpSfx);
			Animator.JumpAnimation();
		}

		private void UpdateJump()
		{
			if (isAccelerationJumpQueued &&
				currentJumpTime >= ACCELERATION_JUMP_LENGTH) // Acceleration jump?
			{
				if (IsHoldingDirection(PathFollower.ForwardAngle, true) &&
				InputVector.Length() > .5f)
				{
					StartAccelJump();
				}

				isAccelerationJumpQueued = false;
			}

			if (!IsJumpClamped)
			{
				if (!Input.IsActionPressed("button_jump"))
				{
					IsJumpClamped = true;
					if (currentJumpTime <= ACCELERATION_JUMP_LENGTH) // Listen for acceleration jump
						isAccelerationJumpQueued = true;
				}
			}
			else if (VerticalSpeed > 0f)
			{
				VerticalSpeed *= jumpCurve; // Kill jump height
			}

			currentJumpTime += PhysicsManager.physicsDelta;
			CheckStomp();
		}

		private void StartAccelJump()
		{
			SetActionState(ActionStates.AccelJump);
			MoveSpeed = Skills.accelerationJumpSpeed;
			Animator.JumpAccelAnimation();
			VerticalSpeed = 5f; // Consistant accel jump height
			isAccelerationJumpQueued = false; // Stop listening for an acceleration jump
		}
		#endregion

		#region Jump Dash & Homing Attack
		[Export]
		public float jumpDashSpeed;
		[Export]
		public float jumpDashPower;
		[Export]
		public float jumpDashGravity;
		[Export]
		public float jumpDashMaxGravity;
		public bool CanJumpDash
		{
			get => canJumpDash;
			set
			{
				canJumpDash = value;
				Lockon.IsMonitoring = value;
			}
		}
		private bool canJumpDash;

		private void CheckJumpDash()
		{
			if (Mathf.IsZeroApprox(jumpBufferTimer)) return;

			if (CanJumpDash)
			{
				StartJumpDash();
				jumpBufferTimer = 0;
			}
		}

		private void StartJumpDash()
		{
			// Backflipping or facing backwards - Jumpdash directly forward
			if (ActionState == ActionStates.Backflip || ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.BackAngle) < Mathf.Pi * .2f)
				MovementAngle = PathFollower.ForwardAngle;
			else // Force MovementAngle to face forward
				MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .5f);

			Effect.PlayActionSFX(Effect.JumpDashSfx);
			Effect.StartTrailFX();

			CanJumpDash = false;
			IsMovingBackward = false; // Can't jumpdash backwards!
			SetActionState(ActionStates.JumpDash);

			if (Lockon.IsBounceLockoutActive) // Interrupt lockout
				RemoveLockoutData(Lockon.bounceLockoutSettings);

			if (Lockon.Target == null || !Lockon.IsTargetAttackable) // Normal jumpdash
			{
				MoveSpeed = jumpDashSpeed;
				VerticalSpeed = jumpDashPower;
				Animator.LaunchAnimation();
			}
			else
			{
				Lockon.StartHomingAttack(); // Start Homing attack
				Animator.StartSpin(2.0f);
				Effect.StartSpinFX();
				ChangeHitbox("spin");
				UpdateJumpDash();
			}
		}

		private void UpdateJumpDash()
		{
			if (Lockon.IsHomingAttacking) // Homing attack
			{
				if (Lockon.Target == null) // Target disappeared. Transition to jumpdash
				{
					MovementAngle = PathFollower.ForwardAngle;
					Lockon.StopHomingAttack();
					StartJumpDash();
					return;
				}

				isCustomPhysicsEnabled = true;
				VerticalSpeed = 0;
				if (Lockon.IsPerfectHomingAttack)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, Skills.perfectHomingAttackSpeed, Skills.homingAttackAcceleration * 2.0f * PhysicsManager.physicsDelta);
				else
					MoveSpeed = Mathf.MoveToward(MoveSpeed, Skills.homingAttackSpeed, Skills.homingAttackAcceleration * PhysicsManager.physicsDelta);
				Velocity = Lockon.HomingAttackDirection.Normalized() * MoveSpeed;
				MovementAngle = ExtensionMethods.CalculateForwardAngle(Lockon.HomingAttackDirection);
				MoveAndSlide();
				UpdateUpDirection();
				PathFollower.Resync();
			}
			else // Normal Jump dash; Apply gravity
			{
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);
			}

			CheckStomp();
		}
		#endregion

		#region Crouch & Slide
		/// <summary> How much can the player adjust their angle while sliding? </summary>
		private const float MAX_SLIDE_ADJUSTMENT = Mathf.Pi * .4f;
		private void StartCrouching()
		{
			if (!IsOnWall && !IsMovingBackward && MoveSpeed != 0)
			{
				if (MoveSpeed <= Skills.SlideSettings.Speed)
					MoveSpeed = Skills.SlideSettings.Speed;

				Effect.PlayActionSFX(Effect.SlideSfx);
				SetActionState(ActionStates.Sliding);
				ChangeHitbox("slide");
			}
			else
			{
				SetActionState(ActionStates.Crouching);
				ChangeHitbox("crouch");
			}

			Animator.StartCrouching();
		}

		private void UpdateCrouching()
		{
			if (MoveSpeed <= 0)
			{
				MoveSpeed = 0;

				if (ActionState == ActionStates.Sliding)
				{
					ActionState = ActionStates.Crouching;
					Animator.ToggleSliding();
					ChangeHitbox("crouch");
				}
			}
			else
			{
				if (ActionState == ActionStates.Sliding)
				{
					// Influence sliding direction slightly
					if (!IsHoldingDirection(PathFollower.BackAngle))
					{
						float targetMovementAngle = ExtensionMethods.ClampAngleRange(GetTargetMovementAngle(), PathFollower.ForwardAngle, MAX_SLIDE_ADJUSTMENT);
						MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, MIN_TURN_AMOUNT);
					}

					// Influence speed
					if (IsHoldingDirection(PathFollower.ForwardAngle))
						MoveSpeed = Skills.SlideSettings.Interpolate(MoveSpeed, -(1 - InputVector.Length()));
					else
						MoveSpeed = Skills.SlideSettings.Interpolate(MoveSpeed, -InputVector.Length());
				}
				else if (ActionState == ActionStates.Crouching)
				{
					MoveSpeed *= .5f;
				}
			}

			if (!Input.IsActionPressed("button_action") && !Animator.IsSlideTransitionActive)
				ResetActionState();
		}
		#endregion

		#region Stomp
		/// <summary> How fast to fall when stomping </summary>
		private const int STOMP_SPEED = -32;
		/// <summary> How much gravity to add each frame. </summary>
		private const int JUMP_CANCEL_GRAVITY = 180;
		/// <summary> How much gravity to add each frame. </summary>
		private const int STOMP_GRAVITY = 540;
		private void UpdateStomp()
		{
			MoveSpeed = 0; // Go STRAIGHT down

			if (Skills.IsSkillEquipped(SkillKey.StompAttack))
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, STOMP_SPEED, STOMP_GRAVITY * PhysicsManager.physicsDelta);
			else
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, STOMP_SPEED, JUMP_CANCEL_GRAVITY * PhysicsManager.physicsDelta);
		}

		private void CheckStomp()
		{
			if (Mathf.IsZeroApprox(actionBufferTimer)) return;

			// Don't allow instant stomps
			if ((ActionState == ActionStates.Jumping || ActionState == ActionStates.AccelJump) &&
				currentJumpTime < .1f)
			{
				return;
			}

			if (ActionState == ActionStates.Grindstep)
				Animator.ResetState(.1f);

			// Stomp
			actionBufferTimer = 0;
			MoveSpeed = 0; // Kill horizontal speed

			canLandingBoost = Skills.IsSkillEquipped(SkillKey.StompDash);

			Lockon.IsMonitoring = false;
			if (Lockon.IsHomingAttacking)
				Lockon.StopHomingAttack();
			SetActionState(ActionStates.Stomping);

			bool attackStomp = Skills.IsSkillEquipped(SkillKey.StompAttack);
			if (attackStomp)
			{
				AttackState = AttackStates.Weak;
				ChangeHitbox("stomp");
			}
			Animator.StompAnimation(attackStomp);
		}
		#endregion

		#region Backflip
		[Export]
		public float backflipHeight;
		/// <summary> How much can the player adjust their angle while backflipping? </summary>
		private const float MAX_BACKFLIP_ADJUSTMENT = Mathf.Pi * .25f;
		/// <summary> How much to turn when backflipping </summary>
		private const float BACKFLIP_TURN_SPEED = .25f;
		private void StartBackflip()
		{
			CanJumpDash = true;
			MoveSpeed = Skills.BackflipSettings.Speed;

			IsMovingBackward = true;
			MovementAngle = GetInputAngle();

			VerticalSpeed = Runtime.CalculateJumpPower(backflipHeight);

			IsOnGround = false;
			SetActionState(ActionStates.Backflip);

			Effect.PlayActionSFX(Effect.JumpSfx);
			Animator.BackflipAnimation();
		}

		private void UpdateBackflip()
		{
			if (!IsHoldingDirection(PathFollower.ForwardAngle)) // Influence backflip direction slightly
			{
				float targetMovementAngle = ExtensionMethods.ClampAngleRange(GetTargetMovementAngle(), PathFollower.BackAngle, MAX_BACKFLIP_ADJUSTMENT);
				MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, BACKFLIP_TURN_SPEED);

				if (IsHoldingDirection(PathFollower.BackAngle))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, InputVector.Length());
				else if (Mathf.IsZeroApprox(InputVector.Length()))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, 0);
			}
			else
			{
				MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, -1);
			}

			if (IsOnGround)
				ResetActionState();
		}
		#endregion

		#region GrindStep
		/// <summary> Will the player get a grindstep bonus when landing on a rail? </summary>
		public bool IsGrindstepBonusActive { get; set; }
		/// <summary> How high to jump during a grindstep. </summary>
		private readonly float GRIND_STEP_HEIGHT = 1.6f;
		/// <summary> How fast to move during a grindstep. </summary>
		private readonly float GRIND_STEP_SPEED = 28.0f;
		public void StartGrindstep()
		{
			// Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(GetInputAngle(), MovementAngle);
			// Calculate how far player is trying to go
			float horizontalTarget = GRIND_STEP_SPEED * Mathf.Sign(inputDeltaAngle);
			horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, inputCurve.Sample(InputVector.Length())); // Give some smoothing based on controller strength

			// Keep some speed forward
			turningVelocity = 0;
			MovementAngle += Mathf.Pi * .25f * Mathf.Sign(inputDeltaAngle);
			VerticalSpeed = Runtime.CalculateJumpPower(GRIND_STEP_HEIGHT);
			MoveSpeed = new Vector2(horizontalTarget, MoveSpeed).Length();
			turnInstantly = true;
			IsGrindstepBonusActive = true;

			CanJumpDash = false; // Disable jumpdashing
			SetActionState(ActionStates.Grindstep);
			Effect.PlayActionSFX(Effect.JumpSfx);
			Animator.StartGrindStep();
		}

		private void StopGrindstep()
		{
			MovementAngle = Animator.VisualAngle;
			Animator.ResetState(.1f);
		}
		#endregion
		#endregion

		#endregion

		#region Damage & Invincibility
		public bool IsInvincible => invincibilityTimer != 0;
		private float invincibilityTimer;
		private const float INVINCIBILITY_LENGTH = 5f;

		public void StartInvincibility()
		{
			invincibilityTimer = INVINCIBILITY_LENGTH;
			Animator.StartInvincibility();
		}

		private void UpdateInvincibility()
		{
			if (IsInvincible)
				invincibilityTimer = Mathf.MoveToward(invincibilityTimer, 0, PhysicsManager.physicsDelta);
		}

		private const float DAMAGE_FRICTION = 20f;
		private void UpdateDamage()
		{
			if (Skills.IsSkillEquipped(SkillKey.DownCancel) && jumpBufferTimer != 0)
			{
				ResetActionState(); // End damage
				Jump();
				jumpBufferTimer = 0;
				StartAccelJump(); // Immediately switch to an accel jump
				return;
			}

			if (!previousKnockbackSettings.stayOnGround && IsOnGround)
			{
				ResetActionState();
				return;
			}

			VerticalSpeed -= Runtime.GRAVITY * PhysicsManager.physicsDelta;
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, DAMAGE_FRICTION * PhysicsManager.physicsDelta);
		}

		[Signal]
		public delegate void KnockbackEventHandler(); // This signal is called anytime a hitbox collides with the player, regardless of invincibilty.
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
		private KnockbackSettings previousKnockbackSettings;

		/// <summary>
		/// Called when the player takes damage or is being knocked around.
		/// </summary>
		public void StartKnockback(KnockbackSettings knockbackSettings = new())
		{
			EmitSignal(SignalName.Knockback); // Emit signal FIRST so external controllers can be alerted
			if (IsInvincible && !knockbackSettings.ignoreInvincibility) return;

			if (Lockon.IsHomingAttacking)
				Lockon.StopHomingAttack();

			if (Skills.IsSpeedBreakActive) // Disable speedbreak
				Skills.ToggleSpeedBreak();

			MovementAngle = PathFollower.ForwardAngle; // Prevent being knocked sideways

			if (MovementState == MovementStates.Normal || knockbackSettings.ignoreMovementState)
			{
				Animator.StartHurt();
				Animator.ResetState();
				previousKnockbackSettings = knockbackSettings;

				MoveSpeed = knockbackSettings.overrideKnockbackSpeed ? knockbackSettings.knockbackSpeed : 8f;
				if (!knockbackSettings.knockForward)
					MoveSpeed *= -1;

				if (!knockbackSettings.stayOnGround)
				{
					IsOnGround = false;
					VerticalSpeed = Runtime.CalculateJumpPower(knockbackSettings.overrideKnockbackHeight ? knockbackSettings.knockbackHeight : 1);
				}
			}

			if (MovementState == MovementStates.External) return; // Only allow autorespawning when not using external controller

			if (!knockbackSettings.disableDamage)
				SetActionState(ActionStates.Damaged);

			// Apply invincibility and drop rings
			if (!IsInvincible)
			{
				StartInvincibility();

				if (!knockbackSettings.disableDamage)
					TakeDamage();
			}
		}

		/// <summary>
		/// Removes 20 rings from the player, or begins a respawn when no rings are left.
		/// </summary>
		public void TakeDamage()
		{
			if (!Stage.IsLevelIngame) return;

			SetActionState(ActionStates.Damaged);

			// No rings; Respawn
			if (Stage.CurrentRingCount == 0)
			{
				Effect.PlayVoice("defeat");
				StartRespawn();
				return;
			}

			Effect.PlayVoice("hurt");

			// Lose soul power
			Skills.ModifySoulGauge(-Mathf.FloorToInt(Skills.SoulPower * .5f));

			// Lose rings
			Stage.UpdateRingCount(Skills.IsSkillEquipped(SkillKey.RingDamage) ? 10 : 20, StageSettings.MathModeEnum.Subtract);
			Stage.IncrementDamageCount();

			// Level failed
			if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Perfect)
			{
				DefeatPlayer();
				Stage.FinishLevel(false);
			}
		}

		/// <summary> True while the player is defeated but hasn't respawned yet. </summary>
		public bool IsDefeated { get; private set; }
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
		}

		/// <summary> Called when the player is returning to a checkpoint. </summary>
		public void StartRespawn(bool debugRespawn = false)
		{
			if (ActionState == ActionStates.Teleport || IsDefeated) return;

			DefeatPlayer();

			if (!debugRespawn)
			{
				// Level failed
				if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Deathless
					|| Stage.Data.MissionType == LevelDataResource.MissionTypes.Perfect)
				{
					Stage.FinishLevel(false);
					return;
				}
			}

			// Fade screen out and connect signals
			TransitionManager.StartTransition(new()
			{
				inSpeed = .5f,
				outSpeed = .5f,
				color = Colors.Black // Use Colors.Transparent for debugging
			});

			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.ProcessRespawn), (uint)ConnectFlags.OneShot);
		}

		/// <summary>
		/// Warp the player to the previous checkpoint and revert any actions.
		/// </summary>
		private void ProcessRespawn()
		{
			ResetActionState();
			MovementState = MovementStates.Normal;

			invincibilityTimer = 0;
			Teleport(Stage.CurrentCheckpoint);
			BonusManager.instance.CancelBonuses();
			Stage.RevertToCheckpointData();
			PathFollower.SetActivePath(Stage.CurrentCheckpoint.PlayerPath); // Revert path
			Camera.PathFollower.SetActivePath(Stage.CurrentCheckpoint.CameraPath);

			IsDefeated = false;
			IsMovingBackward = false;
			ResetVelocity();

			// Clear any collision exceptions
			foreach (Node exception in GetCollisionExceptions())
				RemoveCollisionExceptionWith(exception);

			// Wait a single physics frame to ensure objects update properly
			GetTree().CreateTimer(PhysicsManager.physicsDelta, false, true).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.FinishRespawn));
		}

		/// <summary>
		/// Final step of the respawn process. Re-enable area collider and finish transition.
		/// </summary>
		private void FinishRespawn()
		{
			PathFollower.Resync();
			Camera.Respawn();

			ResetOrientation();

			SnapToGround();
			ChangeHitbox("RESET");

			Stage.RespawnObjects();
			Stage.IncrementRespawnCount();
			Stage.UpdateRingCount(Skills.RespawnRingCount, StageSettings.MathModeEnum.Replace, true); // Reset ring count

			TransitionManager.FinishTransition();
		}

		private const float TELEPORT_START_FX_LENGTH = .2f;
		private const float TELEPORT_END_FX_LENGTH = .5f;
		/// <summary>
		/// Teleports the player to a specific location. Use TeleportSettings to have more control of how teleport occurs.
		/// </summary>
		public async void Teleport(Triggers.TeleportTrigger trigger)
		{
			SetActionState(ActionStates.Teleport);

			if (trigger.resetMovespeed)
				ResetVelocity();

			if (trigger.enableStartFX)
			{
				Animator.StartTeleport();
				await ToSignal(GetTree().CreateTimer(TELEPORT_START_FX_LENGTH, false), SceneTreeTimer.SignalName.Timeout);
			}

			if (trigger.crossfade)
				Camera.StartCrossfade();

			await ToSignal(GetTree().CreateTimer(PhysicsManager.physicsDelta, false), SceneTreeTimer.SignalName.Timeout);
			GlobalPosition = trigger.WarpPosition;
			SnapToGround();

			trigger.ApplyTeleport(); // Apply any signals/path changes
			MovementAngle = PathFollower.ForwardAngle;
			Animator.SnapRotation(PathFollower.ForwardAngle);

			if (trigger.enableEndFX)
			{
				Animator.StopTeleport();
				await ToSignal(GetTree().CreateTimer(TELEPORT_END_FX_LENGTH, false), SceneTreeTimer.SignalName.Timeout);
			}

			ResetActionState();
		}

		/// <summary>
		/// Attempts to snap the player to the ground and sets IsOnGround to true.
		/// </summary>
		private void SnapToGround()
		{
			KinematicCollision3D collision = MoveAndCollide(-UpDirection * 100.0f, true);
			if (collision == null) return;

			GlobalPosition = collision.GetPosition();
			Animator.SnapToGround();
			LandOnGround();
		}
		#endregion

		#region Launchers and Jumps
		[Signal]
		public delegate void LaunchFinishedEventHandler();

		private float launcherTime;
		public LaunchSettings LaunchSettings { get; private set; }
		private Objects.Launcher activeLauncher;
		public void StartLauncher(LaunchSettings data, Objects.Launcher newLauncher = null)
		{
			if (MovementState == MovementStates.Launcher)
			{
				if (activeLauncher != null && activeLauncher == newLauncher)
					return; // Already launching that!

				FinishLauncher();
			}

			ResetMovementState();
			ResetActionState();
			MovementState = MovementStates.Launcher;

			activeLauncher = newLauncher;
			LaunchSettings = data;

			ResetVelocity();

			IsOnGround = false;
			launcherTime = 0;

			CanJumpDash = data.AllowJumpDash;
			Lockon.IsMonitoring = false; // Disable lockon monitoring while launch is active
			Lockon.StopHomingAttack();
			AttackState = AttackStates.OneShot; // Launchers always oneshot all enemies

			if (data.UseAutoAlign)
			{
				Vector3 launchDirection = LaunchSettings.InitialVelocity.RemoveVertical();
				if (!launchDirection.IsEqualApprox(Vector3.Zero))
				{
					MovementAngle = ExtensionMethods.CalculateForwardAngle(launchDirection);
					Animator.SnapRotation(MovementAngle);
				}
			}

			if (data.IsJump) // Play jump effects
			{
				Animator.JumpAnimation();
				UpDirection = Vector3.Up;
				Effect.PlayActionSFX(Effect.JumpSfx);
			}
		}

		private void UpdateLauncher()
		{
			isCustomPhysicsEnabled = true;

			if (activeLauncher?.IsCharacterCentered == false)
			{
				GlobalPosition = activeLauncher.RecenterCharacter();
			}
			else
			{
				Vector3 targetPosition = LaunchSettings.InterpolatePositionTime(launcherTime);
				float heightDelta = targetPosition.Y - GlobalPosition.Y;
				RaycastHit hit = this.CastRay(GlobalPosition, targetPosition - GlobalPosition, Runtime.Instance.environmentMask);
				if (hit && hit.collidedObject.IsInGroup("wall"))
				{
					FinishLauncher();
					return;
				}

				GlobalPosition = targetPosition;
				VerticalSpeed = heightDelta;

				if (heightDelta < 0) // Only check ground when falling
					CheckGround();

				if (IsOnGround || LaunchSettings.IsLauncherFinished(launcherTime)) // Revert to normal state
				{
					FinishLauncher();
					IsMovingBackward = false;
					MoveSpeed = LaunchSettings.HorizontalVelocity * .5f; // Prevent too much movement
					VerticalSpeed = LaunchSettings.FinalVerticalVelocity;
				}

				launcherTime += PhysicsManager.physicsDelta;
			}

			PathFollower.Resync();
		}

		private void FinishLauncher()
		{
			if (activeLauncher != null)
			{
				activeLauncher.Deactivate();

				if (!IsOnGround)
					CanJumpDash = activeLauncher.allowJumpDashing;
				activeLauncher = null;
			}

			Effect.StopSpinFX();
			Effect.StopTrailFX();
			Animator.ResetState();
			ResetMovementState();

			AttackState = AttackStates.None;
			Lockon.IsMonitoring = CanJumpDash;

			EmitSignal(SignalName.LaunchFinished);
		}
		#endregion

		#region Physics
		/// <summary> Size to use for collision checks. </summary>
		[Export]
		public Vector2 CollisionSize;
		/// <summary> Collision shape used for triggering objects. </summary>
		[Export]
		private AnimationPlayer hitboxAnimator;
		public void ChangeHitbox(StringName hitboxAnimation)
		{
			hitboxAnimator.Play(hitboxAnimation);
			hitboxAnimator.Advance(0);
		}

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

		/// <summary> Character's primary movement speed. </summary>
		public float MoveSpeed { get; set; }
		/// <summary> Used for jumping and falling. </summary>
		public float VerticalSpeed { get; set; }
		/// <summary> Resets all speed values to zero. </summary>
		private void ResetVelocity() => MoveSpeed = VerticalSpeed = 0;

		public Vector3 TrueVelocity { get; private set; }

		private void UpdatePhysics()
		{
			Lockon.UpdateLockonTargets();
			if (isCustomPhysicsEnabled) return; // When physics are handled in the state machine

			Vector3 movementDirection = GetMovementDirection();

			Velocity = movementDirection * MoveSpeed;
			Velocity += UpDirection * VerticalSpeed;

			// Store the current position to calculate true velocity later
			TrueVelocity = GlobalPosition;

			MoveAndSlide();
			CheckMainWall(movementDirection);

			// Collision checks
			CheckGround();
			CheckCeiling();

			// Calculate true velocity after physics were processed.
			TrueVelocity -= GlobalPosition;
			if (IsOnGround && IsOnWall() && Mathf.IsZeroApprox(TrueVelocity.LengthSquared()))
				MoveSpeed = 0;

			PathFollower.Resync(); // Resync
			UpdateRecenter();
		}

		public new bool IsOnWall { get; set; }

		public bool IsOnGround { get; set; }
		public bool JustLandedOnGround { get; private set; } // Flag for doing stuff on land
		private RaycastHit groundHit;

		private const int GROUND_CHECK_AMOUNT = 8; // How many "whiskers" to use when checking the ground
		private void CheckGround()
		{
			if (JustLandedOnGround) // RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CollisionPosition;
			float castLength = CollisionSize.Y + (CollisionPadding * 2.0f);
			if (IsOnGround)
				castLength += Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta; // Attempt to remain stuck to the ground when moving quickly
			else if (VerticalSpeed < 0)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;

			Vector3 checkOffset = Vector3.Zero;
			groundHit = new();
			Vector3 castVector = this.Down() * castLength;
			int raysHit = 0;

			// Whisker casts (For smoother collision)
			float interval = Mathf.Tau / GROUND_CHECK_AMOUNT;
			Vector3 castOffset = this.Forward() * ((CollisionSize.Y * .5f) - CollisionPadding);
			for (int i = 0; i < GROUND_CHECK_AMOUNT; i++)
			{
				castOffset = castOffset.Rotated(this.Down(), interval);
				RaycastHit hit = this.CastRay(castOrigin + castOffset, castVector, CollisionMask, false, GetCollisionExceptions());
				DebugManager.DrawRay(castOrigin + castOffset, castVector, hit ? Colors.Red : Colors.White);
				if (ValidateGroundCast(ref hit))
				{
					if (!groundHit)
						groundHit = hit;
					else
						groundHit.Add(hit);
					checkOffset += castOffset;
					raysHit++;
				}
			}

			if (MovementState == MovementStates.External) // Exit early when externally controlled
			{
				if (groundHit)
					Effect.UpdateGroundType(groundHit.collidedObject);
				return;
			}

			if (groundHit) // Successful ground hit
			{
				Effect.UpdateGroundType(groundHit.collidedObject);
				groundHit.Divide(raysHit);

				if (!IsOnGround && VerticalSpeed < 0) // Landing on the ground
				{
					UpDirection = groundHit.normal;
					UpdateOrientation();
					LandOnGround();
				}
				else
				{
					if (JustLandedOnGround)
						IsGrindstepBonusActive = false;

					float snapDistance = groundHit.distance - CollisionSize.Y;
					GlobalPosition -= UpDirection * snapDistance; // Remain snapped to the ground
					UpDirection = UpDirection.Lerp(groundHit.normal, .2f + (.4f * GroundSettings.GetSpeedRatio(MoveSpeed))).Normalized(); // Update world direction
				}
			}
			else
			{
				if (IsOnGround) // Leave ground
				{
					IsOnGround = false;
					Animator.IsFallTransitionEnabled = true;
				}

				UpdateUpDirection();
			}
		}

		private void UpdateUpDirection()
		{
			// Calculate target up direction
			Vector3 targetUpDirection = Vector3.Up;
			if (Camera.ActiveSettings.followPathTilt) // Use PathFollower.Up when on a tilted path.
				targetUpDirection = PathFollower.Up();
			else if (ActionState == ActionStates.Backflip)
				targetUpDirection = PathFollower.HeightAxis;

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
				orientationResetFactor = VerticalSpeed * .2f / Runtime.MAX_GRAVITY;
			}

			UpDirection = UpDirection.Lerp(targetUpDirection, Mathf.Clamp(orientationResetFactor, 0f, 1f)).Normalized();
		}

		public void LandOnGround()
		{
			// Snap to ground
			Vector3 originalVelocity = Velocity;
			Velocity = UpDirection * VerticalSpeed;
			MoveAndSlide();
			Velocity = originalVelocity;

			IsOnGround = true;
			VerticalSpeed = 0;

			IsJumpClamped = false;
			CanJumpDash = false;

			isAccelerationJumpQueued = false;
			Lockon.ResetLockonTarget();

			if (IsCountdownActive) return;
			if (IsDefeated && ActionState == ActionStates.Teleport) return; // Return early when respawning

			ResetActionState();
			JustLandedOnGround = true;

			if (!Stage.IsLevelIngame) return; // Return early when not ingame
			CheckLandingBoost(); // Landing boost skill

			// Play FX
			Effect.PlayLandingFX();
		}

		/// <summary> Checks whether raycast collider is tagged properly. </summary>
		private bool ValidateGroundCast(ref RaycastHit hit)
		{
			if (hit)
			{
				if (!hit.collidedObject.IsInGroup("floor") ||
					(MovementState != MovementStates.External && hit.normal.AngleTo(UpDirection) > Mathf.Pi * .4f)) // Limit angle collision
				{
					hit = new RaycastHit();
				}
				else if (!IsOnGround &&
						hit.collidedObject.IsInGroup("wall") &&
						hit.normal.AngleTo(Vector3.Up) > Mathf.Pi * .2f) // Use Vector3.Up for objects tagged as a wall
				{
					hit = new RaycastHit();
				}
			}

			return hit;
		}

		private bool ValidateWallCast(ref RaycastHit hit)
		{
			if (hit && !hit.collidedObject.IsInGroup("wall"))
				hit = new RaycastHit();

			return hit;
		}

		public void CheckCeiling() // Checks the ceiling.
		{
			// Start check slightly BELOW the floor to ensure object detection
			Vector3 castOrigin = GlobalPosition - (UpDirection * CollisionPadding);
			float castLength = (CollisionSize.Y + CollisionPadding) * 2.0f;
			if (VerticalSpeed > 0)
				castLength += VerticalSpeed * PhysicsManager.physicsDelta;

			Vector3 castVector = UpDirection * castLength;
			if (ActionState == ActionStates.Backflip) // Improve collision detection when backflipping
				castVector += GetMovementDirection() * MoveSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				if (ceilingHit.collidedObject.IsInGroup("crusher") && groundHit) // Check if the player is being crushed
				{
					GD.Print($"Crushed by {ceilingHit.collidedObject.Name}");
					AddCollisionExceptionWith(ceilingHit.collidedObject); // Avoid clipping through the ground
					StartKnockback(new()
					{
						ignoreInvincibility = true,
					});

					return;
				}

				if (!ceilingHit.collidedObject.IsInGroup("ceiling")) return;

				GlobalTranslate(ceilingHit.point - (CollisionPosition + (UpDirection * CollisionSize.Y)));

				float maxVerticalSpeed = 0;
				// Workaround for backflipping into slanted ceilings
				if (ActionState == ActionStates.Backflip)
				{
					float ceilingAngle = ceilingHit.normal.AngleTo(Vector3.Down);

					if (ceilingAngle > Mathf.Pi * .1f) // Only slanted ceilings need this workaround
					{
						float deltaAngle = ExtensionMethods.DeltaAngleRad(PathFollower.ForwardAngle, ExtensionMethods.CalculateForwardAngle(ceilingHit.normal, IsOnGround ? PathFollower.Up() : Vector3.Up));
						if (deltaAngle > Mathf.Pi * .1f) // Wall isn't aligned to the path
							return;

						// Slide down the wall if it's aligned with the path direction
						maxVerticalSpeed = -Mathf.Sin(ceilingAngle) * MoveSpeed;
					}
				}

				if (VerticalSpeed > maxVerticalSpeed)
					VerticalSpeed = maxVerticalSpeed;
			}
		}

		// Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			IsOnWall = false;
			if (Mathf.IsZeroApprox(MoveSpeed)) // No movement
			{
				DebugManager.DrawRay(CollisionPosition, castVector * CollisionSize.X, Colors.White);
				return;
			}

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = CollisionSize.X + CollisionPadding + (Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta);

			RaycastHit wallHit = this.CastRay(CollisionPosition, castVector * castLength, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(CollisionPosition, castVector * castLength, wallHit ? Colors.Red : Colors.White);

			if (!ValidateWallCast(ref wallHit))
				return;

			if (ActionState == ActionStates.Backflip)
				return;

			float wallDelta = ExtensionMethods.DeltaAngleRad(ExtensionMethods.CalculateForwardAngle(wallHit.normal, IsOnGround ? PathFollower.Up() : Vector3.Up), MovementAngle);
			if (wallDelta >= Mathf.Pi * .75f) // Process wall collision 
			{
				if (ActionState == ActionStates.JumpDash &&
					wallHit.collidedObject.IsInGroup("splash jump") &&
					Skills.IsSkillEquipped(SkillKey.SplashJump))
				{
					// Perform a splash jump
					Lockon.StopHomingAttack();
					VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight * .5f);
					return;
				}

				// Cancel speed break (only when recenter lockout ISN'T active)
				if (Skills.IsSpeedBreakActive && !(IsLockoutActive && ActiveLockoutData.recenterPlayer))
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
				if (ActionState == ActionStates.JumpDash && wallHit.collidedObject.IsInGroup("splash jump"))
				{
					MoveSpeed = 0;
					VerticalSpeed = Mathf.Clamp(VerticalSpeed, -Mathf.Inf, 0);
				}

				// Running into wall head-on
				if (wallDelta >= Mathf.Pi * .9f && wallHit.distance <= CollisionSize.X + CollisionPadding)
				{
					IsOnWall = true;
					MoveSpeed = 0; // Kill speed
					return;
				}
			}

			if (!IsMovingBackward && IsOnGround) // Reduce MoveSpeed when running against walls
			{
				float speedClamp = Mathf.Clamp(1.0f - (wallDelta / Mathf.Pi * .4f), 0f, 1f); // Arbitrary formula that works well
				if (GroundSettings.GetSpeedRatio(MoveSpeed) > speedClamp)
					MoveSpeed *= speedClamp;
			}
		}

		private void ResetOrientation()
		{
			UpDirection = Vector3.Up;

			if (Stage.CurrentCheckpoint == null) // Default to parent node's position
				Transform = Transform3D.Identity;
			else
				GlobalTransform = Stage.CurrentCheckpoint.GlobalTransform;

			MovementAngle = PathFollower.ForwardAngle; // Reset movement angle
			Animator.SnapRotation(MovementAngle);
		}

		/// <summary> Orientates Root to world direction, then rotates the gimbal on the y-axis </summary>
		public void UpdateOrientation(bool allowExternalUpdate = false)
		{
			if (!allowExternalUpdate && MovementState == MovementStates.External) return; // Externally controlled

			// Untested! This may end up breaking in certain scenarios
			GlobalRotation = Vector3.Zero;
			Vector3 cross = Vector3.Left.Rotated(Vector3.Up, UpDirection.Flatten().AngleTo(Vector2.Down));
			GlobalRotate(cross, -UpDirection.SignedAngleTo(Vector3.Up, cross));
		}
		#endregion

		/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs </summary>
		public float MovementAngle { get; set; }
		public Vector3 GetMovementDirection()
		{
			float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			return PathFollower.Forward().Rotated(UpDirection, deltaAngle);
		}

		#region Signals
		private bool IsCountdownActive => Interface.Countdown.IsCountdownActive;

		private float countdownBoostTimer;
		private readonly float COUNTDOWN_BOOST_WINDOW = .4f;

		public void OnCountdownStarted()
		{
			PathFollower.Resync();
			MovementAngle = PathFollower.ForwardAngle;

			Animator.SnapRotation(PathFollower.ForwardAngle);
			Animator.PlayCountdown();
		}

		private void UpdateCountdown()
		{
			if (Input.IsActionJustPressed("button_action"))
				actionBufferTimer = 1f;
			actionBufferTimer -= PhysicsManager.physicsDelta;
		}

		public void OnCountdownFinished()
		{
			if (Skills.IsSkillEquipped(SkillKey.RocketStart) && actionBufferTimer > 0 && actionBufferTimer < COUNTDOWN_BOOST_WINDOW) // Successful starting boost
			{
				Effect.StartWind();
				MoveSpeed = Skills.countdownBoostSpeed;
				AddLockoutData(new LockoutResource()
				{
					length = .5f,
					overrideSpeed = true,
					speedRatio = Skills.countdownBoostSpeed,
					resetFlags = LockoutResource.ResetFlags.OnJump,
				});
			}

			Animator.CancelOneshot();

			// Snap camera to gameplay
			Camera.SnapXform();
			Camera.SnapFlag = true;
			actionBufferTimer = 0; // Reset action buffer from starting boost
		}

		private void OnLevelCompleted()
		{
			if (ActionState != ActionStates.Damaged)
				ResetActionState();

			// Disable everything
			Lockon.IsMonitoring = false;
			Skills.DisableBreakSkills();

			if (Stage.LevelState == StageSettings.LevelStateEnum.Failed || Stage.Data.CompletionLockout == null)
				AddLockoutData(Runtime.Instance.StopLockout);
			else
				AddLockoutData(Stage.Data.CompletionLockout);
		}

		private void OnLevelDemoStarted()
		{
			MoveSpeed = 0;
			AddLockoutData(Runtime.Instance.StopLockout);
		}

		public void OnObjectCollisionExit(Node3D body)
		{
			if (body is not PhysicsBody3D) return;

			if (GetCollisionExceptions().Contains(body as PhysicsBody3D))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
			}
		}
		#endregion

		// Components, rarely needs to be edited, so they go at the bottom of the inspector
		// All public so any object can get whatever player data they need
		[Export]
		public CameraController Camera { get; private set; }
		[Export]
		public CharacterPathFollower PathFollower { get; private set; }
		[Export]
		public CharacterAnimator Animator { get; private set; }
		[Export]
		public CharacterEffect Effect { get; private set; }
		[Export]
		public CharacterSkillManager Skills { get; private set; }
		[Export]
		public CharacterLockon Lockon { get; private set; }
	}
}
