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

		public StageSettings Level => StageSettings.instance;
		public StageSettings Stage => StageSettings.instance;

		public override void _EnterTree() => instance = this; //Always Override Singleton

		public override void _Ready()
		{
			CallDeferred(MethodName.ResetOrientation); //Start with proper orientation

			Camera.Initialize();
			Skills.Initialize();

			Path3D startingPath = Stage.CalculateStartingPath(GlobalPosition);
			PathFollower.SetActivePath(startingPath); //Attempt to autoload the stage's default path
			Camera.PathFollower.SetActivePath(startingPath);

			Level.SetCheckpoint(GetParent<Triggers.CheckpointTrigger>()); //Initial checkpoint configuration
			Level.UpdateRingCount(Skills.StartingRingCount, StageSettings.MathModeEnum.Replace); //Start with the proper ring count
			Level.Connect(StageSettings.SignalName.LevelCompleted, new Callable(this, MethodName.OnLevelCompleted));
			Level.Connect(StageSettings.SignalName.LevelDemoStarted, new Callable(this, MethodName.OnLevelDemoStarted));
		}

		public override void _PhysicsProcess(double _)
		{
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
			Normal, //Standard on rails movement
			External, //Cutscenes, and stage objects that override player control
			Launcher, //Springs, Ramps, etc.
		}

		public void ResetMovementState()
		{
			switch (MovementState)
			{
				case MovementStates.External:
					StopExternal();
					break;
			}

			canLandingBoost = false; //Disable landing boost temporarily
			MovementState = MovementStates.Normal;
			Skills.IsSpeedBreakEnabled = Skills.IsTimeBreakEnabled = true; //Reenable soul skills
		}

		public ActionStates ActionState { get; private set; }
		public enum ActionStates //Actions that can happen in the Normal MovementState
		{
			Normal,
			Jumping,
			AccelJump,
			Crouching,
			Sliding,
			Damaged, //Being knocked back by damage
			Respawning, //Idle until respawn timer reaches zero
			JumpDash, //Also includes homing attack
			Stomping, //Jump cancel
			Backflip,
			Grindstep,
		}

		public void ResetActionState() => SetActionState(ActionStates.Normal);
		private void SetActionState(ActionStates newState)
		{
			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Sliding)
				StopCrouching();
			else if (ActionState == ActionStates.JumpDash) // Stop trail VFX
				Effect.StopTrailFX();
			else if (ActionState == ActionStates.Grindstep)
				StopGrindstep();

			ActionState = newState;
		}

		private void UpdateStateMachine()
		{
			if (IsCountdownActive)
			{
				UpdateCountdown();
				return;
			}

			if (IsRespawning) return;

			UpdateInputs();

			isCustomPhysicsEnabled = false;
			switch (MovementState)
			{
				case MovementStates.Normal:
					UpdateNormalState();
					break;
				case MovementStates.External:
					isCustomPhysicsEnabled = true; //Allow custom physics during external control
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
		public Vector2 InputVector => Input.GetVector("move_left", "move_right", "move_up", "move_down");
		public float InputHorizontal => Input.GetAxis("move_left", "move_right");
		public float InputVertical => Input.GetAxis("move_up", "move_down");
		private bool isAxisTapped; //Was the left stick tapped?

		/// <summary> Is the player holding in the specified direction? </summary>
		public bool IsHoldingDirection(float refAngle, bool allowNullInputs = default)
		{
			if (!allowNullInputs && InputVector.IsZeroApprox())
				return false;

			float delta = ExtensionMethods.DeltaAngleRad(GetInputAngle(), refAngle);
			return delta < Mathf.Pi * .4f;
		}

		/// <summary> Returns the input angle based on the camera view. </summary>
		public float GetInputAngle()
		{
			if (InputVector.IsZeroApprox()) // Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(InputVector.AngleTo(Vector2.Up)); // Target rotation angle (in radians)
		}

		/// <summary>
		/// Calculates the target movement angle based on GetInputAngle();
		/// </summary>
		private float GetTargetMovementAngle()
		{
			if (Skills.IsSpeedBreakActive)
			{
				float targetAngle = PathFollower.ForwardAngle + PathFollower.DeltaAngle;
				if (Camera.ActiveSettings.followPathTilt) // Only do this when camera is tilting)
					targetAngle -= PathFollower.DeltaAngle * 1.8f;

				return targetAngle;
			}

			float inputAngle = GetInputAngle();

			if (IsLockoutActive && ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free)
			{
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

			return inputAngle;
		}

		private float jumpBufferTimer;
		private float actionBufferTimer;
		private const float ACTION_BUFFER_LENGTH = .2f; //How long to allow actions to be buffered
		private const float JUMP_BUFFER_LENGTH = .1f; //How long to allow jumps to be buffered
		private void UpdateInputs()
		{
			if (MovementState == MovementStates.External) //Ignore inputs
			{
				jumpBufferTimer = 0;
				actionBufferTimer = 0;
				return;
			}

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
		private readonly List<LockoutResource> lockoutDataList = new List<LockoutResource>();

		/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
		public void AddLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource))
			{
				lockoutDataList.Add(resource); //Add the new lockout data
				if (lockoutDataList.Count >= 2) //List only needs to be sorted if there are multiple elements on it
					lockoutDataList.Sort(new LockoutResource.Comparer());

				if (ActiveLockoutData != null && ActiveLockoutData.priority == -1) //Remove current lockout?
					RemoveLockoutData(ActiveLockoutData);

				if (resource.priority == -1) //Exclude from priority, take over immediately
					SetLockoutData(resource);
				else
					ProcessCurrentLockoutData();
			}
			else if (ActiveLockoutData == resource) //Reset lockout timer
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
			if (IsLockoutActive && lockoutDataList.Count == 0) //Disable lockout
				SetLockoutData(null);
			else if (ActiveLockoutData != lockoutDataList[lockoutDataList.Count - 1]) //Change to current data (Highest priority, last on the list)
				SetLockoutData(lockoutDataList[lockoutDataList.Count - 1]);
		}

		private void SetLockoutData(LockoutResource resource)
		{
			ActiveLockoutData = resource;

			if (resource != null) //Reset flags
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

		private bool isRecentered; //Is the recenter complete?
		private const float RECENTER_POWER = .1f;
		/// <summary> Recenters the player. Only call this AFTER movement has occurred. </summary>
		private void UpdateRecenter()
		{
			if (!IsLockoutActive || !ActiveLockoutData.recenterPlayer) return;

			Vector3 recenterDirection = PathFollower.Back().Rotated(UpDirection, Mathf.Pi * .5f);
			float currentOffset = -PathFollower.FlatPlayerPositionDelta.X;
			float movementOffset = currentOffset;
			if (!isRecentered) //Smooth out recenter speed
			{
				movementOffset = Mathf.MoveToward(movementOffset, 0, Mathf.Abs(MoveSpeed) * RECENTER_POWER * PhysicsManager.physicsDelta);
				if (Mathf.IsZeroApprox(movementOffset))
					isRecentered = true;
				movementOffset = currentOffset - movementOffset;
			}

			GlobalPosition += movementOffset * recenterDirection; //Move towards the pathfollower
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
			externalOffset = Vector3.Zero; //Reset offset
			externalSmoothing = smoothing;
			if (ExternalParent != null && !Mathf.IsZeroApprox(smoothing)) //Smooth out transition
				externalOffset = GlobalPosition - ExternalParent.GlobalPosition;

			ResetVelocity();
			UpdateExternalControl();

			EmitSignal(SignalName.ExternalControlStarted);
		}

		public void StopExternal()
		{
			MovementState = MovementStates.Normal; //Needs to be set to normal BEFORE orientation is reset
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
			if (ActionState == ActionStates.Damaged) //Damage action overrides all other states
			{
				UpdateDamage();
				return;
			}

			UpdateMoveSpeed();
			UpdateTurning();
			IsMovingBackward = ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.BackAngle) < Mathf.Pi * .4f; //Moving backwards

			UpdateSlopeSpd();
			UpdateActions();
		}

		public MovementResource GroundSettings => Skills.GroundSettings;
		public MovementResource AirSettings => Skills.AirSettings;
		public MovementResource BackstepSettings => Skills.BackstepSettings;

		[Export]
		public Curve turningSpeedCurve; //Curve of how speed is lost when turning
		private float turningVelocity;

		/// <summary> Is the player moving backwards? </summary>
		public bool IsMovingBackward { get; set; }

		/// <summary> How much speed to lose when turning sharply. </summary>
		private const float TURNING_SPEED_LOSS = .04f;
		/// <summary> How much to smooth turning when moving slowly. </summary>
		private const float MIN_TURN_AMOUNT = .12f;
		/// <summary> How much to smooth turning when moving at top speed. </summary>
		private const float MAX_TURN_AMOUNT = .4f;
		/// <summary> How much to smooth turning when speed break is active. </summary>
		private const float MAX_SPEED_BREAK_TURN_AMOUNT = .2f;
		/// <summary> How quickly to turnaround when at top speed. </summary>
		private const float STRAFE_TURNAROUND_SPEED = .24f;
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_TURNAROUND_ANGLE = Mathf.Pi * .75f;
		/// <summary> Updates MoveSpeed. What else do you need know? </summary>
		private void UpdateMoveSpeed()
		{
			turnInstantly = Mathf.IsZeroApprox(MoveSpeed); // Store this for turning function

			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Sliding || ActionState == ActionStates.Backflip) return;

			// Override to speedbreak speed
			if (Skills.IsSpeedBreakActive)
			{
				if (Skills.IsSpeedBreakOverrideActive)
					MoveSpeed = ActiveMovementSettings.Interpolate(Skills.speedBreakSpeed, 1.0f);
				return;
			}

			float inputAngle = GetInputAngle();
			float inputLength = InputVector.Length(); // Limits top speed; Modified depending on the LockoutResource.directionOverrideMode

			float targetMovementAngle = GetTargetMovementAngle();
			float inputDot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, targetMovementAngle));

			if (IsLockoutActive)
			{
				if (ActiveLockoutData.overrideSpeed)
				{
					//Override speed to the correct value
					float targetSpd = ActiveMovementSettings.speed * ActiveLockoutData.speedRatio;
					float delta = PhysicsManager.physicsDelta;
					if (MoveSpeed <= targetSpd) //Accelerate using traction
						delta *= ActiveMovementSettings.traction * ActiveLockoutData.tractionMultiplier;
					else //Slow down with friction
						delta *= ActiveMovementSettings.friction * ActiveLockoutData.frictionMultiplier;

					if (delta < 0) //Snap speed (i.e. Dash Panels)
					{
						MoveSpeed = targetSpd;
						return;
					}

					MoveSpeed = Mathf.MoveToward(MoveSpeed, targetSpd, delta);
					return;
				}


				if (ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free)
				{
					//Fixes player holding perpendicular to target direction
					if (!InputVector.IsZeroApprox() && inputDot < .2f)
						inputLength = 0;
				}
			}

			if (Mathf.IsZeroApprox(inputLength) || InputVector.IsZeroApprox()) // Basic slow down
				MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, 0);
			else
			{
				float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);
				bool isTurningAround = deltaAngle > MAX_TURNAROUND_ANGLE;
				if (isTurningAround) //Skid to a stop
					MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, -1);
				else
				{
					if (IsLockoutActive && ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.PathFollower) //Zipper exception
						inputLength *= Mathf.Clamp(inputDot + .5f, 0, 1f); //Arbitrary math to make it easier to maintain speed
					else if (inputDot < .8f) //Slow down while turning
						inputLength *= inputDot;

					if (IsMovingBackward && !IsOnGround) //Greatly reduce input strength when jumping backwards
						inputLength *= .2f;

					if (MoveSpeed < BackstepSettings.speed) //Accelerate faster when at low speeds
						MoveSpeed = Mathf.Lerp(MoveSpeed, ActiveMovementSettings.speed * ActiveMovementSettings.GetSpeedRatio(BackstepSettings.speed), .05f * inputLength);

					MoveSpeed = ActiveMovementSettings.Interpolate(MoveSpeed, inputLength); //Accelerate based on input strength/input direction
				}
			}

			if (MoveSpeed < 0) // Don't allow negative movespeed
			{
				MoveSpeed = Mathf.Abs(MoveSpeed);
				IsMovingBackward = !IsMovingBackward;
				MovementAngle += Mathf.Pi;
			}
		}

		/// <summary> True when the player's MoveSpeed was zero during the previous frame. </summary>
		private bool turnInstantly;
		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			if (ActionState == ActionStates.Backflip || ActionState == ActionStates.Stomping) return;
			if (ActionState == ActionStates.Crouching || MoveSpeed == 0) return;

			float targetMovementAngle = GetTargetMovementAngle();
			bool overrideFacingDirection = (IsLockoutActive &&
			(ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace ||
			ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)) || Skills.IsSpeedBreakActive;

			// Strafe implementation
			if (Skills.IsSpeedBreakActive ||
			(IsLockoutActive && ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe))
			{
				//Custom strafing movement
				float strafeAmount = ExtensionMethods.DotAngle(GetInputAngle() + Mathf.Pi * .5f, PathFollower.ForwardAngle);
				strafeAmount *= InputVector.Length(); //Analog inputs

				//Reduce strafe speed when moving slowly
				float strafeFactor = IsOnGround ? GroundSettings.GetSpeedRatioClamped(MoveSpeed) : AirSettings.GetSpeedRatioClamped(MoveSpeed);
				strafeFactor += .1f;
				strafeAmount *= strafeFactor;
				StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, strafeAmount);
			}
			else
				StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, 0); //Reset strafe speed when not in use

			if (overrideFacingDirection) // Direction is being overridden
				MovementAngle = targetMovementAngle;

			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			if (ActionState == ActionStates.Backflip || ActionState == ActionStates.Sliding) return;
			if (!turnInstantly && deltaAngle > MAX_TURNAROUND_ANGLE) return; // Turning around

			if (turnInstantly) // Instantly set movement angle to target movement angle
			{
				turningVelocity = 0;
				MovementAngle = targetMovementAngle;
				return;
			}

			float maxTurnAmount = MAX_TURN_AMOUNT;
			float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, PathFollower.ForwardAngle);
			if (IsHoldingDirection(PathFollower.ForwardAngle) &&
			(Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle)))
				maxTurnAmount = STRAFE_TURNAROUND_SPEED;

			float speedRatio = GroundSettings.GetSpeedRatioClamped(MoveSpeed);
			float turnDelta = Mathf.Lerp(MIN_TURN_AMOUNT, maxTurnAmount, speedRatio);

			if (IsSpeedLossActive())
			{
				// Calculate turn delta, relative to ground speed
				float speedLossRatio = deltaAngle / MAX_TURNAROUND_ANGLE;
				MoveSpeed -= GroundSettings.speed * speedRatio * turningSpeedCurve.Sample(speedLossRatio) * TURNING_SPEED_LOSS;
				if (MoveSpeed < 0)
					MoveSpeed = 0;
			}

			if (Camera.ActiveSettings.followPathTilt) // Only do this when camera is tilting
				MovementAngle += PathFollower.DeltaAngle * 1.08f; // Random number that seems pretty accurate.
			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, turnDelta);
		}


		/// <summary> Returns true when speed loss should be applied. </summary>
		private bool IsSpeedLossActive()
		{
			// Speedbreak is overriding speed
			if (Skills.IsSpeedBreakActive) return false;

			// Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
			if (IsHoldingDirection(PathFollower.ForwardAngle) && GroundSettings.GetSpeedRatio(MoveSpeed) > .5f)
				return false;

			// Or when overriding speed/direction
			if (IsLockoutActive &&
			(ActiveLockoutData.overrideSpeed || ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free))
				return false;

			return true;
		}


		private MovementResource ActiveMovementSettings
		{
			get
			{
				if (!IsOnGround)
					return AirSettings;
				return IsMovingBackward ? BackstepSettings : GroundSettings;
			}
		}


		/// <summary> Current influence of the slope. </summary>
		private float slopeInfluenceRatio;
		/// <summary> How much should the steepest slope affect the player? </summary>
		private const float SLOPE_INFLUENCE_STRENGTH = .4f;
		/// <summary> Slopes that are shallower than Mathf.PI * threshold are ignored. </summary>
		private const float SLOPE_THRESHOLD = .02f;
		/// <summary> Recalculates slope influence based on ground normal. </summary>
		private void UpdateSlopeInfluence(Vector3 groundNormal)
		{
			// Calculate slope influence
			float angle = groundNormal.AngleTo(Vector3.Up);
			if (Mathf.Abs(angle) < Mathf.Pi * SLOPE_THRESHOLD) // Slope is too insignificant to affect movement
			{
				slopeInfluenceRatio = 0; // Reset influence
				return;
			}

			float rotationAmount = GetMovementDirection().SignedAngleTo(Vector3.Forward, Vector3.Up);
			Vector3 slopeDirection = groundNormal.Rotated(Vector3.Up, rotationAmount).Normalized();
			slopeInfluenceRatio = slopeDirection.Z * SLOPE_INFLUENCE_STRENGTH;
		}

		private void UpdateSlopeSpd()
		{
			if (Mathf.IsZeroApprox(MoveSpeed) || IsMovingBackward) return; //Idle/Backstepping isn't affected by slopes
			if (!IsOnGround) return; //Slope is too shallow or not on the ground
			if (IsLockoutActive && ActiveLockoutData.ignoreSlopes) return; //Lockout is ignoring slopes

			if (IsHoldingDirection(PathFollower.ForwardAngle)) //Accelerating
			{
				if (slopeInfluenceRatio < 0f) //Downhill
				{
					//Capped - MoveSpeed = Mathf.MoveToward(MoveSpeed, GroundSettings.speed, GroundSettings.traction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta);
					MoveSpeed += GroundSettings.traction * Mathf.Abs(slopeInfluenceRatio) * PhysicsManager.physicsDelta; //Uncapped
				}
				else if (GroundSettings.GetSpeedRatioClamped(MoveSpeed) < 1f) //Uphill; Reduce acceleration (Only when not at top speed)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.traction * slopeInfluenceRatio * PhysicsManager.physicsDelta);
			}
			else if (MoveSpeed > 0f) //Decceleration (Only applied when actually moving)
			{
				if (slopeInfluenceRatio < 0f) //Re-apply some speed when moving downhill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, GroundSettings.speed, GroundSettings.friction * Mathf.Abs(slopeInfluenceRatio) * PhysicsManager.physicsDelta);
				else //Increase friction when moving uphill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.friction * slopeInfluenceRatio * PhysicsManager.physicsDelta);
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
			if (IsLockoutActive) //Controls locked out
			{
				if (ActiveLockoutData.resetFlags.HasFlag(LockoutResource.ResetFlags.OnLand) && JustLandedOnGround) //Cancel lockout
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
					return; //Jumpdashing applies custom gravity
				case ActionStates.Backflip:
					UpdateBackflip();
					break;

				default: //Normal air actions
					if (Lockon.IsBouncingLockoutActive)
					{
						Lockon.UpdateBounce();

						if (!Lockon.CanInterruptBounce)
							return;
					}

					CheckStomp();
					break;
			}

			CheckJumpDash();
			ApplyGravity(); //Always apply gravity when in the air
		}

		private void ApplyGravity()
		{
			if (Lockon.IsBouncingLockoutActive) return; //Don't apply gravity when bouncing!

			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, Runtime.MAX_GRAVITY, Runtime.GRAVITY * PhysicsManager.physicsDelta);
		}

		private bool canLandingBoost;
		private void CheckLandingBoost()
		{
			if (!canLandingBoost) return;
			canLandingBoost = false; //Reset landing boost

			if (MovementState != MovementStates.Normal) return;

			//Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
			if (IsHoldingDirection(PathFollower.ForwardAngle) && MoveSpeed < Skills.landingDashSpeed)
			{
				MovementAngle = PathFollower.ForwardAngle;
				MoveSpeed = Skills.landingDashSpeed;
			}
		}

		#region Jump
		[Export]
		public float jumpHeight;
		[Export]
		public float jumpCurve = .95f;
		public bool IsJumpClamped { get; private set; } //True after the player releases the jump button
		private bool isAccelerationJumpQueued;
		private float currentJumpTime; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .08f; //How fast the jump button needs to be released for an "acceleration jump"
		public void Jump(bool ignoreAccelerationJump = default)
		{
			currentJumpTime = ignoreAccelerationJump ? ACCELERATION_JUMP_LENGTH + PhysicsManager.physicsDelta : 0;
			IsJumpClamped = false;
			IsOnGround = false;
			CanJumpDash = true;
			canLandingBoost = Skills.IsSkillEnabled(SkillKeyEnum.LandingDash);
			SetActionState(ActionStates.Jumping);
			VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight);

			if (IsMovingBackward || MoveSpeed < 0) //Kill speed when jumping backwards
				MoveSpeed = 0;

			Effect.PlayActionSFX(Effect.JUMP_SFX);
			Animator.JumpAnimation();
		}

		private void UpdateJump()
		{
			if (isAccelerationJumpQueued && currentJumpTime >= ACCELERATION_JUMP_LENGTH) //Acceleration jump?
			{
				if (IsHoldingDirection(PathFollower.ForwardAngle, true) && InputVector.Length() > .5f)
				{
					SetActionState(ActionStates.AccelJump);
					MoveSpeed = Skills.accelerationJumpSpeed;
					Animator.JumpAccelAnimation();
				}

				VerticalSpeed = 5f; //Consistant accel jump height
				isAccelerationJumpQueued = false; //Stop listening for an acceleration jump
			}

			if (!IsJumpClamped)
			{
				if (!Input.IsActionPressed("button_jump"))
				{
					IsJumpClamped = true;
					if (currentJumpTime <= ACCELERATION_JUMP_LENGTH) //Listen for acceleration jump
						isAccelerationJumpQueued = true;
				}
			}
			else if (VerticalSpeed > 0f)
				VerticalSpeed *= jumpCurve; //Kill jump height

			currentJumpTime += PhysicsManager.physicsDelta;
			CheckStomp();
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
			//Backflipping or facing backwards - Jumpdash directly forward
			if (ActionState == ActionStates.Backflip || ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.BackAngle) < Mathf.Pi * .2f)
				MovementAngle = PathFollower.ForwardAngle;
			else //Force MovementAngle to face forward
				MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .5f);

			Animator.StopHurt(); // Cancel hurt animation if necessary
			Effect.PlayActionSFX("jump dash");
			Effect.StartTrailFX();

			CanJumpDash = false;
			IsMovingBackward = false; //Can't jumpdash backwards!
			MoveSpeed = jumpDashSpeed;
			SetActionState(ActionStates.JumpDash);

			if (Lockon.IsBouncingLockoutActive) //Interrupt lockout
				RemoveLockoutData(Lockon.bounceLockoutSettings);

			if (Lockon.Target == null) //Normal jumpdash
			{
				VerticalSpeed = jumpDashPower;
				Animator.LaunchAnimation();
			}
			else
			{
				Lockon.StartHomingAttack(); //Start Homing attack
				Animator.JumpAccelAnimation();
			}
		}

		private void UpdateJumpDash()
		{
			if (Lockon.IsHomingAttacking) //Homing attack
			{
				if (Lockon.Target == null) //Target disappeared. Transition to jumpdash
				{
					MovementAngle = PathFollower.ForwardAngle;
					Lockon.StopHomingAttack();
					StartJumpDash();
					return;
				}

				isCustomPhysicsEnabled = true;
				VerticalSpeed = 0;
				Velocity = Lockon.HomingAttackDirection.Normalized() * Skills.homingAttackSpeed;
				MovementAngle = ExtensionMethods.CalculateForwardAngle(Lockon.HomingAttackDirection);
				MoveAndSlide();

				PathFollower.Resync();
			}
			else //Normal Jump dash; Apply gravity
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);

			CheckStomp();
		}
		#endregion

		#region Crouch & Slide
		/// <summary> How much can the player adjust their angle while sliding? </summary>
		private const float MAX_SLIDE_ADJUSTMENT = Mathf.Pi * .4f;
		private void StartCrouching()
		{
			if (!IsOnWall && ((!IsMovingBackward && MoveSpeed != 0) || MoveSpeed >= Skills.SlideSettings.speed))
			{
				if (MoveSpeed <= Skills.SlideSettings.speed)
					MoveSpeed = Skills.SlideSettings.speed;

				Effect.PlayActionSFX("slide");
				SetActionState(ActionStates.Sliding);
			}
			else
				SetActionState(ActionStates.Crouching);
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
					MoveSpeed *= .5f;
			}

			if (!Input.IsActionPressed("button_action"))
				ResetActionState();
		}

		private void StopCrouching() => Animator.StopCrouching();
		#endregion

		#region Stomp
		/// <summary> How fast to fall when stomping </summary>
		private const int STOMP_SPEED = -32;
		/// <summary> How much gravity to add each frame. </summary>
		private const int STOMP_GRAVITY = 180;
		private void UpdateStomp()
		{
			MoveSpeed = StrafeSpeed = 0; //Go STRAIGHT down
			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, STOMP_SPEED, STOMP_GRAVITY * PhysicsManager.physicsDelta);
		}

		private void CheckStomp()
		{
			if (Mathf.IsZeroApprox(actionBufferTimer)) return;

			//Don't allow instant stomps
			if ((ActionState == ActionStates.Jumping || ActionState == ActionStates.AccelJump) &&
			currentJumpTime < .1f)
				return;

			if (ActionState == ActionStates.Grindstep)
				Animator.ResetState(.1f);

			//Stomp
			actionBufferTimer = 0;
			MoveSpeed = StrafeSpeed = 0; //Kill horizontal speed

			canLandingBoost = true;
			Lockon.ResetLockonTarget();
			Lockon.IsMonitoring = false;
			SetActionState(ActionStates.Stomping);

			//TODO Play a separate stomping animation if using a stomp skill
			Animator.StompAnimation(false);
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
			MoveSpeed = Skills.BackflipSettings.speed;

			IsMovingBackward = true;
			MovementAngle = GetInputAngle();

			VerticalSpeed = Runtime.CalculateJumpPower(backflipHeight);

			IsOnGround = false;
			SetActionState(ActionStates.Backflip);

			Effect.PlayActionSFX(Effect.JUMP_SFX);
			Animator.BackflipAnimation();
		}


		private void UpdateBackflip()
		{
			if (!IsHoldingDirection(PathFollower.ForwardAngle)) //Influence backflip direction slightly
			{
				float targetMovementAngle = ExtensionMethods.ClampAngleRange(GetTargetMovementAngle(), PathFollower.BackAngle, MAX_BACKFLIP_ADJUSTMENT);
				MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, BACKFLIP_TURN_SPEED);

				if (IsHoldingDirection(PathFollower.BackAngle))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, InputVector.Length());
				else if (Mathf.IsZeroApprox(InputVector.Length()))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, 0);
			}
			else
				MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, -1);

			if (IsOnGround)
				ResetActionState();
		}
		#endregion

		#region GrindStep
		/// <summary> How high to jump during a grindstep. </summary>
		private readonly float GRIND_STEP_HEIGHT = 1.6f;
		/// <summary> How fast to move during a grindstep. </summary>
		private readonly float GRIND_STEP_SPEED = 24.0f;
		public void StartGrindstep()
		{
			// Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(GetInputAngle(), MovementAngle);
			// Calculate how far player is trying to go
			float horizontalTarget = GRIND_STEP_SPEED * Mathf.Sign(inputDeltaAngle);
			horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, InputVector.Length()); // Give some smoothing based on controller strength

			// Keep some speed forward
			MovementAngle += Mathf.Pi * .25f * Mathf.Sign(inputDeltaAngle);
			VerticalSpeed = Runtime.CalculateJumpPower(GRIND_STEP_HEIGHT);
			MoveSpeed = new Vector2(horizontalTarget, MoveSpeed).Length();

			CanJumpDash = false; // Disable jumpdashing
			SetActionState(ActionStates.Grindstep);
			Effect.PlayActionSFX(Effect.JUMP_SFX);
			Animator.StartGrindStep();
		}

		public void StopGrindstep()
		{
			StrafeSpeed = 0;
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
			if (Mathf.IsZeroApprox(MoveSpeed) ||
				(!previousKnockbackSettings.stayOnGround && IsOnGround))
			{
				ResetActionState();
				return;
			}

			VerticalSpeed -= Runtime.GRAVITY * PhysicsManager.physicsDelta;
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, DAMAGE_FRICTION * PhysicsManager.physicsDelta);
		}

		[Signal]
		public delegate void KnockbackEventHandler(); //This signal is called anytime a hitbox collides with the player, regardless of invincibilty.
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
		public void StartKnockback(KnockbackSettings knockbackSettings = new KnockbackSettings())
		{
			EmitSignal(SignalName.Knockback); //Emit signal FIRST so external controllers can be alerted
			if (IsInvincible && !knockbackSettings.ignoreInvincibility) return;

			if (Lockon.IsHomingAttacking)
				Lockon.StopHomingAttack();

			if (Skills.IsSpeedBreakActive) //Disable speedbreak
				Skills.ToggleSpeedBreak();

			MovementAngle = PathFollower.ForwardAngle; //Prevent being knocked sideways

			if (MovementState == MovementStates.Normal || knockbackSettings.ignoreMovementState)
			{
				Animator.StartHurt();
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

			if (MovementState == MovementStates.External) return; //Only allow autorespawning when not using external controller

			//Apply invincibility and drop rings
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
			SetActionState(ActionStates.Damaged);

			if (Level.CurrentRingCount == 0)
			{
				Effect.PlayVoice("defeat");
				StartRespawn();
			}
			else
			{
				Effect.PlayVoice("hurt");
				Level.UpdateRingCount(20, StageSettings.MathModeEnum.Subtract);
			}
		}

		[Signal]
		/// <summary> Use this instead of LevelSettings.SignalName.Respawn to only respawn when the player is defeated. </summary>
		public delegate void RespawnEventHandler();

		/// <summary> True after the player is defeated, but hasn't respawned yet. </summary>
		public bool IsDefeated { get; private set; }
		/// <summary> Is the player currently respawning? </summary>
		public bool IsRespawning { get; private set; }
		/// <summary>
		/// Called when the player is returning to a checkpoint.
		/// </summary>
		public void StartRespawn()
		{
			if (IsRespawning) return;

			//Fade screen out, enable respawn flag, and connect signals
			IsDefeated = true;
			IsRespawning = true;
			TransitionManager.StartTransition(new TransitionData()
			{
				inSpeed = .5f,
				outSpeed = .5f,
				color = Colors.Black //Use Colors.Transparent for debugging
			});

			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.ProcessRespawn), (uint)ConnectFlags.OneShot);
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.OnRespawnFinished), (uint)ConnectFlags.OneShot);
		}

		/// <summary>
		/// Warp the player to the previous checkpoint and revert any actions.
		/// </summary>
		private void ProcessRespawn()
		{
			Lockon.IsMonitoring = false;
			areaTrigger.Disabled = true;

			IsDefeated = false;
			ResetActionState();
			MovementState = MovementStates.Normal;

			invincibilityTimer = 0;
			Teleport(Level.CurrentCheckpoint);
			PathFollower.SetActivePath(Level.CheckpointPlayerPath); //Revert path
			Camera.PathFollower.SetActivePath(Level.CheckpointCameraPath);
			PathFollower.Resync();

			IsMovingBackward = false;
			ResetVelocity();
			ResetOrientation();

			Animator.StopHurt(); // Stop hurt animation

			Level.RespawnObjects();
			Level.IncrementRespawnCount();
			Level.UpdateRingCount(Skills.RespawnRingCount, StageSettings.MathModeEnum.Replace, true); //Reset ring count

			EmitSignal(SignalName.Respawn);

			//Wait a single physics frame to ensure objects update properly
			GetTree().CreateTimer(PhysicsManager.physicsDelta).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.FinishRespawn));
		}

		/// <summary>
		/// Final step of the respawn process. Re-enable area collider and finish transition.
		/// </summary>
		private void FinishRespawn()
		{
			SnapToGround();
			areaTrigger.Disabled = false;

			//TODO Play respawn animation/sfx
			TransitionManager.FinishTransition();
		}

		/// <summary>
		/// Disable respawn flags and allow the game to continue.
		/// </summary>
		private void OnRespawnFinished() => IsRespawning = false;

		private const float TELEPORT_FX_LENGTH = .2f;
		/// <summary>
		/// Teleports the player to a specific location. Use TeleportSettings to have more control of how teleport occurs.
		/// </summary>
		public async void Teleport(Triggers.TeleportTrigger trigger)
		{
			if (trigger.enableStartFX)
			{
				//TODO Play FX
				await ToSignal(GetTree().CreateTimer(TELEPORT_FX_LENGTH), SceneTreeTimer.SignalName.Timeout);
			}

			GlobalPosition = trigger.WarpPosition;
			SnapToGround();

			trigger.ApplyTeleport(); //Apply any signals/path changes

			MovementAngle = PathFollower.ForwardAngle;
			Animator.SnapRotation(PathFollower.ForwardAngle);

			if (trigger.resetMovespeed)
				ResetVelocity();

			if (trigger.crossfade)
				Camera.StartCrossfade();
		}

		/// <summary>
		/// Attempts to snap the player to the ground and sets IsOnGround to true.
		/// </summary>
		private void SnapToGround()
		{
			KinematicCollision3D collision = MoveAndCollide(Vector3.Down * 100.0f, true);
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
		private LaunchSettings LaunchSettings;
		private Objects.Launcher activeLauncher;
		public void StartLauncher(LaunchSettings data, Objects.Launcher newLauncher = null)
		{
			if (activeLauncher != null && activeLauncher == newLauncher) return; //Already launching that!

			ResetMovementState();
			ResetActionState();
			MovementState = MovementStates.Launcher;

			activeLauncher = newLauncher;
			LaunchSettings = data;

			ResetVelocity();

			IsOnGround = false;
			launcherTime = 0;

			CanJumpDash = data.AllowJumpDash;
			Lockon.IsMonitoring = false; //Disable lockon monitoring while launch is active
			Lockon.ResetLockonTarget();

			if (data.UseAutoAlign)
			{
				Vector3 launchDirection = LaunchSettings.InitialVelocity.RemoveVertical();
				if (!launchDirection.IsEqualApprox(Vector3.Zero))
				{
					MovementAngle = ExtensionMethods.CalculateForwardAngle(launchDirection);
					Animator.SnapRotation(MovementAngle);
				}
			}

			if (data.IsJump) //Play jump effects
			{
				Animator.JumpAnimation();
				Effect.PlayActionSFX(Effect.JUMP_SFX);
			}
		}

		private void UpdateLauncher()
		{
			isCustomPhysicsEnabled = true;
			if (activeLauncher != null && !activeLauncher.IsCharacterCentered)
				GlobalPosition = activeLauncher.RecenterCharacter();
			else
			{
				Vector3 targetPosition = LaunchSettings.InterpolatePositionTime(launcherTime);
				float heightDelta = targetPosition.Y - GlobalPosition.Y;
				GlobalPosition = targetPosition;

				if (heightDelta < 0) //Only check ground when falling
					CheckGround();

				if (IsOnGround || LaunchSettings.IsLauncherFinished(launcherTime)) //Revert to normal state
				{
					FinishLauncher();
					MoveSpeed = LaunchSettings.HorizontalVelocity * .5f; //Prevent too much movement
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

			ResetMovementState();

			Lockon.IsMonitoring = CanJumpDash;
			EmitSignal(SignalName.LaunchFinished);
		}
		#endregion

		#region Physics
		/// <summary> Collision shape used for colliding with the environment. </summary>
		[Export]
		private CollisionShape3D environmentCollider;
		/// <summary> Collision shape used for triggering objects. </summary>
		[Export]
		private CollisionShape3D areaTrigger;
		/// <summary> Size to use for collision checks. </summary>
		public float CollisionRadius => (environmentCollider.Shape as SphereShape3D).Radius;
		public bool IsEnvironmentColliderEnabled
		{
			get => !environmentCollider.Disabled;
			set => environmentCollider.Disabled = !value;
		}

		/// <summary> Center of collision calculations </summary>
		public Vector3 CenterPosition
		{
			get => GlobalPosition + UpDirection * CollisionRadius;
			set => GlobalPosition = value - UpDirection * CollisionRadius;
		}
		private const float COLLISION_PADDING = .04f;

		/// <summary> Character's primary movement speed. </summary>
		public float MoveSpeed { get; set; }
		/// <summary> Used during speed break, etc. </summary>
		public float StrafeSpeed { get; set; }
		/// <summary> Used for jumping and falling. </summary>
		public float VerticalSpeed { get; set; }
		/// <summary> Resets all speed values to zero. </summary>
		private void ResetVelocity() => MoveSpeed = StrafeSpeed = VerticalSpeed = 0;

		public Vector3 TrueVelocity { get; private set; }

		private void UpdatePhysics()
		{
			Lockon.UpdateLockonTargets();
			if (isCustomPhysicsEnabled) return; //When physics are handled in the state machine

			Vector3 movementDirection = GetMovementDirection();
			Vector3 strafeDirection = movementDirection.Cross(UpDirection);

			Velocity = movementDirection * MoveSpeed + strafeDirection * StrafeSpeed;
			Velocity += UpDirection * VerticalSpeed;

			//Store the current position to calculate true velocity later
			TrueVelocity = GlobalPosition;

			MoveAndSlide();

			CheckMainWall(movementDirection);
			CheckStrafeWall(strafeDirection);

			//Collision checks
			CheckGround();
			CheckCeiling();

			//Calculate true velocity after physics were processed.
			TrueVelocity -= GlobalPosition;
			if (IsOnGround && IsOnWall() && Mathf.IsZeroApprox(TrueVelocity.LengthSquared()))
			{
				MoveSpeed = 0;
				StrafeSpeed = 0;
			}

			PathFollower.Resync(); //Resync
			UpdateRecenter();
		}

		public new bool IsOnWall { get; set; }

		public bool IsOnGround { get; set; }
		public bool JustLandedOnGround { get; private set; } //Flag for doing stuff on land

		private const int GROUND_CHECK_AMOUNT = 8; //How many "whiskers" to use when checking the ground
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = CollisionRadius + COLLISION_PADDING * 2.0f;
			if (IsOnGround)
				castLength += Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta; // Attempt to remain stuck to the ground when moving quickly
			else if (VerticalSpeed < 0)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;

			Vector3 checkOffset = Vector3.Zero;
			RaycastHit groundHit = new();
			Vector3 castVector = this.Down() * castLength;
			int raysHit = 0;

			// Whisker casts (For smoother collision)
			float interval = Mathf.Tau / GROUND_CHECK_AMOUNT;
			Vector3 castOffset = this.Forward() * (CollisionRadius - COLLISION_PADDING);
			for (int i = 0; i < GROUND_CHECK_AMOUNT; i++)
			{
				castOffset = castOffset.Rotated(this.Down(), interval).Normalized() * CollisionRadius;
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
				float snapDistance = groundHit.distance - CollisionRadius - COLLISION_PADDING;
				// Landing on the ground
				if (!IsOnGround && VerticalSpeed < 0)
				{
					UpDirection = groundHit.normal;
					UpdateOrientation();
					LandOnGround();
					Effect.PlayLandingFX();
				}

				GlobalPosition -= UpDirection * snapDistance; // Snap to ground

				if (IsOnGround)
				{
					//Update world direction
					UpDirection = UpDirection.Lerp(groundHit.normal, .2f + .4f * GroundSettings.GetSpeedRatio(MoveSpeed)).Normalized();
					UpdateSlopeInfluence(groundHit.normal);
				}
			}
			else
			{
				if (IsOnGround) //Leave ground
				{
					IsOnGround = false;
					Animator.IsFallTransitionEnabled = true;
				}

				// Calculate target up direction
				Vector3 targetUpDirection = Vector3.Up;
				if (Camera.ActiveSettings.followPathTilt) // Use PathFollower.Up when on a tilted path.
					targetUpDirection = PathFollower.Up();
				else if (ActionState == ActionStates.Backflip)
					targetUpDirection = PathFollower.HeightAxis;

				// Calculate reset factor
				float orientationResetFactor = 0;
				if (ActionState == ActionStates.Stomping ||
				ActionState == ActionStates.JumpDash || ActionState == ActionStates.Backflip) // Quickly reset when stomping/homing attacking
					orientationResetFactor = .2f;
				else if (VerticalSpeed > 0)
					orientationResetFactor = .01f;
				else
					orientationResetFactor = VerticalSpeed * .2f / Runtime.MAX_GRAVITY;

				UpDirection = UpDirection.Lerp(targetUpDirection, Mathf.Clamp(orientationResetFactor, 0f, 1f)).Normalized();
			}
		}

		public void LandOnGround()
		{
			IsOnGround = true;
			VerticalSpeed = 0;

			IsJumpClamped = false;
			CanJumpDash = false;

			isAccelerationJumpQueued = false;
			if (ActionState == ActionStates.Grindstep)
				ResetActionState();

			ResetActionState();
			Lockon.ResetLockonTarget();

			if (IsCountdownActive || IsRespawning) return; //Don't do this stuff during countdown or respawn

			JustLandedOnGround = true;
			CheckLandingBoost(); //Landing boost skill

			// Play FX
			Effect.PlayLandingFX();
		}

		/// <summary> Checks whether raycast collider is tagged properly. </summary>
		private bool ValidateGroundCast(ref RaycastHit hit)
		{
			if (hit)
			{
				if (!hit.collidedObject.IsInGroup("floor"))
					hit = new RaycastHit();
				else if (MovementState != MovementStates.External && hit.normal.AngleTo(UpDirection) > Mathf.Pi * .4f) //Limit angle collision
					hit = new RaycastHit();
				else if (!IsOnGround &&
					hit.collidedObject.IsInGroup("wall")) //Use Vector3.Up for objects tagged as a wall
				{
					if (hit.normal.AngleTo(Vector3.Up) > Mathf.Pi * .2f)
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

		public void CheckCeiling() //Checks the ceiling.
		{
			//Ceiling check casts from the root position, to allow more accurate crusher detection
			Vector3 castOrigin = GlobalPosition;
			float castLength = CollisionRadius * 2f;
			if (VerticalSpeed > 0)
				castLength += VerticalSpeed * PhysicsManager.physicsDelta;

			Vector3 castVector = UpDirection * castLength;
			if (ActionState == ActionStates.Backflip) //Improve collision detection when backflipping
				castVector += GetMovementDirection() * MoveSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				if (ceilingHit.collidedObject.IsInGroup("crusher") && IsOnGround) //Check if the player is being crushed
				{
					GD.Print($"Crushed by {ceilingHit.collidedObject.Name}");
					AddCollisionExceptionWith(ceilingHit.collidedObject); //Avoid clipping through the ground
					StartKnockback(new KnockbackSettings()
					{
						ignoreInvincibility = true,
					});

					return;
				}

				if (!ceilingHit.collidedObject.IsInGroup("ceiling")) return;

				GlobalTranslate(ceilingHit.point - (CenterPosition + UpDirection * CollisionRadius));

				float maxVerticalSpeed = 0;
				//Workaround for backflipping into slanted ceilings
				if (ActionState == ActionStates.Backflip)
				{
					float ceilingAngle = ceilingHit.normal.AngleTo(Vector3.Down);

					if (ceilingAngle > Mathf.Pi * .1f) //Only slanted ceilings need this workaround
					{
						float deltaAngle = ExtensionMethods.DeltaAngleRad(PathFollower.ForwardAngle, ExtensionMethods.CalculateForwardAngle(ceilingHit.normal, IsOnGround ? PathFollower.Up() : Vector3.Up));
						if (deltaAngle > Mathf.Pi * .1f) //Wall isn't aligned to the path
							return;

						//Slide down the wall if it's aligned with the path direction
						maxVerticalSpeed = -Mathf.Sin(ceilingAngle) * MoveSpeed;
					}
				}

				if (VerticalSpeed > maxVerticalSpeed)
					VerticalSpeed = maxVerticalSpeed;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			IsOnWall = false;
			if (Mathf.IsZeroApprox(MoveSpeed)) //No movement
			{
				DebugManager.DrawRay(CenterPosition, castVector * CollisionRadius, Colors.White);
				return;
			}

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = CollisionRadius + COLLISION_PADDING + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;

			RaycastHit wallHit = this.CastRay(CenterPosition, castVector * castLength, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(CenterPosition, castVector * castLength, wallHit ? Colors.Red : Colors.White);

			if (ValidateWallCast(ref wallHit))
			{
				if (ActionState != ActionStates.JumpDash && ActionState != ActionStates.Backflip)
				{
					float wallDelta = ExtensionMethods.DeltaAngleRad(ExtensionMethods.CalculateForwardAngle(wallHit.normal, IsOnGround ? PathFollower.Up() : Vector3.Up), MovementAngle);
					if (wallDelta >= Mathf.Pi * .75f) // Process wall collision 
					{
						// Cancel speed break
						if (Skills.IsSpeedBreakActive)
							Skills.ToggleSpeedBreak();

						// Running into wall head-on
						if (wallDelta >= Mathf.Pi * .9f && wallHit.distance <= CollisionRadius + COLLISION_PADDING)
						{
							IsOnWall = true;
							MoveSpeed = 0; // Kill speed
							return;
						}
					}

					if (!IsMovingBackward && IsOnGround) // Reduce MoveSpeed when running against walls
					{
						float speedClamp = Mathf.Clamp(1.0f - (wallDelta / Mathf.Pi) * .4f, 0f, 1f); // Arbitrary formula that works well
						if (GroundSettings.GetSpeedRatio(MoveSpeed) > speedClamp)
							MoveSpeed *= speedClamp;
					}
				}
			}
		}

		private void CheckStrafeWall(Vector3 castVector)
		{
			if (Mathf.IsZeroApprox(StrafeSpeed)) //Strafing disabled
				return;

			castVector *= Mathf.Sign(StrafeSpeed);
			float castLength = CollisionRadius + COLLISION_PADDING + Mathf.Abs(StrafeSpeed) * PhysicsManager.physicsDelta;

			RaycastHit wallHit = this.CastRay(CenterPosition, castVector * castLength, CollisionMask, false, GetCollisionExceptions());
			DebugManager.DrawRay(CenterPosition, castVector * castLength, wallHit ? Colors.Red : Colors.White);

			if (ValidateWallCast(ref wallHit))
				StrafeSpeed = 0;
		}

		private const float ORIENTATION_SMOOTHING = .4f;
		private void ResetOrientation()
		{
			UpDirection = Vector3.Up;

			if (Level.CurrentCheckpoint == null) //Default to parent node's position
				Transform = Transform3D.Identity;
			else
				GlobalTransform = Level.CurrentCheckpoint.GlobalTransform;

			MovementAngle = PathFollower.ForwardAngle; //Reset movement angle
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
			Vector3 pathFollowerForward = PathFollower.Back().Rotated(UpDirection, PathFollower.DeltaAngle);

			//Tilted ground fix
			float fixAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			return PathFollower.Back().Rotated(UpDirection, fixAngle);
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
			SnapToGround();
		}

		private void UpdateCountdown()
		{
			if (Input.IsActionJustPressed("button_action"))
				actionBufferTimer = 1f;
			actionBufferTimer -= PhysicsManager.physicsDelta;
		}

		public void OnCountdownFinished()
		{
			if (Skills.IsSkillEnabled(SkillKeyEnum.RocketStart) && actionBufferTimer > 0 && actionBufferTimer < COUNTDOWN_BOOST_WINDOW) //Successful starting boost
			{
				MoveSpeed = Skills.countdownBoostSpeed;
				AddLockoutData(new LockoutResource()
				{
					length = .5f,
					overrideSpeed = true,
					speedRatio = Skills.countdownBoostSpeed,
					resetFlags = LockoutResource.ResetFlags.OnJump
				});
				GD.Print("Successful countdown boost");
			}

			Animator.CancelOneshot();

			//Snap camera to gameplay
			Camera.SnapFlag = true;
			actionBufferTimer = 0; //Reset action buffer from starting boost
		}


		private void OnLevelCompleted()
		{
			ResetActionState();
			// Disable everything
			Lockon.IsMonitoring = false;
			Skills.IsTimeBreakEnabled = false;
			Skills.IsSpeedBreakEnabled = false;

			if (Level.LevelState == StageSettings.LevelStateEnum.Failed || Level.CompletionLockout == null)
				AddLockoutData(Runtime.Instance.StopLockout);
			else
				AddLockoutData(Level.CompletionLockout);
		}


		private void OnLevelDemoStarted()
		{
			MoveSpeed = 0;
			AddLockoutData(Runtime.Instance.StopLockout);
		}

		public void OnObjectCollisionEnter(Node3D body)
		{
			/*
			Note for when I come back wondering why the player is being pushed through the floor
			Ensure all crushers' animationplayers are using the PHYSICS update mode
			If this is true, then proceed to panic.

			Crusher check has been moved to CheckCeiling().
			*/

			if (Lockon.IsHomingAttacking && body.IsInGroup("wall") && body.IsInGroup("splash jump"))
			{
				if (Skills.IsSkillEnabled(SkillKeyEnum.SplashJump)) // Perform a splash jump
					Skills.SplashJump();
				else // Cancel HomingAttack
					Lockon.StopHomingAttack();
			}
		}

		public void OnObjectCollisionExit(Node3D body)
		{
			if (!(body is PhysicsBody3D)) return;

			if (GetCollisionExceptions().Contains(body as PhysicsBody3D))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
			}
		}
		#endregion

		//Components, rarely needs to be edited, so they go at the bottom of the inspector
		//All public so any object can get whatever player data they need
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
