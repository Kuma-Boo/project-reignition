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
		public StageSettings Stage => StageSettings.instance;

		public override void _EnterTree() => instance = this; //Override Singleton

		public override void _Ready()
		{
			CallDeferred(MethodName.ResetOrientation); //Start with proper orientation

			Stage.SetCheckpoint(GetParent<Node3D>()); //Initial checkpoint configuration
			Stage.Connect(StageSettings.SignalName.StageCompleted, new Callable(this, MethodName.OnStageCompleted));
			PathFollower.SetActivePath(Stage.StartingPath); //Attempt to autoload the stage's default path

			PhysicsManager.Instance.Connect(PhysicsManager.SignalName.PhysicsUpdated, new Callable(this, MethodName.SnapToGround), (uint)ConnectFlags.OneShot + (uint)ConnectFlags.Deferred);
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateStateMachine();
			UpdatePhysics();
			UpdateOrientation();

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
					EmitSignal(SignalName.ExternalControlFinished);
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
			useCustomPhysics = false;

			switch (MovementState)
			{
				case MovementStates.Normal:
					UpdateNormalState();
					break;
				case MovementStates.External:
					UpdateExternalControl();
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
			if (!allowNullInputs && Controller.MovementAxis.IsEqualApprox(Vector2.Zero))
				return false;

			float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle(), refAngle);
			return delta < Mathf.Pi * .4f;
		}

		/// <summary>
		/// Returns the target movement angle based on the camera view.
		/// </summary>
		public float GetTargetInputAngle()
		{
			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero)) //Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(Controller.MovementAxis.AngleTo(Vector2.Up)); //Target rotation angle (in radians)
		}

		private float GetTargetMovementAngle()
		{
			float inputAngle = GetTargetInputAngle();

			if (Skills.IsSpeedBreakActive)
			{
				return PathFollower.ForwardAngle;
			}
			else if (IsLockoutActive && CurrentLockoutData.overrideMode != LockoutResource.OverrideMode.Free)
			{
				float targetAngle = Mathf.DegToRad(CurrentLockoutData.overrideAngle);
				if (CurrentLockoutData.spaceMode == LockoutResource.SpaceMode.Camera)
					targetAngle = Camera.TransformAngle(targetAngle);
				else if (CurrentLockoutData.spaceMode == LockoutResource.SpaceMode.PathFollower)
					targetAngle = PathFollower.ForwardAngle + targetAngle;
				else if (CurrentLockoutData.spaceMode == LockoutResource.SpaceMode.Local)
					targetAngle += MovementAngle;

				if (CurrentLockoutData.allowReversing) //Check if we're trying to turn around
				{
					float dot = Mathf.Sign(ExtensionMethods.DotAngle(inputAngle, PathFollower.ForwardAngle));
					if (dot < 0) //Invert targetAngle when moving backwards
						targetAngle += Mathf.Pi;
				}

				return targetAngle;
			}

			return inputAngle;
		}

		private float jumpBufferTimer;
		private float actionBufferTimer;
		private const float ACTION_BUFFER_LENGTH = .2f; //How long to allow actions to be buffered
		private const float JUMP_BUFFER_LENGTH = .1f; //How long to allow actions to be buffered
		private void UpdateInputs()
		{
			if (MovementState == MovementStates.External) //Ignore inputs
			{
				jumpBufferTimer = 0;
				actionBufferTimer = 0;
				return;
			}

			if (IsLockoutActive && CurrentLockoutData.disableActions) return;

			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private float lockoutTimer;
		public LockoutResource CurrentLockoutData { get; private set; }
		public bool IsLockoutActive => CurrentLockoutData != null;
		private readonly List<LockoutResource> lockoutDataList = new List<LockoutResource>();

		/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
		public void AddLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource))
			{
				lockoutDataList.Add(resource); //Add the new lockout data
				if (lockoutDataList.Count >= 2) //List only needs to be sorted if there are multiple elements on it
					lockoutDataList.Sort(new LockoutResource.Comparer());

				if (CurrentLockoutData != null && CurrentLockoutData.priority == -1) //Remove current lockout?
					RemoveLockoutData(CurrentLockoutData);

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
			else if (CurrentLockoutData != lockoutDataList[lockoutDataList.Count - 1]) //Change to current data (Highest priority, last on the list)
			{
				SetLockoutData(lockoutDataList[lockoutDataList.Count - 1]);
				if (CurrentLockoutData.resetActions) //Reset actions if needed
					ResetActionState();
			}
		}

		private void SetLockoutData(LockoutResource resource)
		{
			CurrentLockoutData = resource;

			if (resource != null) //Reset flags
			{
				lockoutTimer = 0;
				isRecentered = false;
			}
		}

		private void UpdateLockoutTimer()
		{
			if (!IsLockoutActive || Mathf.IsZeroApprox(CurrentLockoutData.length))
				return;

			lockoutTimer = Mathf.MoveToward(lockoutTimer, CurrentLockoutData.length, PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(lockoutTimer, CurrentLockoutData.length))
				RemoveLockoutData(CurrentLockoutData);
		}

		private bool isRecentered; //Is the recenter complete?
		/// <summary> Recenters the player. Only call this AFTER movement has occurred. </summary>
		private void UpdateRecenter()
		{
			if (!IsLockoutActive || !CurrentLockoutData.recenterPlayer) return;

			Vector3 recenterDirection = PathFollower.Forward().Rotated(UpDirection, Mathf.Pi * .5f);
			float currentOffset = -PathFollower.PlayerPositionDelta.x;
			float movementOffset = currentOffset;
			if (!isRecentered) //Smooth out recenter speed
			{
				movementOffset = Mathf.MoveToward(movementOffset, 0, GroundSettings.GetSpeedRatio(MoveSpeed) * PhysicsManager.physicsDelta);
				if (Mathf.IsZeroApprox(movementOffset))
					isRecentered = true;
				movementOffset = currentOffset - movementOffset;
			}

			GlobalPosition += movementOffset * recenterDirection; //Move towards the pathfollower
		}
		#endregion

		#region External Control, Automation and Events
		private Vector3 externalOffset;
		private Node3D externalParent;
		private float externalSmoothing;

		[Signal]
		public delegate void ExternalControlFinishedEventHandler();
		public void StartExternal(Node3D followObject = null, float smoothing = 0f, bool allowSpeedBreak = false)
		{
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
		}

		public void UpdateExternalControl()
		{
			useCustomPhysics = true;
			externalOffset = externalOffset.Lerp(Vector3.Zero, externalSmoothing); //Smooth out entry

			if (externalParent != null)
				GlobalTransform = externalParent.GlobalTransform;

			GlobalPosition += externalOffset;
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

			UpdateMoveSpd();
			UpdateTurning();
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
		public bool IsMovingBackward { get; private set; }

		private const float TURN_SPEED = .1f; //How much to turn when moving slowly
		private const float TURNING_SPEED_LOSS = .04f; //How much speed to lose when turning sharply
		private const float MAX_TURN_SPEED = .2f; //How much to turn when moving at top speed
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_TURNAROUND_ANGLE = Mathf.Pi * .6f;
		/// <summary> Updates MoveSpd. What else do you need know? </summary>
		private void UpdateMoveSpd()
		{
			turnInstantly = Mathf.IsZeroApprox(MoveSpeed); //Store this so UpdateTurning works properly

			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Backflip) return;
			if (Skills.IsSpeedBreakActive) return; //Overridden to max speed

			float inputAngle = GetTargetInputAngle();
			float dot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, GetTargetMovementAngle()));
			float inputLength = GetMovementDirection().Length(); //Limits top speed; Modified depending on the LockoutResource.directionOverrideMode
			if (dot < .8f)
				inputLength *= dot;

			MovementResource activeMovementResource = GetActiveMovementSettings();

			if (IsLockoutActive && CurrentLockoutData.overrideSpeed)
			{
				//Override speed to the correct value
				float targetSpd = activeMovementResource.speed * CurrentLockoutData.speedRatio;
				if (Mathf.IsZeroApprox(CurrentLockoutData.tractionMultiplier)) //Snap speed (i.e. Dash Panels)
				{
					MoveSpeed = targetSpd;
					return;
				}

				float delta = PhysicsManager.physicsDelta;
				if (MoveSpeed <= targetSpd) //Accelerate using traction
					delta *= activeMovementResource.traction * CurrentLockoutData.tractionMultiplier;
				else //Slow down with friction
					delta *= activeMovementResource.friction * CurrentLockoutData.frictionMultiplier;
				MoveSpeed = Mathf.MoveToward(MoveSpeed, targetSpd, delta);
				return;
			}

			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero) || dot <= 0.1f) //Basic slow down
				MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, 0);
			else
			{
				float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);
				bool isTurningAround = deltaAngle > MAX_TURNAROUND_ANGLE;
				if (isTurningAround) //Skid to a stop
					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, -1);
				else
					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, inputLength); //Accelerate based on input strength
			}

			IsMovingBackward = MoveSpeed > 0 && ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.ForwardAngle) > MAX_TURNAROUND_ANGLE; //Moving backwards, limit speed
		}

		/// <summary> True when the player's MoveSpeed was zero </summary>
		private bool turnInstantly;
		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			float targetMovementAngle = GetTargetMovementAngle();
			bool overrideFacingDirection = Skills.IsSpeedBreakActive || (IsLockoutActive &&
			(CurrentLockoutData.overrideMode == LockoutResource.OverrideMode.Replace ||
			CurrentLockoutData.overrideMode == LockoutResource.OverrideMode.Strafe));

			//Strafe implementation
			if (Skills.IsSpeedBreakActive ||
			(IsLockoutActive && CurrentLockoutData.overrideMode == LockoutResource.OverrideMode.Strafe))
			{
				//Custom strafing movement
				float strafeAmount = ExtensionMethods.DotAngle(GetTargetInputAngle() + Mathf.Pi * .5f, PathFollower.ForwardAngle);
				strafeAmount *= Controller.MovementAxis.Length(); //Analog inputs

				//Reduce strafe speed when moving slowly
				strafeAmount *= (IsOnGround ? GroundSettings.GetSpeedRatioClamped(MoveSpeed) : AirSettings.GetSpeedRatioClamped(MoveSpeed)) + .1f;
				StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, strafeAmount);
			}

			if (overrideFacingDirection)
				MovementAngle = targetMovementAngle;

			StrafeSpeed = Skills.strafeSettings.Interpolate(StrafeSpeed, 0); //Reset strafe when not in use

			float speedRatio = IsOnGround ? GroundSettings.GetSpeedRatio(MoveSpeed) : AirSettings.GetSpeedRatio(MoveSpeed);
			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			float turnDelta = Mathf.Lerp(TURN_SPEED, MAX_TURN_SPEED, speedRatio);

			if (ActionState == ActionStates.Backflip) return;
			if (!turnInstantly && deltaAngle > MAX_TURNAROUND_ANGLE) return; //Turning around

			if (turnInstantly) //Instantly set movement angle to target movement angle
			{
				turningVelocity = 0;
				MovementAngle = targetMovementAngle;
			}
			if (!IsLockoutActive || !CurrentLockoutData.overrideSpeed) //Don't apply turning speed loss when overriding speed
			{
				//Calculate turn delta, relative to ground speed
				float speedLossRatio = deltaAngle / MAX_TURNAROUND_ANGLE;
				MoveSpeed -= GroundSettings.speed * speedRatio * turningSpeedCurve.Sample(speedLossRatio) * TURNING_SPEED_LOSS;
				if (MoveSpeed < 0)
					MoveSpeed = 0;
			}

			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, turnDelta);
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
			if (IsLockoutActive && CurrentLockoutData.ignoreSlopes) return; //Lockout is ignoring slopes

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
				if (CurrentLockoutData.resetOnLand && JustLandedOnGround) //Cancel lockout
					RemoveLockoutData(CurrentLockoutData);
				else if (CurrentLockoutData.disableActions)
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

				if (IsHoldingDirection(PathFollower.BackAngle) && (!IsLockoutActive || CurrentLockoutData.allowReversing))
					StartBackflip();
				else
					Jump();
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
		private bool isJumpClamped; //True after the player releases the jump button
		private bool isAccelerationJump;
		private float currentJumpTime; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .04f; //How fast the jump button needs to be released for an "acceleration jump"
		public void Jump(bool disableAccelerationJump = default)
		{
			currentJumpTime = disableAccelerationJump ? ACCELERATION_JUMP_LENGTH + PhysicsManager.physicsDelta : 0;
			isJumpClamped = false;
			IsOnGround = false;
			CanJumpDash = true;
			canLandingBoost = Skills.isLandingDashEnabled;
			ActionState = ActionStates.Jumping;
			VerticalSpd = RuntimeConstants.GetJumpPower(jumpHeight);
			Sound.PlayActionSFX("jump");

			if (IsMovingBackward) //Kill speed when jumping backwards
				MoveSpeed = 0;
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpTime >= ACCELERATION_JUMP_LENGTH) //Acceleration jump?
			{
				if (IsHoldingDirection(PathFollower.ForwardAngle, true) && Controller.MovementAxis.Length() > .5f)
				{
					ActionState = ActionStates.AccelJump;
					MoveSpeed = Skills.accelerationJumpSpeed;
					//MovementAngle = ExtensionMethods.ClampAngleRange(GetTargetInputAngle(), PathFollower.ForwardAngle, Mathf.Pi * .5f);
				}

				VerticalSpd = 5f; //Consistant accel jump height
				isAccelerationJump = false; //Stop listening for an acceleration jump
			}

			if (!isJumpClamped)
			{
				if (!Controller.jumpButton.isHeld)
				{
					isJumpClamped = true;
					if (currentJumpTime <= ACCELERATION_JUMP_LENGTH) //Listen for acceleration jump
						isAccelerationJump = true;
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

			MoveSpeed = jumpDashSpeed;
			CanJumpDash = false; //Don't use get/set so we keep our target monitoring.
			ActionState = ActionStates.JumpDash;
			Sound.PlayActionSFX("jump dash");

			if (Lockon.LockonTarget == null) //Normal jumpdash
				VerticalSpd = jumpDashPower;
			else
				Lockon.HomingAttack(); //Start Homing attack
		}

		private void UpdateJumpDash()
		{
			if (Lockon.IsHomingAttacking) //Homing attack
			{
				if (Lockon.LockonTarget == null) //Target disappeared. Transition to jumpdash
				{
					Lockon.IsHomingAttacking = false;
					StartJumpDash();
					return;
				}

				useCustomPhysics = true;
				VerticalSpd = 0;
				Velocity = Lockon.HomingAttackDirection.Normalized() * Lockon.homingAttackSpeed;
				MoveAndSlide();
				PathFollower.Resync();
			}
			else //Normal Jump dash; Apply gravity
				VerticalSpd = Mathf.MoveToward(VerticalSpd, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);

			CheckStomp();
		}
		#endregion

		#region Crouch
		private const float SLIDE_FRICTION = 8f;
		private void StartCrouching()
		{
			ActionState = ActionStates.Crouching;
		}

		private void UpdateCrouching()
		{
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, SLIDE_FRICTION * PhysicsManager.physicsDelta);

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
			if (actionBufferTimer != 0) //Stomp
			{
				actionBufferTimer = 0;
				ResetVelocity();

				canLandingBoost = true;
				Lockon.ResetLockonTarget();
				Lockon.IsMonitoring = false;

				ActionState = ActionStates.Stomping;
			}
		}
		#endregion

		#region Backflip
		[Export]
		public float backflipSpeed;
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
			}

			if (IsOnGround)
				ResetActionState();
		}

		private void StartBackflip()
		{
			CanJumpDash = true;
			MoveSpeed = backflipSpeed;
			MovementAngle = GetTargetInputAngle();
			VerticalSpd = RuntimeConstants.GetJumpPower(backflipHeight);
			Sound.PlayActionSFX("jump");

			IsOnGround = false;
			ActionState = ActionStates.Backflip;
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
		public delegate void DamagedEventHandler(Node3D n); //This signal is called anytime a hitbox collides with the player, regardless of invincibilty.

		/// <summary>
		/// Called when the player takes damage.
		/// </summary>
		public void TakeDamage(Node3D node)
		{
			EmitSignal(SignalName.Damaged, node);

			if (IsInvincible) return; //Don't take damage.

			ActionState = ActionStates.Damaged;
			invincibliltyTimer = INVINCIBILITY_LENGTH;

			if (MovementState == MovementStates.Normal)
				Knockback(true);
		}

		/// <summary>
		/// Knocks the player backwards.
		/// </summary>
		public void Knockback(bool damaged)
		{
			//TODO add support for being knocked forwards

			IsOnGround = false;
			MoveSpeed = -4f;
			VerticalSpd = 4f;
			//TODO Play hurt animation
		}

		/// <summary>
		/// Called when the player loses a life, be it from falling or getting hit without rings.
		/// </summary>
		public void Kill()
		{
			//TODO Check deathless mission modifier/Play death animation
			StartRespawn();
		}

		public bool IsRespawning { get; private set; } //Is the player currently respawning?
		private void StartRespawn() //Fade screen out, enable respawn flag, and connect signals
		{
			IsRespawning = true;
			TransitionManager.StartTransition(new TransitionData()
			{
				inSpeed = .5f,
				outSpeed = .5f,
				color = Colors.Black
			});

			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.ProcessRespawn), (uint)ConnectFlags.OneShot);
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.FinishRespawn), (uint)ConnectFlags.OneShot);
		}

		/// <summary>
		/// Warp the player to the previous checkpoint and revert any actions.
		/// </summary>
		private void ProcessRespawn()
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			GlobalPosition = Stage.CurrentCheckpoint.GlobalPosition;
			PathFollower.SetActivePath(Stage.CheckpointPath); //Revert path
			ResetVelocity();
			ResetOrientation();

			Camera.ResetFlag = true;
			Stage.RespawnObjects(true); //Respawn Objects

			CallDeferred(MethodName.SnapToGround);

			//TODO Play respawn animation/sfx
			TransitionManager.FinishTransition();
		}

		/// <summary>
		/// Force the player onto the ground by setting VerticalSpeed to a high value, then immediately checking the ground.
		/// </summary>
		private void SnapToGround()
		{
			MoveAndCollide(Vector3.Down * 1000);
			CheckGround();

			if (!IsOnGround)
			{
				VerticalSpd = 0f;
				GD.Print("Couldn't find ground to snap to!");
			}
		}

		/// <summary>
		/// Disable respawn flags and allow the game to continue on normally
		/// </summary>
		private void FinishRespawn()
		{
			IsRespawning = false;
		}
		#endregion

		#region Launchers and Jumps
		[Signal]
		public delegate void LauncherFinishedEventHandler();

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
			useCustomPhysics = true;
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
			if (activeLauncher != null && !IsOnGround)
				CanJumpDash = activeLauncher.allowJumpDashing;

			ResetMovementState();
			activeLauncher = null;
			EmitSignal(SignalName.LauncherFinished);
		}

		public void JumpTo(Vector3 destination, float midHeight = 0f, bool relativeToEnd = false) //Generic JumpTo
		{
			Objects.LaunchData data = Objects.LaunchData.Create(GlobalPosition, destination, midHeight, relativeToEnd);
			StartLauncher(data);
		}
		#endregion

		#region Physics
		[Export]
		private CollisionShape3D environmentCollider;
		public bool IsEnvironmentColliderEnabled
		{
			get => !environmentCollider.Disabled;
			set => environmentCollider.Disabled = !value;
		}

		/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs </summary>
		public float MovementAngle { get; set; }
		public Vector3 GetMovementDirection() => this.Forward().Rotated(UpDirection, MovementAngle);

		public float MoveSpeed { get; set; } //Character's primary movement speed.
		public float StrafeSpeed { get; set; } //Used during speed break, etc. 
		public float VerticalSpd { get; set; } //Used for jumping and falling

		private void ResetVelocity() //Resets all speed values to zero
		{
			MoveSpeed = StrafeSpeed = VerticalSpd = 0;
		}

		private bool useCustomPhysics; //TRUE whenever external objects are overridding physics
		private void UpdatePhysics()
		{
			Lockon.UpdateLockonTargets();
			if (useCustomPhysics) return; //When physics are handled in the state machine

			Vector3 movementDirection = GetMovementDirection();

			//Collision checks
			CheckGround();
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

		public Vector3 CenterPosition
		{
			get => GlobalPosition + UpDirection * COLLISION_RADIUS; //Center of collision calculations
			set => GlobalPosition = value - UpDirection * COLLISION_RADIUS;
		}
		private const int GROUND_CHECK_AMOUNT = 8; //How many "whiskers" to use when checking the ground
		private const float COLLISION_RADIUS = .3f;
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS + COLLISION_PADDING;
			if (IsOnGround)
				castLength += MoveSpeed * PhysicsManager.physicsDelta; //Atttempt to remain stuck to the ground when moving quickly
			else if (VerticalSpd < 0)
				castLength += Mathf.Abs(VerticalSpd) * PhysicsManager.physicsDelta;
			else if (VerticalSpd > 0)
				castLength = -.1f; //Reduce snapping when moving upwards

			Vector3 castVector = -UpDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, CollisionMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!ValidateGroundCast(ref groundHit))
			{
				//Whisker casts (For slanted walls and ground)
				float interval = Mathf.Tau / GROUND_CHECK_AMOUNT;
				Vector3 castOffset = this.Forward() * (COLLISION_RADIUS - COLLISION_PADDING);
				for (int i = 0; i < GROUND_CHECK_AMOUNT; i++)
				{
					castOffset = castOffset.Rotated(UpDirection, interval).Normalized() * COLLISION_RADIUS;
					groundHit = this.CastRay(castOrigin + castOffset, castVector, CollisionMask);
					Debug.DrawRay(castOrigin + castOffset, castVector, groundHit ? Colors.Red : Colors.White);
					if (ValidateGroundCast(ref groundHit)) break; //Found the floor
				}
			}

			if (groundHit) //Successful ground hit
			{
				if (!IsOnGround) //Landing on the ground
				{
					IsOnGround = true;
					VerticalSpd = 0;

					isJumpClamped = false;
					CanJumpDash = false;
					isAccelerationJump = false;
					JustLandedOnGround = true;

					ResetActionState();
					Lockon.ResetLockonTarget();
					UpDirection = groundHit.normal;
					UpdateOrientation();

					if (!IsCountdownActive && !IsRespawning) //Don't do this stuff during countdown
					{
						Sound.PlayLandingSFX();
						CheckLandingBoost(); //Landing boost skill
					}
				}
				else //Update world direction
					UpDirection = UpDirection.Lerp(groundHit.normal, .2f).Normalized();

				Sound.UpdateGroundType(groundHit.collidedObject);
				GlobalPosition -= groundHit.normal * (groundHit.distance - COLLISION_RADIUS); //Snap to ground
				FloorMaxAngle = Mathf.Pi * .25f; //Allow KinematicBody to deal with slopes
				UpdateSlopeInfluence(groundHit.normal);
			}
			else
			{
				IsOnGround = false;
				//Smooth world direction based on vertical speed

				float orientationResetFactor = 0;
				if (VerticalSpd > 0)
					orientationResetFactor = .1f;
				else
					orientationResetFactor = (VerticalSpd / RuntimeConstants.MAX_GRAVITY) - .15f;

				FloorMaxAngle = 0; //Treat everything as a wall when in the air
				UpDirection = UpDirection.Lerp(Vector3.Up, Mathf.Clamp(orientationResetFactor, 0f, 1f)).Normalized();
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


		private void CheckCeiling() //Checks the ceiling.
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS;

			Vector3 castVector = UpDirection * castLength;
			if (VerticalSpd > 0)
				castVector.y += VerticalSpd * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, CollisionMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GlobalTranslate(ceilingHit.point - (CenterPosition + UpDirection * COLLISION_RADIUS));

				if (VerticalSpd > 0)
					VerticalSpd = 0;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (Mathf.IsZeroApprox(MoveSpeed)) //No movement
			{
				Debug.DrawRay(CenterPosition, castVector * COLLISION_RADIUS, Colors.White);
				return;
			}

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;

			RaycastHit wallHit = this.CastRay(CenterPosition, castVector * castLength, CollisionMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector * castLength, wallHit ? Colors.Red : Colors.White);

			if (ValidateWallCast(ref wallHit))
			{
				if (wallHit && ActionState != ActionStates.JumpDash)
				{
					float wallRatio = Mathf.Abs(wallHit.normal.Dot(castVector));
					if (wallRatio > .9f) //Running into wall head-on
					{
						if (Skills.IsSpeedBreakActive) //Cancel speed break
							Skills.ToggleSpeedBreak();

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

		private const float COLLISION_PADDING = .1f;
		private const float ORIENTATION_SMOOTHING = .4f;
		private void ResetOrientation()
		{
			UpDirection = Vector3.Up;

			if (Stage.CurrentCheckpoint == null) //Default to parent node's position
				Transform = Transform3D.Identity;
			else
				GlobalTransform = Stage.CurrentCheckpoint.GlobalTransform;

			PathFollower.Resync(); //Update path follower
			MovementAngle = PathFollower.ForwardAngle; //Reset movement angle

			//TODO Re-sync visual rotations
		}

		private void UpdateOrientation() //Orientates Root to world direction, then rotates the gimbal on the y-axis
		{
			if (MovementState == MovementStates.External) return; //Externally controlled

			Transform3D t = GlobalTransform;
			t.basis.z = Vector3.Back;
			t.basis.y = UpDirection;
			if (Mathf.Abs(t.basis.z.Dot(t.basis.y)) > .9f) //BUGFIX Prevent player blipping out of existence when on 90 wall
				t.basis.z = Vector3.Up;
			t.basis.x = -t.basis.z.Cross(t.basis.y);
			t.basis = t.basis.Orthonormalized();
			GlobalTransform = t;
		}
		#endregion

		//Gets the rotation of a given "forward" vector
		public static float CalculateForwardAngle(Vector3 forwardDirection)
		{
			float dot = forwardDirection.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > .9f) //Moving vertically
			{
				Vector3 upDirection = instance.UpDirection.RemoveVertical().Normalized();
				return forwardDirection.SignedAngleTo(Vector3.Up * Mathf.Sign(dot), upDirection);
			}

			return forwardDirection.Flatten().AngleTo(Vector2.Down);
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
		}

		public void CountdownCompleted()
		{
			if (Skills.isCountdownBoostEnabled && actionBufferTimer > 0 && actionBufferTimer < COUNTDOWN_BOOST_WINDOW) //Successful starting boost
			{
				MoveSpeed = Skills.countdownBoostSpeed;
				GD.Print("Successful countdown boost");
			}

			actionBufferTimer = 0; //Reset action buffer from starting boost
		}

		private void OnStageCompleted(bool _)
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
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're ACTUALLy being crushed and not just running into the side of the crusher
				float checkLength = COLLISION_RADIUS * 5f; //Needs to be long enough to guarantee hitting the target
				RaycastHit hit = this.CastRay(CenterPosition, UpDirection * checkLength, CollisionMask, false);
				if (hit.collidedObject == body)
				{
					GD.Print($"Crushed by {body.Name}");
					AddCollisionExceptionWith(body); //Avoid clipping through the ground
					TakeDamage(body);
				}
			}

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
		public CharacterSound Sound { get; private set; }
		[Export]
		public CharacterSkillManager Skills { get; private set; }
		[Export]
		public CharacterLockon Lockon { get; private set; }
	}
}
