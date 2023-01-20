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

		public CameraController Camera => CameraController.instance;
		public InputManager.Controller Controller => InputManager.controller;
		public LevelSettings Level => LevelSettings.instance;
		public StageSettings Stage => StageSettings.instance;

		public override void _EnterTree() => instance = this; //Override Singleton

		public override void _Ready()
		{
			CallDeferred(MethodName.ResetOrientation); //Start with proper orientation

			PathFollower.SetActivePath(Stage.mainPath); //Attempt to autoload the stage's default path
			Level.SetCheckpoint(GetParent<Node3D>()); //Initial checkpoint configuration
			Level.Connect(LevelSettings.SignalName.LevelCompleted, new Callable(this, MethodName.OnLevelCompleted));
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
			Crouching, //Sliding included
			Damaged, //Being knocked back by damage
			Respawning, //Idle until respawn timer reaches zero
			JumpDash, //Also includes homing attack
			Stomping, //Jump cancel
			Backflip,
		}

		/// <summary>
		/// Reset action state to ActionState.Normal
		/// </summary>
		public void ResetActionState() => ActionState = ActionStates.Normal;
		private void UpdateStateMachine()
		{
			if (IsRespawning) return;

			if (IsCountdownActive)
			{
				UpdateCountdown();
				return;
			}

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
		/// <summary>
		/// Is the player holding in the specified direction?
		/// </summary>
		public bool IsHoldingDirection(float refAngle, bool allowNullInputs = default)
		{
			if (!allowNullInputs && Controller.IsHoldingNeutral)
				return false;

			float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle(), refAngle);
			return delta < Mathf.Pi * .4f;
		}

		/// <summary>
		/// Returns the target movement angle based on the camera view.
		/// </summary>
		public float GetTargetInputAngle()
		{
			if (Controller.IsHoldingNeutral) //Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(Controller.MovementAxis.AngleTo(Vector2.Up)); //Target rotation angle (in radians)
		}

		private float GetTargetMovementAngle()
		{
			float inputAngle = GetTargetInputAngle();

			if (Skills.IsSpeedBreakActive)
				return PathFollower.ForwardAngle + PathFollower.DeltaAngle;

			if (IsLockoutActive && ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free)
			{
				float targetAngle = Mathf.DegToRad(ActiveLockoutData.movementAngle);
				if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.Camera)
					targetAngle = Camera.TransformAngle(targetAngle);
				else if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.PathFollower)
					targetAngle = PathFollower.ForwardAngle + targetAngle + PathFollower.DeltaAngle;
				else if (ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.Local)
					targetAngle += MovementAngle;

				if (ActiveLockoutData.allowReversing) //Check if we're trying to turn around
				{
					if (turnInstantly)
						IsMovingBackward = ExtensionMethods.DeltaAngleRad(inputAngle, PathFollower.ForwardAngle) > Mathf.Pi * .54f;

					if (IsMovingBackward)
						targetAngle += Mathf.Pi; //Flip targetAngle when moving backwards
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

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
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

			Vector3 recenterDirection = PathFollower.Forward().Rotated(UpDirection, Mathf.Pi * .5f);
			float currentOffset = -PathFollower.FlatPlayerPositionDelta.x;
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
		private Node3D externalParent;
		private Vector3 externalOffset;
		private float externalSmoothing;

		/// <summary> Used during homing attacks and whenever external objects are overridding physics. </summary>
		private bool isCustomPhysicsEnabled;

		public void StartExternal(Node controller, Node3D followObject = null, float smoothing = 0f, bool allowSpeedBreak = false)
		{
			ExternalController = controller;

			ResetMovementState();
			MovementState = MovementStates.External;
			ActionState = ActionStates.Normal;

			Skills.IsSpeedBreakEnabled = allowSpeedBreak;

			externalParent = followObject;
			externalOffset = Vector3.Zero; //Reset offset
			externalSmoothing = smoothing;
			if (externalParent != null && !Mathf.IsZeroApprox(smoothing)) //Smooth out transition
				externalOffset = GlobalPosition - externalParent.GlobalPosition;

			ResetVelocity();
			UpdateExternalControl();

			EmitSignal(SignalName.ExternalControlStarted);
		}

		public void StopExternal()
		{
			MovementState = MovementStates.Normal; //Needs to be set to normal BEFORE orientation is reset

			UpdateOrientation();
			EmitSignal(SignalName.ExternalControlCompleted);
		}

		/// <summary>
		/// Moves the player to externalParent. Must be called after external controller has been processed.
		/// </summary>
		public void UpdateExternalControl()
		{
			if (JustLandedOnGround) //Bugfix: Don't let animator get stuck playing landing animation
				JustLandedOnGround = false;

			isCustomPhysicsEnabled = true;
			externalOffset = externalOffset.Lerp(Vector3.Zero, externalSmoothing); //Smooth out entry

			if (externalParent != null)
				GlobalTransform = externalParent.GlobalTransform;

			GlobalPosition += externalOffset;
			PathFollower.Resync();
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
			IsMovingBackward = ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.ForwardAngle) > Mathf.Pi * .54f; //Moving backwards

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

		private const float TURNING_SPEED_LOSS = .04f; //How much speed to lose when turning sharply
		private const float MIN_TURN_SPEED = .12f; //How much to turn when moving slowly
		private const float MAX_TURN_SPEED = .32f; //How much to turn when moving at top speed
		private const float STRAFE_TURNAROUND_SPEED = .2f; //How quickly to turnaround when at top speed
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_TURNAROUND_ANGLE = Mathf.Pi * .75f;
		/// <summary> Updates MoveSpeed. What else do you need know? </summary>
		private void UpdateMoveSpeed()
		{
			turnInstantly = Mathf.IsZeroApprox(MoveSpeed); //Store this for turning function

			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Backflip) return;
			if (Skills.IsSpeedBreakActive) return; //Overridden to max speed

			float inputAngle = GetTargetInputAngle();
			float inputLength = Controller.MovementAxisLength; //Limits top speed; Modified depending on the LockoutResource.directionOverrideMode

			MovementResource activeMovementResource = GetActiveMovementSettings();

			if (IsLockoutActive && ActiveLockoutData.overrideSpeed)
			{
				//Override speed to the correct value
				float targetSpd = activeMovementResource.speed * ActiveLockoutData.speedRatio;
				if (Mathf.IsZeroApprox(ActiveLockoutData.tractionMultiplier)) //Snap speed (i.e. Dash Panels)
				{
					MoveSpeed = targetSpd;
					return;
				}

				float delta = PhysicsManager.physicsDelta;
				if (MoveSpeed <= targetSpd) //Accelerate using traction
					delta *= activeMovementResource.traction * ActiveLockoutData.tractionMultiplier;
				else //Slow down with friction
					delta *= activeMovementResource.friction * ActiveLockoutData.frictionMultiplier;
				MoveSpeed = Mathf.MoveToward(MoveSpeed, targetSpd, delta);
				return;
			}

			float inputDot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, GetTargetMovementAngle()));
			if (Controller.IsHoldingNeutral) //Basic slow down
				MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, 0);
			else
			{
				float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);
				bool isTurningAround = deltaAngle > MAX_TURNAROUND_ANGLE;
				if (isTurningAround) //Skid to a stop
					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, -1);
				else
				{
					if (inputDot < .8f)
						inputLength *= inputDot;

					if (IsMovingBackward && !IsOnGround) //Greatly reduce input strength when jumping backwards
						inputLength *= .2f;

					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, inputLength); //Accelerate based on input strength/input direction
				}
			}
		}

		/// <summary> True when the player's MoveSpeed was zero </summary>
		private bool turnInstantly;
		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			if (ActionState == ActionStates.Backflip) return;

			float targetMovementAngle = GetTargetMovementAngle();
			bool overrideFacingDirection = Skills.IsSpeedBreakActive || (IsLockoutActive &&
			(ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace ||
			ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe));

			//Strafe implementation
			if (Skills.IsSpeedBreakActive ||
			(IsLockoutActive && ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe))
			{
				//Custom strafing movement
				float strafeAmount = ExtensionMethods.DotAngle(GetTargetInputAngle() + Mathf.Pi * .5f, PathFollower.ForwardAngle);
				strafeAmount *= Controller.MovementAxisLength; //Analog inputs

				//Reduce strafe speed when moving slowly
				strafeAmount *= (IsOnGround ? GroundSettings.GetSpeedRatioClamped(MoveSpeed) : AirSettings.GetSpeedRatioClamped(MoveSpeed)) + .1f;
				StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, strafeAmount);
			}

			if (overrideFacingDirection)
				MovementAngle = targetMovementAngle;

			StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, 0); //Reset strafe when not in use

			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			if (ActionState == ActionStates.Backflip) return;
			if (!turnInstantly && deltaAngle > MAX_TURNAROUND_ANGLE) return; //Turning around

			float maxTurnSpeed = MAX_TURN_SPEED;
			float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, PathFollower.ForwardAngle);
			if (IsHoldingDirection(PathFollower.ForwardAngle) &&
			(Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle)))
				maxTurnSpeed = STRAFE_TURNAROUND_SPEED;

			float speedRatio = IsOnGround ? GroundSettings.GetSpeedRatio(MoveSpeed) : AirSettings.GetSpeedRatio(MoveSpeed);
			float turnDelta = Mathf.Lerp(MIN_TURN_SPEED, maxTurnSpeed, speedRatio);

			if (turnInstantly) //Instantly set movement angle to target movement angle
			{
				turningVelocity = 0;
				MovementAngle = targetMovementAngle;
			}

			if (IsSpeedLossActive())
			{
				//Calculate turn delta, relative to ground speed
				float speedLossRatio = deltaAngle / MAX_TURNAROUND_ANGLE;
				MoveSpeed -= GroundSettings.speed * speedRatio * turningSpeedCurve.Sample(speedLossRatio) * TURNING_SPEED_LOSS;
				if (MoveSpeed < 0)
					MoveSpeed = 0;
			}

			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, turnDelta);
			if (Camera.ActiveSettings.isRollEnabled) //Only do this when camera is rolling
				MovementAngle += PathFollower.DeltaAngle * 1.84f; //Random number that seems pretty accurate.
		}

		/// <summary>
		/// Returns true when speed loss should be applied.
		/// </summary>
		private bool IsSpeedLossActive()
		{
			//Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
			if (IsHoldingDirection(PathFollower.ForwardAngle) && GroundSettings.GetSpeedRatio(MoveSpeed) > .5f)
				return false;

			//Or when overriding speed/direction
			if (IsLockoutActive &&
			(ActiveLockoutData.overrideSpeed || ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free))
				return false;

			return true;
		}

		private MovementResource GetActiveMovementSettings()
		{
			if (!IsOnGround)
				return AirSettings;
			return IsMovingBackward ? BackstepSettings : GroundSettings;
		}

		private float slopeInfluence; //Current influence of the slope
		private const float SLOPE_INFLUENCE_STRENGTH = .2f; //How much should slope affect player?
		private const float SLOPE_THRESHOLD = .02f; //Ignore slopes that are shallower than Mathf.PI * threshold
		private void UpdateSlopeInfluence(Vector3 groundNormal)
		{
			//Calculate slope influence
			float angle = groundNormal.AngleTo(Vector3.Up);
			if (Mathf.Abs(angle) < Mathf.Pi * SLOPE_THRESHOLD) //Slope is too insignificant to affect movement
			{
				slopeInfluence = 0; //Reset influence
				return;
			}

			float rotationAmount = PathFollower.Forward().SignedAngleTo(Vector3.Forward, Vector3.Up);
			Vector3 slopeDirection = groundNormal.Rotated(Vector3.Up, rotationAmount).Normalized();
			slopeInfluence = slopeDirection.z * SLOPE_INFLUENCE_STRENGTH;
		}

		private void UpdateSlopeSpd()
		{
			if (Mathf.IsZeroApprox(MoveSpeed) || IsMovingBackward) return; //Idle/Backstepping isn't affected by slopes
			if (!IsOnGround) return; //Slope is too shallow or not on the ground
			if (IsLockoutActive && ActiveLockoutData.ignoreSlopes) return; //Lockout is ignoring slopes

			if (IsHoldingDirection(PathFollower.ForwardAngle)) //Accelerating
			{
				if (slopeInfluence < 0f) //Downhill
				{
					//Capped - MoveSpeed = Mathf.MoveToward(MoveSpeed, GroundSettings.speed, GroundSettings.traction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta);
					MoveSpeed += GroundSettings.traction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta; //Uncapped
				}
				else if (GroundSettings.GetSpeedRatioClamped(MoveSpeed) < 1f) //Uphill; Reduce acceleration (Only when not at top speed)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.traction * slopeInfluence * PhysicsManager.physicsDelta);
			}
			else if (MoveSpeed > 0f) //Decceleration (Only applied when actually moving)
			{
				if (slopeInfluence < 0f) //Re-apply some speed when moving downhill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, GroundSettings.speed, GroundSettings.friction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta);
				else //Increase friction when moving uphill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, GroundSettings.friction * slopeInfluence * PhysicsManager.physicsDelta);
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
			if (IsLockoutActive) //Controls locked out.
			{
				if (ExtensionMethods.IsSet<LockoutResource.ResetFlags>(LockoutResource.ResetFlags.OnLand, ActiveLockoutData.resetFlags) && JustLandedOnGround) //Cancel lockout
					RemoveLockoutData(ActiveLockoutData);
				else if (ActiveLockoutData.disableActions)
					return;
			}

			if (Skills.IsSpeedBreakActive) return;

			if (ActionState == ActionStates.Crouching)
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

				if (IsLockoutActive && ExtensionMethods.IsSet<LockoutResource.ResetFlags>(LockoutResource.ResetFlags.OnJump, ActiveLockoutData.resetFlags))
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
					if (Lockon.IsBouncing)
					{
						Lockon.UpdateBounce();
						return;
					}

					CheckStomp();
					break;
			}

			CheckJumpDash();
			ApplyGravity(); //Always apply gravity when in the air
		}

		private void ApplyGravity() => VerticalSpd = Mathf.MoveToward(VerticalSpd, RuntimeConstants.MAX_GRAVITY, RuntimeConstants.GRAVITY * PhysicsManager.physicsDelta);

		[Export]
		public float landingBoost; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private bool canLandingBoost;
		private void CheckLandingBoost()
		{
			if (!canLandingBoost) return;
			canLandingBoost = false; //Reset landing boost

			if (MovementState != MovementStates.Normal) return;

			//Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
			if (IsHoldingDirection(PathFollower.ForwardAngle) && MoveSpeed < landingBoost)
				MoveSpeed = landingBoost;
		}

		#region Jump
		[Export]
		public float jumpHeight;
		[Export]
		public float jumpCurve = .95f;
		public bool IsJumpClamped { get; private set; } //True after the player releases the jump button
		/// <summary> Is the player switching between rails? </summary>
		public bool IsGrindstepJump { get; set; }
		private bool isAccelerationJumpQueued;
		private float currentJumpTime; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .08f; //How fast the jump button needs to be released for an "acceleration jump"
		public void Jump(bool disableAccelerationJump = default)
		{
			currentJumpTime = disableAccelerationJump ? ACCELERATION_JUMP_LENGTH + PhysicsManager.physicsDelta : 0;
			IsJumpClamped = false;
			IsOnGround = false;
			CanJumpDash = true;
			disableGroundSnap = true;
			canLandingBoost = Skills.isLandingDashEnabled;
			ActionState = ActionStates.Jumping;
			VerticalSpd = RuntimeConstants.GetJumpPower(jumpHeight);

			if (IsMovingBackward || MoveSpeed < 0) //Kill speed when jumping backwards
				MoveSpeed = 0;

			Effect.PlayActionSFX(Effect.JUMP_SFX);
			Animator.Jump();
		}

		private void UpdateJump()
		{
			if (isAccelerationJumpQueued && currentJumpTime >= ACCELERATION_JUMP_LENGTH) //Acceleration jump?
			{
				if (IsHoldingDirection(PathFollower.ForwardAngle, true) && Controller.MovementAxisLength > .5f)
				{
					ActionState = ActionStates.AccelJump;
					MoveSpeed = Skills.accelerationJumpSpeed;
					Animator.AirAttackAnimation();
				}

				VerticalSpd = 5f; //Consistant accel jump height
				isAccelerationJumpQueued = false; //Stop listening for an acceleration jump
			}

			if (!IsJumpClamped)
			{
				if (!Controller.jumpButton.isHeld)
				{
					IsJumpClamped = true;
					if (currentJumpTime <= ACCELERATION_JUMP_LENGTH) //Listen for acceleration jump
						isAccelerationJumpQueued = true;
				}
			}
			else if (VerticalSpd > 0f)
				VerticalSpd *= jumpCurve; //Kill jump height

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
			if (CanJumpDash && jumpBufferTimer != 0)
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

			if (CanJumpDash)
				Effect.PlayActionSFX("jump dash");

			CanJumpDash = false;
			IsMovingBackward = false; //Can't jumpdash backwards!
			MoveSpeed = jumpDashSpeed;
			ActionState = ActionStates.JumpDash;

			if (Lockon.LockonTarget == null) //Normal jumpdash
			{
				VerticalSpd = jumpDashPower;
				Animator.LaunchAnimation();
			}
			else
			{
				Lockon.StartHomingAttack(); //Start Homing attack
				Animator.AirAttackAnimation();
			}
		}

		private void UpdateJumpDash()
		{
			if (Lockon.IsHomingAttacking) //Homing attack
			{
				if (Lockon.LockonTarget == null) //Target disappeared. Transition to jumpdash
				{
					MovementAngle = PathFollower.ForwardAngle;
					Lockon.IsHomingAttacking = false;
					StartJumpDash();
					return;
				}

				isCustomPhysicsEnabled = true;
				VerticalSpd = 0;
				Velocity = Lockon.HomingAttackDirection.Normalized() * Skills.homingAttackSpeed;
				MovementAngle = CalculateForwardAngle(Lockon.HomingAttackDirection);
				MoveAndSlide();
				PathFollower.Resync();
			}
			else //Normal Jump dash; Apply gravity
				VerticalSpd = Mathf.MoveToward(VerticalSpd, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);

			CheckStomp();
		}
		#endregion

		#region Crouch & Slide
		private void StartCrouching()
		{
			ActionState = ActionStates.Crouching;
		}

		public void UpdateCrouching()
		{
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, Skills.SlideFriction * PhysicsManager.physicsDelta); //Slow down

			if (Controller.actionButton.wasReleased)
				ResetActionState();
		}
		#endregion

		#region Stomp
		/// <summary> How fast to fall when stomping </summary>
		private const int STOMP_SPEED = -32;
		/// <summary> How much gravity to add each frame </summary>
		private const int STOMP_GRAVITY = 320;
		private void UpdateStomp()
		{
			MoveSpeed = StrafeSpeed = 0; //Go STRAIGHT down
			VerticalSpd = Mathf.MoveToward(VerticalSpd, STOMP_SPEED, STOMP_GRAVITY * PhysicsManager.physicsDelta);
		}

		private void CheckStomp()
		{
			if (Mathf.IsZeroApprox(actionBufferTimer)) return;

			//Don't allow instant stomps
			if ((ActionState == ActionStates.Jumping || ActionState == ActionStates.AccelJump) &&
			currentJumpTime < .1f)
			{
				actionBufferTimer = 0;
				return;
			}

			//Stomp
			actionBufferTimer = 0;
			ResetVelocity();

			canLandingBoost = true;
			Lockon.ResetLockonTarget();
			Lockon.IsMonitoring = false;

			ActionState = ActionStates.Stomping;
		}
		#endregion

		#region Backflip
		[Export]
		public float backflipHeight;
		/// <summary> How much can the player adjust their angle while backflipping? </summary>
		private const float MAX_BACKFLIP_ADJUSTMENT = Mathf.Pi * .25f;
		/// <summary> How much to turn when backflipping </summary>
		private const float BACKFLIP_TURN_SPEED = .25f;
		private void UpdateBackflip()
		{
			if (!IsHoldingDirection(PathFollower.ForwardAngle)) //Influence backflip direction slightly
			{
				float targetMovementAngle = ExtensionMethods.ClampAngleRange(GetTargetMovementAngle(), PathFollower.BackAngle, MAX_BACKFLIP_ADJUSTMENT);
				MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, BACKFLIP_TURN_SPEED);

				if (IsHoldingDirection(PathFollower.BackAngle))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, Controller.MovementAxisLength);
				else if (Mathf.IsZeroApprox(Controller.MovementAxisLength))
					MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, 0);
			}
			else
				MoveSpeed = Skills.BackflipSettings.Interpolate(MoveSpeed, -1);

			if (IsOnGround)
				ResetActionState();
		}

		private void StartBackflip()
		{
			CanJumpDash = true;
			MoveSpeed = Skills.BackflipSettings.speed;

			IsMovingBackward = true;
			MovementAngle = GetTargetInputAngle();

			VerticalSpd = RuntimeConstants.GetJumpPower(backflipHeight);

			IsOnGround = false;
			disableGroundSnap = true;
			ActionState = ActionStates.Backflip;

			Effect.PlayActionSFX(Effect.JUMP_SFX);
			Animator.Backflip();
		}
		#endregion
		#endregion
		#endregion

		#region Damage & Invincibility
		public bool IsInvincible => invincibliltyTimer != 0;
		private float invincibliltyTimer;
		private const float INVINCIBILITY_LENGTH = 3f;

		private void UpdateInvincibility()
		{
			if (IsInvincible)
				invincibliltyTimer = Mathf.MoveToward(invincibliltyTimer, 0, PhysicsManager.physicsDelta);
		}

		private void UpdateDamage()
		{
			if (IsOnGround)
			{
				ResetActionState();
				return;
			}

			VerticalSpd -= RuntimeConstants.GRAVITY * PhysicsManager.physicsDelta;
		}

		[Signal]
		public delegate void DamagedEventHandler(); //This signal is called anytime a hitbox collides with the player, regardless of invincibilty.
		public enum KnockbackMode
		{
			Disabled,
			Backward, //Bump the player back
			Forward, //Bump the player forward
		}

		/// <summary>
		/// Called when the player takes damage or is being knocked around.
		/// </summary>
		public void Knockback(bool disableDamage = false, KnockbackMode knockbackMode = KnockbackMode.Backward)
		{
			EmitSignal(SignalName.Damaged);

			//Apply invincibility and drop rings
			if (!IsInvincible && !disableDamage)
			{
				ActionState = ActionStates.Damaged;
				invincibliltyTimer = INVINCIBILITY_LENGTH;
			}

			if (MovementState == MovementStates.Normal)
			{
				IsGrindstepJump = false;
				IsOnGround = false;

				MovementAngle = PathFollower.ForwardAngle; //Prevent being knocked sideways

				switch (knockbackMode)
				{
					case KnockbackMode.Backward:
						MoveSpeed = -8f;
						VerticalSpd = RuntimeConstants.GetJumpPower(3f);
						disableGroundSnap = true;
						break;
					case KnockbackMode.Forward:
						MoveSpeed = 8f;
						VerticalSpd = RuntimeConstants.GetJumpPower(3f);
						disableGroundSnap = true;
						break;
				}
			}
		}

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

			//ProcessMode = ProcessModeEnum.Disabled;
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.ProcessRespawn), (uint)ConnectFlags.OneShot);
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.OnRespawnFinished), (uint)ConnectFlags.OneShot);
		}

		/// <summary>
		/// Warp the player to the previous checkpoint and revert any actions.
		/// </summary>
		private void ProcessRespawn()
		{
			IsDefeated = false;
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			GlobalPosition = Level.CurrentCheckpoint.GlobalPosition;
			PathFollower.SetActivePath(Level.CheckpointPath); //Revert path
			PathFollower.Resync();

			ResetVelocity();
			ResetOrientation();

			Camera.UpdateCameraSettings(Level.CheckpointCamera, 0); //Revert camera settings

			//"Flicker" area collider to re-trigger any area the player happens to be respawning in
			areaTrigger.Disabled = true;
			//Wait a single physics frame to ensure objects reset properly
			GetTree().CreateTimer(PhysicsManager.physicsDelta).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.FinishRespawn));
		}

		/// <summary>
		/// Final step of the respawn process. Re-enable area collider and finish transition.
		/// </summary>
		private void FinishRespawn()
		{
			Level.RespawnObjects();
			SnapToGround();
			areaTrigger.Disabled = false;

			//TODO Play respawn animation/sfx
			TransitionManager.FinishTransition();
		}

		/// <summary>
		/// Disable respawn flags and allow the game to continue.
		/// </summary>
		private void OnRespawnFinished() => IsRespawning = false;

		/// <summary>
		/// Attempts to snap the player to the ground and sets IsOnGround to true.
		/// </summary>
		private void SnapToGround()
		{
			RaycastHit groundHit = this.CastRay(CenterPosition, Vector3.Down * 100.0f, CollisionMask);
			Debug.DrawRay(CenterPosition, Vector3.Down * 100.0f, groundHit ? Colors.Red : Colors.White);

			if (groundHit)
			{
				GlobalPosition -= groundHit.normal * (groundHit.distance - CollisionRadius); //Snap to ground
				LandOnGround();
			}
			else
				GD.Print("Couldn't find ground to snap to!");
		}
		#endregion

		#region Launchers and Jumps
		[Signal]
		public delegate void LaunchFinishedEventHandler();

		private float launcherTime;
		private Objects.Launcher activeLauncher;
		private Objects.LaunchData launchData;
		public void StartLauncher(Objects.LaunchData data, Objects.Launcher newLauncher = null, bool useAutoAlignment = false)
		{
			if (activeLauncher != null && activeLauncher == newLauncher) return; //Already launching that!

			ResetMovementState();
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Launcher;

			activeLauncher = newLauncher;
			launchData = data;

			ResetVelocity();

			IsOnGround = false;
			launcherTime = 0;

			CanJumpDash = false;
			Lockon.ResetLockonTarget();

			if (useAutoAlignment)
				MovementAngle = CalculateForwardAngle(launchData.launchDirection);
		}

		private void UpdateLauncher()
		{
			isCustomPhysicsEnabled = true;
			if (activeLauncher != null && !activeLauncher.IsCharacterCentered)
				GlobalPosition = activeLauncher.RecenterCharacter();
			else
			{
				Vector3 targetPosition = launchData.InterpolatePositionTime(launcherTime);
				float heightDelta = targetPosition.y - GlobalPosition.y;
				GlobalPosition = targetPosition;

				if (heightDelta < 0) //Only check ground when falling
					CheckGround();

				if (IsOnGround || launchData.IsLauncherFinished(launcherTime)) //Revert to normal state
				{
					FinishLauncher();
					MoveSpeed = launchData.HorizontalVelocity * .5f; //Prevent too much movement
					VerticalSpd = launchData.FinalVerticalVelocity;
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
			EmitSignal(SignalName.LaunchFinished);
		}

		public void JumpTo(Vector3 destination, float midHeight = 0f, bool relativeToEnd = false) //Generic JumpTo
		{
			Objects.LaunchData data = Objects.LaunchData.Create(GlobalPosition, destination, midHeight, relativeToEnd);
			StartLauncher(data);
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
		public float VerticalSpd { get; set; }
		/// <summary> Resets all speed values to zero </summary>
		private void ResetVelocity() => MoveSpeed = StrafeSpeed = VerticalSpd = 0;

		private void UpdatePhysics()
		{
			Lockon.UpdateLockonTargets();
			if (isCustomPhysicsEnabled) return; //When physics are handled in the state machine

			//Collision checks
			CheckGround();

			Vector3 movementDirection = GetMovementDirection();
			CheckMainWall(movementDirection);

			if (ActionState == ActionStates.JumpDash) //Jump dash ignores slopes
				movementDirection = movementDirection.RemoveVertical().Normalized();

			Velocity = movementDirection * MoveSpeed + movementDirection.Cross(UpDirection) * StrafeSpeed;
			Velocity += UpDirection * VerticalSpd;

			MoveAndSlide();
			CheckCeiling();

			PathFollower.Resync(); //Resync
			UpdateRecenter();
		}

		public bool IsOnGround { get; set; }
		public bool JustLandedOnGround { get; private set; } //Flag for doing stuff on land
		/// <summary> Disable ground snapping for a single frame. </summary>
		private bool disableGroundSnap;

		private const int GROUND_CHECK_AMOUNT = 8; //How many "whiskers" to use when checking the ground
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = CollisionRadius + COLLISION_PADDING;
			if (IsOnGround)
				castLength += Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta; //Atttempt to remain stuck to the ground when moving quickly
			else if (VerticalSpd < 0)
				castLength += Mathf.Abs(VerticalSpd) * PhysicsManager.physicsDelta;
			else if (disableGroundSnap)
			{
				castLength = -.1f; //Reduce snapping when jumping
				disableGroundSnap = false;
			}

			Vector3 castVector = -UpDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, CollisionMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!ValidateGroundCast(ref groundHit))
			{
				//Whisker casts (For slanted walls and ground)
				float interval = Mathf.Tau / GROUND_CHECK_AMOUNT;
				Vector3 castOffset = this.Forward() * (CollisionRadius - COLLISION_PADDING);
				for (int i = 0; i < GROUND_CHECK_AMOUNT; i++)
				{
					castOffset = castOffset.Rotated(UpDirection, interval).Normalized() * CollisionRadius;
					groundHit = this.CastRay(castOrigin + castOffset, castVector, CollisionMask, false, GetCollisionExceptions());
					Debug.DrawRay(castOrigin + castOffset, castVector, groundHit ? Colors.Red : Colors.White);
					if (ValidateGroundCast(ref groundHit)) break; //Found the floor
				}
			}

			if (groundHit) //Successful ground hit
			{
				if (!IsOnGround) //Landing on the ground
				{
					UpDirection = groundHit.normal;
					UpdateOrientation();
					LandOnGround();
				}
				else //Update world direction
					UpDirection = UpDirection.Lerp(groundHit.normal, .2f + .4f * GroundSettings.GetSpeedRatio(MoveSpeed)).Normalized();

				Effect.UpdateGroundType(groundHit.collidedObject);

				float snapDistance = groundHit.distance - CollisionRadius;
				if (JustLandedOnGround && Mathf.Abs(groundHit.normal.Dot(Vector3.Up)) < .9f) //Slanted ground fix
				{
					Vector3 offsetVector = groundHit.point - GlobalPosition;
					Vector3 axis = offsetVector.Cross(Vector3.Up).Normalized();
					if (axis.IsNormalized())
					{
						offsetVector = offsetVector.Rotated(axis, offsetVector.SignedAngleTo(Vector3.Up, axis));
						snapDistance = offsetVector.y;
					}
				}
				GlobalPosition -= groundHit.normal * snapDistance; //Snap to ground
				FloorMaxAngle = Mathf.Pi * .25f; //Allow KinematicBody to deal with slopes
				UpdateSlopeInfluence(groundHit.normal);
			}
			else
			{
				IsOnGround = false;

				//Smooth world direction based on vertical speed
				float orientationResetFactor = 0;
				if (ActionState == ActionStates.Stomping ||
				ActionState == ActionStates.JumpDash) //Quickly reset when stomping/homing attacking
					orientationResetFactor = .2f;
				else if (ActionState == ActionStates.Backflip)
				{
					float pathFollowerPitch = PathFollower.Forward().y;
					if (pathFollowerPitch >= -.4f) //Backflipping downhill; reset faster
						orientationResetFactor = Mathf.Clamp(pathFollowerPitch, .1f, .2f);
				}
				else if (VerticalSpd > 0)
					orientationResetFactor = .01f;
				else
					orientationResetFactor = (VerticalSpd * .2f / RuntimeConstants.MAX_GRAVITY) - .05f;

				FloorMaxAngle = 0; //Treat everything as a wall when in the air
				UpDirection = UpDirection.Lerp(Vector3.Up, Mathf.Clamp(orientationResetFactor, 0f, 1f)).Normalized();
			}
		}

		public void LandOnGround()
		{
			IsOnGround = true;
			VerticalSpd = 0;

			IsJumpClamped = false;
			CanJumpDash = false;
			IsGrindstepJump = false;
			isAccelerationJumpQueued = false;

			ResetActionState();
			Lockon.ResetLockonTarget();

			if (!IsCountdownActive && !IsRespawning) //Don't do this stuff during countdown or respawn
			{
				JustLandedOnGround = true;
				CheckLandingBoost(); //Landing boost skill
				Effect.PlayLandingFX();
			}
		}

		private bool ValidateGroundCast(ref RaycastHit hit) //Don't count walls as the ground
		{
			if (hit)
			{
				//Unless the collider is supposed to be both
				if (hit.collidedObject.IsInGroup("wall") && !hit.collidedObject.IsInGroup("floor"))
					hit = new RaycastHit();
				else if (hit.normal.AngleTo(UpDirection) > Mathf.Pi * .4f) //Limit angle collision
					hit = new RaycastHit();
				else if (!IsOnGround && hit.collidedObject.IsInGroup("wall")) //Be more strict on objects tagged as a wall
				{
					if (hit.normal.AngleTo(Vector3.Up) > Mathf.Pi * .1f)
						hit = new RaycastHit();
				}
			}

			return hit;
		}
		private bool ValidateWallCast(ref RaycastHit hit)
		{
			if (hit)
			{
				if (!hit.collidedObject.IsInGroup("wall") && !hit.collidedObject.IsInGroup("moveable"))
					hit = new RaycastHit();
			}

			return hit;
		}

		public void CheckCeiling() //Checks the ceiling.
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = CollisionRadius;
			if (VerticalSpd > 0)
				castLength += VerticalSpd * PhysicsManager.physicsDelta;

			Vector3 castVector = UpDirection * castLength;
			if (ActionState == ActionStates.Backflip) //Improve collision detection when backflipping
				castVector += GetMovementDirection() * MoveSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, CollisionMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GD.Print("Hit " + ceilingHit.collidedObject.Name);
				if (ceilingHit.collidedObject.IsInGroup("crusher") && IsOnGround) //Check if the player is being crushed
				{
					GD.Print($"Crushed by {ceilingHit.collidedObject.Name}");
					AddCollisionExceptionWith(ceilingHit.collidedObject); //Avoid clipping through the ground
					Knockback();
					return;
				}

				GlobalTranslate(ceilingHit.point - (CenterPosition + UpDirection * CollisionRadius));

				float maxVerticalSpeed = 0;
				if (ActionState == ActionStates.Backflip) //Fix backflipping into slanted ceilings
				{
					float ceilingAngle = ceilingHit.normal.AngleTo(Vector3.Down);
					if (ceilingAngle > Mathf.Pi * .1f)
					{
						//Use the dot product to determine the sign of the angle
						ceilingAngle *= Mathf.Sign(ceilingHit.normal.Flatten().Dot(castVector.Flatten()));
						maxVerticalSpeed = Mathf.Sin(ceilingAngle) * MoveSpeed;
					}
				}

				if (VerticalSpd > maxVerticalSpeed)
					VerticalSpd = maxVerticalSpeed;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (Mathf.IsZeroApprox(MoveSpeed)) //No movement
			{
				Debug.DrawRay(CenterPosition, castVector * CollisionRadius, Colors.White);
				return;
			}

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = CollisionRadius + COLLISION_PADDING + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;

			RaycastHit wallHit = this.CastRay(CenterPosition, castVector * castLength, CollisionMask, false, GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector * castLength, wallHit ? Colors.Red : Colors.White);

			if (ValidateWallCast(ref wallHit))
			{
				if (wallHit && ActionState != ActionStates.JumpDash && ActionState != ActionStates.Backflip)
				{
					float wallRatio = Mathf.Abs(wallHit.normal.Dot(castVector));
					if (wallRatio > .9f) //Running into wall head-on
					{
						if (Skills.IsSpeedBreakActive) //Cancel speed break
							Skills.ToggleSpeedBreak();

						if (wallHit.distance < CollisionRadius + COLLISION_PADDING)
							MoveSpeed = 0; //Kill speed
					}
					else //Reduce MoveSpd when moving against walls
					{
						float speedClamp = Mathf.Clamp(1.2f - wallRatio * .4f, 0f, 1f); //Arbitrary formula that works well
						MoveSpeed *= speedClamp;
					}
				}
			}
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

		private void UpdateOrientation() //Orientates Root to world direction, then rotates the gimbal on the y-axis
		{
			if (MovementState == MovementStates.External) return; //Externally controlled

			//Untested! This may end up breaking in certain scenarios
			GlobalRotation = Vector3.Zero;
			Vector3 cross = Vector3.Left.Rotated(Vector3.Up, UpDirection.Flatten().AngleTo(Vector2.Down));
			GlobalRotate(cross, -UpDirection.SignedAngleTo(Vector3.Up, cross));
		}
		#endregion

		/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs </summary>
		public float MovementAngle { get; set; }
		public Vector3 GetMovementDirection()
		{
			Vector3 pathFollowerForward = PathFollower.Forward().Rotated(UpDirection, PathFollower.DeltaAngle);

			if (Skills.IsSpeedBreakActive) //Follow pathfollower more accurately when speedbreaking
				return pathFollowerForward;

			/*
			if (!Camera.ActiveSettings.isRollEnabled) //Old method
			{
				Vector3 flatForward = this.Forward().Rotated(UpDirection, MovementAngle);
				return flatForward; //Normally this is good enough
			}
			*/

			//Tilted ground fix
			float fixAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			return PathFollower.Forward().Rotated(UpDirection, fixAngle);
		}

		//Gets the rotation of a given "forward" vector
		public float CalculateForwardAngle(Vector3 forwardDirection)
		{
			float dot = forwardDirection.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > .9f) //Moving vertically
			{
				float angle = new Vector2(forwardDirection.x + forwardDirection.z, forwardDirection.y).Angle();
				Vector3 axis = PathFollower.RightAxis; //Fallback
				if (IsOnGround)
					axis = forwardDirection.Cross(UpDirection).Normalized();

				forwardDirection = -forwardDirection.Rotated(axis, angle);
			}

			return forwardDirection.Flatten().Normalized().AngleTo(Vector2.Down);
		}

		#region Signals
		private bool IsCountdownActive => Interface.Countdown.IsCountdownActive;

		private float countdownBoostTimer;
		private readonly float COUNTDOWN_BOOST_WINDOW = .4f;

		private void UpdateCountdown()
		{
			if (Controller.actionButton.wasPressed)
				actionBufferTimer = 1f;
			actionBufferTimer -= PhysicsManager.physicsDelta;

			PathFollower.Resync();
			MovementAngle = PathFollower.ForwardAngle;

			Animator.SnapRotation(PathFollower.ForwardAngle);
			Animator.PlayCountdown();
		}

		public void OnCountdownLanded() => Effect.PlayLandingFX();

		public void OnCountdownFinished()
		{
			if (Skills.isCountdownBoostEnabled && actionBufferTimer > 0 && actionBufferTimer < COUNTDOWN_BOOST_WINDOW) //Successful starting boost
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
			Camera.ExternalController = null;

			actionBufferTimer = 0; //Reset action buffer from starting boost
		}

		private void OnLevelCompleted(bool _)
		{
			//Disable everything
			Lockon.IsMonitoring = false;
			Skills.IsTimeBreakEnabled = false;
			Skills.IsSpeedBreakEnabled = false;
		}

		public void OnObjectCollisionEnter(Node3D body)
		{
			/*
			Note for when I come back wondering why the player is being pushed through the floor
			Ensure all crushers' animationplayers are using the PHYSICS update mode
			If this is true, then proceed to panic.
			*/
			/*
			Old crusher check - Moved to CheckCeiling().
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're ACTUALLY being crushed and not just running into the side of the crusher
				float checkLength = CollisionRadius * 5f; //Needs to be long enough to guarantee hitting the target
				RaycastHit hit = this.CastRay(CenterPosition, UpDirection * checkLength, CollisionMask, false);
				if (hit.collidedObject == body)
				{
					GD.Print($"Crushed by {body.Name}");
					AddCollisionExceptionWith(body); //Avoid clipping through the ground
					Knockback();
				}
			}
			*/

			if (Lockon.IsHomingAttacking && body.IsInGroup("wall") && body.IsInGroup("splash jump"))
			{
				if (Skills.isSplashJumpEnabled) //Perform a splash jump
					Skills.SplashJump();
				else //Cancel HomingAttack/JumpDash
				{
					Lockon.IsHomingAttacking = false;
					ResetActionState();
				}
			}
		}

		public void OnObjectCollisionExit(Node3D body)
		{
			if (!(body is PhysicsBody3D)) return;

			if (body.IsInGroup("crusher") && GetCollisionExceptions().Contains(body as PhysicsBody3D))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
			}
		}
		#endregion

		//Components, rarely needs to be edited, so they go at the bottom of the inspector
		//All public so any object can get whatever player data they need
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
