using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for handling the player's state, physics and basic movement.
	/// </summary>
	public class CharacterController : KinematicBody
	{
		public static CharacterController instance;

		//Component controllers
		[Export]
		public NodePath pathFollower;
		public CharacterPathFollower PathFollower { get; private set; }
		[Export]
		public NodePath animator;
		public CharacterAnimator Animator { get; private set; }
		[Export]
		public NodePath sound;
		public CharacterSound Sound { get; private set; }
		[Export]
		public NodePath soul;
		public CharacterSoulSkill Soul { get; private set; }
		[Export]
		public NodePath lockon;
		public CharacterLockon Lockon { get; private set; }
		[Export]
		public NodePath gimbal; //Responsible for y-axis rotation
		public Spatial _gimbal;
		public CameraController Camera { get; set; } //Camera is set externally

		public override void _EnterTree()
		{
			instance = this;

			PathFollower = GetNode<CharacterPathFollower>(pathFollower);
			Animator = GetNode<CharacterAnimator>(animator);
			Sound = GetNode<CharacterSound>(sound);
			Soul = GetNode<CharacterSoulSkill>(soul);
			Lockon = GetNode<CharacterLockon>(lockon);

			_gimbal = GetNode<Spatial>(gimbal);
			_environmentCollider = GetNode<CollisionShape>(environmentCollider);

			ResetOrientation = true; //Start with proper orientation

			if (StageSettings.instance != null)
				StageSettings.instance.Connect(nameof(StageSettings.StageCompleted), this, nameof(OnStageCompleted));
		}

		[Signal]
		public delegate void PlayerProcessed(); //Called every frame after the player is done processing

		public override void _PhysicsProcess(float _)
		{
			ProcessStateMachine();

			UpdatePhysics();

			UpdateOrientation();
			Animator.UpdateAnimation();
			Soul.UpdateSoulSkills();

			EmitSignal(nameof(PlayerProcessed));
		}

		#region State Machine
		public MovementStates MovementState { get; private set; }
		public enum MovementStates
		{
			Normal, //Standard on rails movement
			External, //Cutscenes, and stage objects that override player control
			Sidle, //Scooting along the wall
			Grinding, //Grinding on rails
			Launcher, //Springs, Ramps, etc.
		}

		public void ResetMovementState()
		{
			switch (MovementState)
			{
				case MovementStates.Grinding:
					StopGrinding();
					break;
				case MovementStates.External:
					EmitSignal(nameof(ExternalControlCompleted));
					break;
			}

			enableLandingBoost = false; //Disable landing boost temporarily
			CancelMovementState(MovementState);
			Soul.IsSpeedBreakEnabled = Soul.IsTimeBreakEnabled = true; //Reenable soul skills
		}

		public void CancelMovementState(MovementStates fromState) //Reset state to Normal
		{
			if (MovementState == fromState)
				MovementState = MovementStates.Normal;
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

			//State specific
			Hanging, //Hanging from a ledge (In Sidle Mode)
		}

		public void ResetActionState() //Reset action state
		{
			switch (ActionState)
			{
				default:
					break;
			}

			ActionState = ActionStates.Normal;
		}

		private void ProcessStateMachine()
		{
			if (isCountdownActive) return;
			UpdateInputs();

			customPhysicsEnabled = false;

			switch (MovementState)
			{
				case MovementStates.Normal:
					UpdateNormalState();
					break;
				case MovementStates.External:
					UpdateExternalControl();
					break;
				case MovementStates.Sidle:
					UpdateSidle();
					break;
				case MovementStates.Launcher:
					UpdateLauncher();
					break;
				case MovementStates.Grinding:
					UpdateGrinding();
					break;
			}

			UpdateInvincibility();
			UpdateControlLockTimer();
		}
		#endregion

		#region Controls
		public InputManager.Controller Controller => InputManager.controller;

		public bool isSideScroller; //Are we in a 2D section?
		public bool isFacingRight; //Determines which way on the controller is back (Only in sidescroller)
		private float GetMovementInputValue()
		{
			if (!isSideScroller)
				return rotatedMovementValue.y;

			return isFacingRight ? Controller.horizontalAxis.value : -Controller.horizontalAxis.value; //Ignore camera influence when in sidescroller
		}
		public float GetStrafeInputValue()
		{
			//Returns 1 for moving right, -1 for moving left
			if (!isSideScroller)
				return rotatedMovementValue.x;

			return 0; //No strafe when sidescrolling
		}

		private Vector2 rotatedMovementValue; //MovementDirection, relative to camera
		private float inputRotationAmount;

		private bool IsHoldingForward()
		{
			if (isSideScroller)
				return (Controller.horizontalAxis.value > 0 && isFacingRight) || (Controller.horizontalAxis.value < 0 && !isFacingRight);

			return rotatedMovementValue.y > 0.6f && Mathf.Abs(rotatedMovementValue.x) < .8f;
		}

		private bool IsHoldingBack()
		{
			if(isSideScroller)
				return (Controller.horizontalAxis.value > 0 && !isFacingRight) || (Controller.horizontalAxis.value < 0 && isFacingRight);

			return rotatedMovementValue.y < -0.6f && Mathf.Abs(rotatedMovementValue.x) < .8f;
		}

		private float jumpBufferTimer;
		private float actionBufferTimer;
		private const float ACTION_BUFFER_LENGTH = .2f; //How long to allow actions to be buffered
		private const float JUMP_BUFFER_LENGTH = .1f; //How long to allow actions to be buffered

		private void UpdateInputs()
		{
			if(Camera != null) //Calculate movement value
			{
				inputRotationAmount = -PathFollower.ForwardDirection.Flatten().Normalized().AngleTo(Vector2.Up.Rotated(Camera.ViewAngle));
				rotatedMovementValue = Controller.MovementAxis.Rotated(inputRotationAmount); //Input angle based off view angle, in character space

				//Software Deadzone
				if (Mathf.Abs(rotatedMovementValue.x) < .2f)
					rotatedMovementValue.x = 0f;
				if (Mathf.Abs(rotatedMovementValue.y) < .2f)
					rotatedMovementValue.y = 0f;
			}

			if (MovementState == MovementStates.External) //Ignore inputs
			{
				jumpBufferTimer = 0;
				actionBufferTimer = 0;
				return;
			}
			
			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private bool isCountdownActive;
		public void CountdownStarted()
		{
			isCountdownActive = true;
			Animator.Countdown();
		}
		public void CountdownCompleted() => isCountdownActive = false;

		private void OnStageCompleted(bool _)
		{
			//Disable everything
			Lockon.IsMonitoring = false;
			Soul.IsTimeBreakEnabled = false;
			Soul.IsSpeedBreakEnabled = false;

			StartControlLockout(StageSettings.instance.CompletionControlLockout);
		}

		private bool isControlsLocked;
		private float controlLockoutTimer;
		public ControlLockoutResource ControlLockoutData { get; private set; }

		public void ResetControlLockout()
		{
			controlLockoutTimer = 0;
			isControlsLocked = false;
		}
		public void StartControlLockout(ControlLockoutResource data)
		{
			if(data == null)
			{
				ResetControlLockout();
				return;
			}

			if (isControlsLocked && ControlLockoutData.priority > data.priority) return; //Already using a lockout with higher priority

			isRecentered = false; //Just in case
			isControlsLocked = true;
			ControlLockoutData = data;
			controlLockoutTimer = data.length;

			if (ControlLockoutData.resetActionState)
				ResetActionState();
		}

		private void UpdateControlLockTimer()
		{
			if (!isControlsLocked || controlLockoutTimer <= 0)
				return;

			controlLockoutTimer = Mathf.MoveToward(controlLockoutTimer, 0, PhysicsManager.physicsDelta);
			if (controlLockoutTimer == 0)
				ResetControlLockout();
		}
		#endregion

		#region External Control, Automation and Events
		private Vector3 externalOffset;
		private Spatial externalParent;

		[Signal]
		public delegate void ExternalControlCompleted();
		public void StartExternal(Spatial followObject = null, bool snap = false)
		{
			ResetMovementState();
			MovementState = MovementStates.External;
			ActionState = ActionStates.Normal;

			externalParent = followObject;
			externalOffset = Vector3.Zero; //Reset offset
			if (externalParent != null && !snap) //Smooth out transition
				externalOffset = GlobalTranslation - externalParent.GlobalTranslation;

			ResetVelocity();
			UpdateExternalControl();
		}

		public void UpdateExternalControl()
		{
			customPhysicsEnabled = true;
			externalOffset = externalOffset.LinearInterpolate(Vector3.Zero, .2f); //Smooth out entry

			if (externalParent != null)
				GlobalTransform = externalParent.GlobalTransform;

			GlobalTranslation += externalOffset;
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

			UpdateFreeMovement();
			UpdateMoveSpeed();
			UpdateStrafeSpeed();
			UpdateActions();
		}

		[Export]
		public MovementResource moveSettings;
		[Export]
		public MovementResource airMoveSettings;
		[Export]
		public MovementResource runningStrafeSettings;
		[Export]
		public MovementResource airStrafeSettings;
		[Export]
		public MovementResource backstepSettings;

		public bool IsFreeMovementActive { get; private set; } //Can the player rotate freely?
		public float FreeMoveSpeed { get; set; } //Move speed for free movement
		private bool IsJumpDashing => ActionState == ActionStates.JumpDash || ActionState == ActionStates.AccelJump;
		private Vector2 freeMoveDirection; //Current movement direction
		private const float FREE_MOVE_TURNAROUND_ANGLE = 2f; //At what angle do inputs register as turning around?
		private void UpdateFreeMovement() //Allows the player to turn freely, similar to Sonic Adventure. Helps with more open areas
		{
			//Calculate whether the player is turning freely
			if (isControlsLocked) //Locked controls are handled with the UpdateMoveSpeed and UpdateStrafeSpeed
				SetFreeMovement(false);
			else if (MoveSpeed <= 0 && IsHoldingBack()) //Backstepping always overrides Adventure-styled movement
				SetFreeMovement(false);
			else if (MoveSpeed >= 0) //Forward movement
			{
				if (!IsFreeMovementActive && moveSettings.GetSpeedRatio(MoveSpeed) < .6f)  //Free rotational movement if speed drops below 60%, or we're accelerating
					SetFreeMovement(true);
				else if (moveSettings.GetSpeedRatio(MoveSpeed) > .8f) //Stop using free movement when moving forward quickly
					SetFreeMovement(false);
			}

			if (!IsFreeMovementActive) return; //Player is confined to a path

			//360ish movement, similar to the original Adventure games
			float angle = freeMoveDirection.AngleTo(rotatedMovementValue);
			MovementResource activeSettings = IsOnGround ? moveSettings : airMoveSettings;

			if (rotatedMovementValue == Vector2.Zero || IsJumpDashing)
				FreeMoveSpeed = activeSettings.Interpolate(FreeMoveSpeed, 0f); //Slow down
			else if (Mathf.IsZeroApprox(FreeMoveSpeed) || Mathf.Abs(angle) < FREE_MOVE_TURNAROUND_ANGLE)
			{
				if (Mathf.IsZeroApprox(FreeMoveSpeed)) //Snap rotation
					freeMoveDirection = rotatedMovementValue.Normalized();
				else //Rotate smoothly
					freeMoveDirection = freeMoveDirection.Rotated(angle * .2f * (1f - SpeedRatio)); //Lerp turning (Higher speeds turn slower)

				float input = Mathf.Abs(rotatedMovementValue.Length() * (1f - (Mathf.Abs(angle) / FREE_MOVE_TURNAROUND_ANGLE)));
				FreeMoveSpeed = activeSettings.Interpolate(FreeMoveSpeed, input);
			}
			else
				FreeMoveSpeed = activeSettings.Interpolate(FreeMoveSpeed, -1f); //Turn around

			freeMoveDirection = freeMoveDirection.Normalized(); //Normalize movement direction

			if (FreeMoveSpeed < 0) //FreeMoveSpeed cannot be negative
				FreeMoveSpeed = 0f;
			SetFreeMoveSpeed(FreeMoveSpeed);
		}

		private void SetFreeMovement(bool value)
		{
			IsFreeMovementActive = value;

			if (IsFreeMovementActive) //Sync variables
			{
				Vector2 movementVector = new Vector2(StrafeSpeed, MoveSpeed);

				if (!movementVector.IsEqualApprox(Vector2.Zero))
				{
					if (!IsJumpDashing)
						freeMoveDirection = movementVector;
					else if (freeMoveDirection.y < 0) //Disable jumpdashing backwards
						freeMoveDirection.y = 0;
				}
				else if (freeMoveDirection.IsEqualApprox(Vector2.Zero))
					freeMoveDirection = Vector2.Down;

				freeMoveDirection = freeMoveDirection.Normalized();
				SetFreeMoveSpeed(movementVector.Length());
			}
			else
				freeMoveDirection = Vector2.Down;
		}

		private void SetFreeMoveSpeed(float spd)
		{
			FreeMoveSpeed = spd;
			StrafeSpeed = FreeMoveSpeed * freeMoveDirection.x;
			MoveSpeed = FreeMoveSpeed * freeMoveDirection.y;
		}

		private void UpdateMoveSpeed()
		{
			if (IsCrouching) return;
			if (Soul.IsSpeedBreakActive) return; //Overridden to max speed

			if (isControlsLocked && ControlLockoutData.overrideSpeed)
			{
				if (Mathf.IsZeroApprox(ControlLockoutData.tractionRatio)) //No traction, keep current speed
					return;

				//Change speed to the correct value
				float spd = moveSettings.speed * ControlLockoutData.speedRatio;

				if(ControlLockoutData.tractionRatio < 0) //Negative traction, snap speed
				{
					MoveSpeed = spd;
					return;
				}

				float delta = PhysicsManager.physicsDelta;
				if (MoveSpeed < spd) //Accelerate using traction
					delta *= moveSettings.traction * ControlLockoutData.tractionRatio;
				else //Slow down with friction
					delta *= moveSettings.friction * ControlLockoutData.frictionRatio;

				MoveSpeed = Mathf.MoveToward(MoveSpeed, spd, delta);
				return;
			}

			if (IsFreeMovementActive)
				return;

			if (IsOnGround)
			{
				if (MoveSpeed >= 0)
				{
					if (IsHoldingForward() && SpeedRatio > .8f) //Fix slowing down when turning at high speeds
						MoveSpeed = moveSettings.Interpolate(MoveSpeed, 1f);
					else
					{
						if (Mathf.Abs(StrafeSpeed) > MoveSpeed) //Slow speed turning
							MoveSpeed = Mathf.MoveToward(MoveSpeed, Mathf.Abs(StrafeSpeed), runningStrafeSettings.traction * .5f * GetMovementInputValue() * PhysicsManager.physicsDelta);

						MoveSpeed = moveSettings.Interpolate(MoveSpeed, GetMovementInputValue());
						UpdateSlopeMoveSpeed();
					}
				}
				else
					MoveSpeed = backstepSettings.Interpolate(MoveSpeed, -GetMovementInputValue()); //Input direction needs to be inverted for negative movement
			}
			else
				MoveSpeed = airMoveSettings.Interpolate(MoveSpeed, GetMovementInputValue());
		}

		private void UpdateSlopeMoveSpeed()
		{
			if (Mathf.Abs(slopeInfluence) < SLOPE_DEADZONE) return; //Slope is too shallow

			if (GetMovementInputValue() > 0f) //Accelerating
			{
				if (slopeInfluence < 0f) //Downhill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, moveSettings.speed, moveSettings.traction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta);
				else if (SpeedRatio < 1f) //Uphill; Reduce acceleration
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, moveSettings.traction * slopeInfluence * PhysicsManager.physicsDelta);
			}
			else if (MoveSpeed > 0f)
			{
				if (slopeInfluence < 0f) //Re-apply some speed when moving downhill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, moveSettings.speed, moveSettings.friction * Mathf.Abs(slopeInfluence) * PhysicsManager.physicsDelta);
				else //Increase friction when moving uphill
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, moveSettings.friction * slopeInfluence * PhysicsManager.physicsDelta);
			}
		}

		private void UpdateStrafeSpeed()
		{
			if (MoveSpeed == 0 && IsCrouching) return;			
			if(isSideScroller) return; //No strafing in a sidescroller.

			if (isControlsLocked && ControlLockoutData.strafeSettings != ControlLockoutResource.StrafeSettings.Default)
			{
				if (ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.Recenter)
					RecenterStrafe();
				else if (ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.KeepPosition)
					StrafeSpeed = 0f;

				return;
			}

			if(IsFreeMovementActive)
				return;

			if (IsOnGround)
				StrafeSpeed = runningStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
			else
				StrafeSpeed = airStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
		}

		private bool isRecentered;
		public void RecenterStrafe()
		{
			//Calculate distance along the plane defined by StrafeDirection
			Vector3 localOffset = PathFollower.LocalPlayerPosition;

			if(isSideScroller || isRecentered)
			{
				GlobalTranslate(PathFollower.StrafeDirection * localOffset.x); //Instantly snap when sidescrolling
				return;
			}

			float recenterSmoothing = runningStrafeSettings.speed * Mathf.Abs(SpeedRatio);
			localOffset.x = Mathf.MoveToward(localOffset.x, 0, recenterSmoothing * PhysicsManager.physicsDelta);
			GlobalTranslation = PathFollower.Xform(localOffset) + PathFollower.GlobalTranslation;

			if (Mathf.IsZeroApprox(localOffset.x))
				isRecentered = true;
		}

		#region Actions
		public const float GRAVITY = 28.0f;
		[Export]
		public float maxGravity;
		private void UpdateActions()
		{
			if (IsStomping)
			{
				UpdateStomp();
				return;
			}

			if (IsBackflipping)
			{
				UpdateBackflip();
				return;
			}

			if (Lockon.IsBouncing)
			{
				Lockon.UpdateBounce();
				return;
			}

			if (ActionState == ActionStates.JumpDash)
			{
				UpdateJumpDash();
				return;
			}

			if (IsOnGround)
				UpdateGroundActions();
			else
				UpdateAirActions();
		}

		private void UpdateGroundActions()
		{
			if (isControlsLocked) //Controls locked out.
			{
				if (ControlLockoutData.resetOnLand && JustLandedOnGround)
					ResetControlLockout();
				else if (ControlLockoutData.disableJumping)
					return;
			}

			if (Soul.IsSpeedBreakActive) return;

			if (IsCrouching)
				UpdateCrouching();
			else if (actionBufferTimer != 0)
			{
				StartCrouching();
				actionBufferTimer = 0;
			}

			if (jumpBufferTimer != 0)
			{
				jumpBufferTimer = 0;
				if (IsHoldingBack())
					StartBackflip();
				else
					Jump();
			}
		}

		private void UpdateAirActions()
		{
			CheckStomp();
			CheckJumpDash();

			if (IsJumping)
				UpdateJump();

			ApplyGravity();
		}

		//Apply Gravity
		private void ApplyGravity() => VerticalSpeed = Mathf.MoveToward(VerticalSpeed, maxGravity, GRAVITY * PhysicsManager.physicsDelta);

		[Export]
		public float landingBoost; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private bool enableLandingBoost;
		private void CheckLandingBoost()
		{
			if (!enableLandingBoost) return;
			enableLandingBoost = false; //Reset landing boost

			if (MovementState != MovementStates.Normal) return;

			//Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
			if (IsHoldingForward() && MoveSpeed < landingBoost)
			{
				MoveSpeed = landingBoost;
				if(IsFreeMovementActive) //Landing boost only works directly forward
				{
					freeMoveDirection = Vector2.Down;
					FreeMoveSpeed = landingBoost;
				}
			}
		}

		#region Jump
		[Export]
		public float accelerationJumpSpeed;
		[Export]
		public float jumpHeight;
		public float JumpPower => Mathf.Sqrt(2 * GRAVITY * jumpHeight);
		[Export]
		public float jumpCurve = .95f;
		public bool IsJumping => ActionState == ActionStates.Jumping;
		private bool isJumpClamped; //True after the player releases the jump button
		private bool isAccelerationJump;
		private float currentJumpLength; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .04f; //How fast the jump button needs to be released for an "acceleration jump"
		private void Jump()
		{
			currentJumpLength = 0;
			isJumpClamped = false;
			IsOnGround = false;
			CanJumpDash = true;
			enableLandingBoost = true;
			ActionState = ActionStates.Jumping;

			VerticalSpeed = JumpPower;
			Animator.Jump();
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpLength >= ACCELERATION_JUMP_LENGTH)
			{
				//Acceleration jump?
				if (IsHoldingForward() || (IsFreeMovementActive && rotatedMovementValue.Length() > .5f))
				{
					ActionState = ActionStates.AccelJump;
					SetFreeMovement(true);
					SetFreeMoveSpeed(accelerationJumpSpeed);
					Animator.JumpAccel();
				}

				VerticalSpeed = 5f; //Consistant accel jump height
				isAccelerationJump = false; //Stop listening for an acceleartion jump
			}

			if (!isJumpClamped)
			{
				if (!Controller.jumpButton.isHeld)
				{
					isJumpClamped = true;
					if (currentJumpLength <= ACCELERATION_JUMP_LENGTH) //Listen for acceleration jump
						isAccelerationJump = true;
				}
			}
			else if (IsRising)
				VerticalSpeed *= jumpCurve; //Kill jump height

			currentJumpLength += PhysicsManager.physicsDelta;
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
		public bool IsAttacking { get; private set; } //Should the player damage enemies?

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
			IsAttacking = true;
			CanJumpDash = false; //Don't use get/set so we keep our target monitoring.
			ActionState = ActionStates.JumpDash;

			if (Lockon.LockonTarget == null) //Normal jumpdash
			{
				SetFreeMovement(true);
				SetFreeMoveSpeed(jumpDashSpeed);
				VerticalSpeed = jumpDashPower;
			}
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

				VerticalSpeed = 0;

				customPhysicsEnabled = true;

				freeMoveDirection = Lockon.HomingAttackDirection.Flatten().Rotated(_gimbal.GlobalRotation.y).Normalized();
				MoveAndSlide(Lockon.HomingAttackDirection.Normalized() * Lockon.homingAttackSpeed);
				PathFollower.Resync();
			}
			else //Normal Jump dash; Apply gravity
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);

			CheckStomp();
		}
		#endregion

		#region Crouch
		public bool IsCrouching => ActionState == ActionStates.Crouching;
		private const float SLIDE_FRICTION = 8f;
		private void StartCrouching()
		{
			ActionState = ActionStates.Crouching;
		}

		private void UpdateCrouching()
		{
			MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, SLIDE_FRICTION * PhysicsManager.physicsDelta);

			if(MoveSpeed == 0)
				StrafeSpeed = Mathf.MoveToward(StrafeSpeed, 0, runningStrafeSettings.friction * PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasReleased)
				ResetActionState();
		}
		#endregion

		#region Stomp
		private const int STOMP_SPEED = -32;
		private const int STOMP_GRAVITY = 108;
		public bool IsStomping => ActionState == ActionStates.Stomping;
		private void StartStomping()
		{
			ResetVelocity();
			actionBufferTimer = 0;

			enableLandingBoost = true;
			Lockon.ResetLockonTarget();
			Lockon.IsMonitoring = false;

			ActionState = ActionStates.Stomping;
			Animator.Stomp();
		}

		private void UpdateStomp()
		{
			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, STOMP_SPEED, STOMP_GRAVITY * PhysicsManager.physicsDelta);
		}

		private void CheckStomp()
		{
			if (actionBufferTimer != 0)
				StartStomping();
		}
		#endregion

		#region Backflip
		[Export]
		public float backflipSpeed;
		[Export]
		public float backflipHeight;
		private float BackflipPower => Mathf.Sqrt(2 * GRAVITY * backflipHeight);
		private bool IsBackflipping => ActionState == ActionStates.Backflip;
		private float backflipTimer;
		private const float BACKFLIP_LENGTH = .4f;
		private void UpdateBackflip()
		{
			backflipTimer += PhysicsManager.physicsDelta;
			if (backflipTimer > BACKFLIP_LENGTH)
				CancelBackflip();

			StrafeSpeed = 0;
			MoveSpeed = -backflipSpeed;
			CheckStomp();
			CheckJumpDash();
			ApplyGravity();
		}

		private void StartBackflip()
		{
			backflipTimer = 0;
			StrafeSpeed = 0;
			MoveSpeed = -backflipSpeed;
			VerticalSpeed = BackflipPower;
			CanJumpDash = true;

			IsOnGround = false;

			freeMoveDirection = Vector2.Down; //For jumpdashing

			ActionState = ActionStates.Backflip;
			Animator.Backflip();
		}

		public void CancelBackflip()
		{
			if(ActionState == ActionStates.Backflip)
				ActionState = ActionStates.Normal;
		}
		#endregion
		#endregion

		#endregion

		#region Damage & Invincibility
		public bool IsInvincible => invincibliltyTimer != 0;
		private float invincibliltyTimer;
		private const float INVINCIBILITY_LENGTH = 5f;

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

			VerticalSpeed -= GRAVITY * PhysicsManager.physicsDelta;
		}

		[Signal]
		public delegate void Damaged(Spatial n); //This signal is called anytime a hitbox collides with the player, regardless of invincibilty.

		public void TakeDamage(Spatial node)
		{
			EmitSignal(nameof(Damaged), node);

			if (IsInvincible) return; //Don't take damage.

			ActionState = ActionStates.Damaged;
			invincibliltyTimer = INVINCIBILITY_LENGTH;

			if (MovementState == MovementStates.Normal)
			{
				IsOnGround = false;
				MoveSpeed = -4f;
				StrafeSpeed = 0f;
				VerticalSpeed = 4f;
			}
			else if (MovementState == MovementStates.Sidle) //Start falling
			{
				sidleTimer = SIDLE_DAMAGE_LENGTH;
				MoveSpeed = VerticalSpeed = 0;
			}
		}

		public void Kill()
		{
			ResetVelocity();
			PathFollower.HOffset = PathFollower.VOffset = 0;

			//TODO Check deathless mission modifier/Play death animation
			Respawn();
		}

		public void Respawn()
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			GlobalTransform = Triggers.CheckpointTrigger.activeCheckpoint.GlobalTransform;

			StageSettings.instance.RespawnObjects();

			freeMoveDirection = Vector2.Zero; //Reset free roam direction
			ResetOrientation = true;
			Camera.ResetFlag = true;
		}
		#endregion

		#region Sidle
		[Export]
		public MovementResource sidleSettings;
		public Spatial CurrentRailing { get; set; }
		private float sidleTimer;
		private readonly float SIDLE_DAMAGE_LENGTH = .5f;
		private readonly float SIDLE_RAIL_FALL_SPEED = 4f;
		private readonly float SIDLE_HORIZONTAL_SPEED = .25f;

		public void StartSidle()
		{
			Soul.IsSpeedBreakEnabled = false;

			ControlLockoutData.disableJumping = true;
			MovementState = MovementStates.Sidle;
		}

		private void UpdateSidle()
		{
			sidleTimer = Mathf.MoveToward(sidleTimer, 0, PhysicsManager.physicsDelta);

			switch (ActionState)
			{
				case ActionStates.Normal:
					break;
				case ActionStates.Damaged: //Fall
					UpdateSidleDamage();
					return;
				case ActionStates.Hanging:
					UpdateSidleHang();
					return;
				default:
					UpdateNormalState(); //Busy with a previous action
					return;
			}
			
			MoveSpeed = sidleSettings.Interpolate(MoveSpeed, GetMovementInputValue());
		}

		private void UpdateSidleDamage()
		{
			if (sidleTimer == 0)
			{
				PathFollower.HOffset = Mathf.Lerp(PathFollower.HOffset, -1, SIDLE_HORIZONTAL_SPEED);

				if (CurrentRailing != null)
				{
					float targetY = CurrentRailing.GlobalTranslation.y;
					GlobalTranslation = new Vector3(GlobalTranslation.x, Mathf.MoveToward(GlobalTranslation.y, targetY, SIDLE_RAIL_FALL_SPEED * PhysicsManager.physicsDelta), GlobalTranslation.z); //Snap to railing

					if (Mathf.IsEqualApprox(GlobalTranslation.y, targetY))
					{
						ActionState = ActionStates.Hanging;
						sidleTimer = 5f;
						PathFollower.HOffset = -1f; //All railings MUST be 1 unit away from the wall.
					}
				}
				else
					ApplyGravity();
			}
		}

		private void UpdateSidleHang()
		{
			if (sidleTimer == 0)
			{
				if (IsRising)
					PathFollower.HOffset = Mathf.Lerp(PathFollower.HOffset, 0, SIDLE_HORIZONTAL_SPEED);
				else if (IsOnGround)
					PathFollower.HOffset = 0;

				ApplyGravity();
			}
			else if (jumpBufferTimer != 0)
			{
				//Sidle Recovery
				jumpBufferTimer = 0;
				VerticalSpeed = 8;
				sidleTimer = 0;
			}
		}
		#endregion

		#region Launchers and Jumps
		[Signal]
		public delegate void LauncherFinished();
		
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

			if (!useAutoAlignment) return;

			Vector3 direction = launchData.launchDirection.RemoveVertical().Normalized();
			if (!direction.IsNormalized()) //Direction parallel with Vector3.Up! Use launcher's forward direction instead.
			{
				if (newLauncher == null) return;
				direction = newLauncher.Forward().RemoveVertical().Normalized();
			}

			//Animator.SetForwardDirection(direction);
			//Animator.ResetLocalRotation();
		}

		private void UpdateLauncher()
		{
			customPhysicsEnabled = true;
			if (activeLauncher != null && !activeLauncher.IsCharacterCentered)
				GlobalTranslation = activeLauncher.RecenterCharacter();
			else
			{
				Vector3 targetPosition = launchData.InterpolatePosition(launcherTime);
				float heightDelta = targetPosition.y - GlobalTranslation.y;
				GlobalTranslation = targetPosition;

				if (heightDelta < 0) //Only check ground when falling
					CheckGround();

				if (IsOnGround || launchData.IsLauncherFinished(launcherTime)) //Revert to normal state
				{
					FinishLauncher();
					if (!IsOnGround)
					{
						MoveSpeed = launchData.InitialHorizontalVelocity;
						VerticalSpeed = launchData.FinalVerticalVelocity;
					}
				}

				launcherTime += PhysicsManager.physicsDelta;
			}

			PathFollower.Resync();
		}

		private void FinishLauncher()
		{
			if(activeLauncher != null && !IsOnGround)
				CanJumpDash = activeLauncher.allowJumpDashing;
				
			ResetMovementState();
			activeLauncher = null;
			EmitSignal(nameof(LauncherFinished));
		}

		public void JumpTo(Vector3 destination, float midHeight = 0f, bool relativeToEnd = false) //Generic JumpTo
		{
			Objects.LaunchData data = Objects.LaunchData.Create(GlobalTranslation, destination, midHeight, relativeToEnd);
			StartLauncher(data);
		}
		#endregion

		#region Grinding
		[Export]
		public MovementResource grindingSettings;
		[Export]
		public ControlLockoutResource grindStepLockoutSettings;
		[Export]
		public int grindShuffleSpeed;
		[Export]
		public int grindStepSpeed;
		[Export]
		public int grindStepHeight;
		public bool IsGrinding => MovementState == MovementStates.Grinding;
		public bool IsGrindStepping { get; private set; }
		private Objects.GrindRail grindRail;
		private const float MINIMUM_GRIND_SPEED = 2f;

		public void StartGrinding(Objects.GrindRail newRail, Vector3 railPosition)
		{
			if (IsGrindStepping) //Grindstep bonus
				StageSettings.instance.AddBonus(StageSettings.BonusType.GrindStep);

			IsGrindStepping = false;
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Grinding;
			grindRail = newRail;

			StrafeSpeed = 0;
			VerticalSpeed = 0;

			MoveSpeed = grindShuffleSpeed;
			GlobalTranslation = railPosition;

			Animator.StartGrinding();
		}

		private void UpdateGrinding()
		{
			customPhysicsEnabled = true;
			MoveSpeed = grindingSettings.Interpolate(MoveSpeed, GetMovementInputValue());

			//Update Shuffle
			AnimationNodeStateMachinePlayback grindState = Animator.GrindingState;
			string currentAnimation = grindState.GetCurrentNode();

			if (jumpBufferTimer != 0)
			{
				jumpBufferTimer = 0;

				if (Mathf.Abs(GetStrafeInputValue()) < .5f)
				{
					Jump();
					StrafeSpeed = 0;
				}
				else
				{
					//Grind step
					StartControlLockout(grindStepLockoutSettings);
					StrafeSpeed = grindStepSpeed * Mathf.Sign(GetStrafeInputValue());
					VerticalSpeed = grindStepHeight;
					IsGrindStepping = true;
				}

				StopGrinding();
				return;
			}
			else if (currentAnimation == "balance_left" || currentAnimation == "balance_right")
			{
				//Grind shuffle
				if (Controller.horizontalAxis.WasTapBuffered)
				{
					Controller.horizontalAxis.ResetTap();
					int targetGrindDirection = currentAnimation == "balance_left" ? 1 : -1;
					if (targetGrindDirection != Controller.horizontalAxis.sign)
					{
						grindState.Travel(targetGrindDirection == 1 ? "balance_right" : "balance_left");
						MoveSpeed = grindShuffleSpeed;
					}
				}
			}
			else if (Mathf.IsEqualApprox(grindState.GetCurrentPlayPosition(), grindState.GetCurrentLength()))
			{
				//BUG FIX somtimes the animator gets stuck
				grindState.Travel(currentAnimation == "shuffle_right" ? "balance_right" : "balance_left");
			}

			MoveAndSlide(-grindRail.Forward() * MoveSpeed);
			PathFollower.Resync();

			if (MoveSpeed <= MINIMUM_GRIND_SPEED)
			{
				StrafeSpeed = currentAnimation == "balance_left" ? 10 : -10; //Hop off
				StopGrinding();
			}
		}

		public void StopGrinding()
		{
			grindRail = null;
			Animator.StopGrinding();
			CancelMovementState(MovementStates.Grinding);
		}
		#endregion

		#region Physics
		[Export]
		public NodePath environmentCollider;
		private CollisionShape _environmentCollider;
		public void EnableEnvironmentCollider() => _environmentCollider.Disabled = false; //For external control that doesn't want the player to collide with environments
		public void DisableEnvironmentCollider() => _environmentCollider.Disabled = true;

		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask;

		public float StrafeSpeed //Player's strafing speed
		{
			get => velocity.x;
			set => velocity.x = value;
		}
		public float VerticalSpeed //Player's vertical speed
		{
			get => velocity.y;
			set => velocity.y = value;
		}
		public float MoveSpeed //Player's speed towards the goal
		{
			get => velocity.z;
			set => velocity.z = value;
		}
		private Vector3 velocity; //x -> strafe, y -> jump/fall, z -> speed
		private void ResetVelocity() //Resets all speed values to zero
		{
			MoveSpeed = 0;
			FreeMoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;
		}

		public float SpeedRatio => IsFreeMovementActive ? moveSettings.GetSpeedRatio(FreeMoveSpeed) : MoveSpeed < 0 ? backstepSettings.GetSpeedRatio(MoveSpeed) : moveSettings.GetSpeedRatio(MoveSpeed);
		public Vector3 Velocity => PathFollower.Xform(velocity);

		public bool IsFalling => VerticalSpeed < 0;
		public bool IsRising => VerticalSpeed > 0;

		public Vector3 CenterPosition => GlobalTranslation + worldDirection * COLLISION_RADIUS; //Center of collision calculations

		private bool customPhysicsEnabled;
		private const float COLLISION_RADIUS = .4f;
		private void UpdatePhysics()
		{
			Lockon.ProcessLockonTargets();
			if (customPhysicsEnabled) return; //When physics are handled in the state machine

			//Increases accuracy around turns
			Vector3 movementDirection = PathFollower.GlobalTranslation; //Cache path follower's previous position
			PathFollower.UpdateOffset(MoveSpeed * PhysicsManager.physicsDelta); //Update pathfollower
			movementDirection = (PathFollower.GlobalTranslation - movementDirection).Normalized(); //Use the difference to the current position as the movement direction
			movementDirection *= Mathf.Sign(MoveSpeed); //Invert if needed

			//Collision checks
			CheckGround();

			strafeCollisions = StrafeCollisions.None;
			CheckStrafeWall(1);
			CheckStrafeWall(-1);
			CheckMainWall(movementDirection);

			if (!IsOnGround && ActionState == ActionStates.JumpDash) //Jump dash ignores slopes
				movementDirection = movementDirection.RemoveVertical().Normalized();
			MoveAndSlide(movementDirection * MoveSpeed + PathFollower.StrafeDirection * StrafeSpeed + worldDirection * VerticalSpeed);
			CheckCeiling();

			PathFollower.Resync(); //Resync
		}

		public Vector3 worldDirection = Vector3.Up; //Current "up" direction of the player

		private float slopeInfluence;
		private const float SLOPE_DEADZONE = .1f; //Ignore slope influence when less than this value
		private const float SLOPE_INFLUENCE = .8f; //How much slope influence should affect the player

		public bool IsOnGround { get; private set; }
		public bool JustLandedOnGround { get; private set; } //Flag for doing stuff on land
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS;
			if (IsOnGround)
			{
				castLength += .5f; //For slopes that go downwards

				if (Soul.IsSpeedBreakActive) //Moving faster, more snapping needed
					castLength += .5f;
			}
			else if (IsFalling)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;
			else if (IsRising)
				castLength = -.1f; //Reduce snapping when moving upwards

			Vector3 castVector = -worldDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!ValidateGroundCast(ref groundHit))
			{
				//Whisker casts (For slanted walls and ground)
				float interval = Mathf.Tau / 4f;
				Vector3 castOffset = PathFollower.ForwardDirection;
				for (int i = 0; i < 4; i++)
				{
					castOffset = castOffset.Rotated(worldDirection, interval).Normalized() * COLLISION_RADIUS * .5f;
					groundHit = this.CastRay(castOrigin + castOffset, castVector, environmentMask);
					Debug.DrawRay(castOrigin + castOffset, castVector, groundHit ? Colors.Red : Colors.White);
					if (ValidateGroundCast(ref groundHit)) break; //Found the floor
				}
			}

			if (groundHit) //Successful ground hit
			{
				GlobalTranslation -= worldDirection * (groundHit.distance - COLLISION_RADIUS); //Snap to ground

				if (!IsOnGround) //Landing on the ground
				{
					IsOnGround = true;
					VerticalSpeed = 0;

					isJumpClamped = false;
					IsAttacking = false;
					CanJumpDash = false;
					IsGrindStepping = false;
					isAccelerationJump = false;

					JustLandedOnGround = true;

					CheckLandingBoost(); //Landing boost skill

					ResetActionState();
					Lockon.ResetLockonTarget();
					worldDirection = groundHit.normal;
				}
				else //Update world direction
					worldDirection = worldDirection.LinearInterpolate(groundHit.normal, .2f).Normalized();

				//Calculate slope influence
				float rotationAmount = PathFollower.GlobalTransform.Forward().SignedAngleTo(Vector3.Forward, Vector3.Up);
				Vector3 slopeDirection = groundHit.normal.Rotated(Vector3.Up, rotationAmount).Normalized();
				slopeInfluence = slopeDirection.z * SLOPE_INFLUENCE;
			}
			else
			{
				slopeInfluence = 0f; //Reset slope influence
				if (IsOnGround && !IsBackflipping)
					Animator.FallAnimation();

				IsOnGround = false;
				
				if (IsBackflipping) return;

				if (isControlsLocked && ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.Recenter)
					worldDirection = PathFollower.Up(); //Follow path
				else //Smooth world direction based on vertical speed
					worldDirection = worldDirection.LinearInterpolate(Vector3.Up, Mathf.Clamp((VerticalSpeed / maxGravity) - .15f, 0f, 1f)).Normalized();
			}
		}

		private bool ValidateGroundCast(ref RaycastHit groundHit) //Don't count walls as the ground
		{
			if (groundHit && groundHit.collidedObject.IsInGroup("wall")) groundHit = new RaycastHit();
			return groundHit;
		}

		private void CheckCeiling() //Checks the ceiling.
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS;

			Vector3 castVector = worldDirection * castLength;
			if (IsRising)
				castVector.y += VerticalSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GlobalTranslate(ceilingHit.point - (CenterPosition + worldDirection * COLLISION_RADIUS));

				if (IsRising)
					VerticalSpeed = 0;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (Mathf.IsZeroApprox(MoveSpeed)) return; //No movement

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;

			RaycastHit centerHit = this.CastRay(CenterPosition, castVector * castLength, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector * castLength, centerHit ? Colors.Red : Colors.White);
			if (!IsValidWallCast(centerHit))
				centerHit = new RaycastHit();

			if (centerHit)
			{
				float wallRatio = ExtensionMethods.DotProd2D(centerHit.normal, PathFollower.ForwardDirection);
				if (wallRatio > .8f || strafeCollisions == StrafeCollisions.Both)
				{
					if (Soul.IsSpeedBreakActive) //Cancel speed break
						Soul.ToggleSpeedBreak();

					MoveSpeed = 0;

					if (centerHit.distance > COLLISION_RADIUS + COLLISION_PADDING)
						GlobalTranslate(castVector * (centerHit.distance - COLLISION_RADIUS)); //Snap to wall

					if (IsFreeMovementActive)
					{
						float dot = Mathf.Abs(castVector.Flatten().Normalized().Dot(freeMoveDirection.Rotated(-_gimbal.GlobalRotation.y)));
						float speedClamp = Mathf.Clamp(1.2f - dot * .4f, 0f, 1f);
						if (dot > .8f)
							speedClamp = 0f;
						SetFreeMoveSpeed(FreeMoveSpeed * speedClamp);
					}
				}
			}
		}

		private bool IsValidWallCast(RaycastHit hit) => hit && (ExtensionMethods.DotProd2D(hit.normal, hit.direction) >= .5f && !hit.collidedObject.IsInGroup("floor"));

		private StrafeCollisions strafeCollisions;
		private enum StrafeCollisions
		{
			None,
			Left,
			Right,
			Both
		}
		private const float COLLISION_PADDING = .1f;

		//Checks for wall collision side to side. (Always active)
		private void CheckStrafeWall(int direction)
		{
			if (isSideScroller) return; //No wall checks when sidescrolling.

			bool isActiveDirection = Mathf.Sign(StrafeSpeed) == direction;
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(StrafeSpeed) * PhysicsManager.physicsDelta;
			Vector3 castVector = PathFollower.StrafeDirection * castLength * direction;
			RaycastHit hit = this.CastRay(CenterPosition, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector, hit ? Colors.Red : Colors.White);

			if (hit)
			{
				//Only process active collision
				if (isActiveDirection)
				{
					float dot = ExtensionMethods.DotProd2D(hit.normal, PathFollower.StrafeDirection);

					if (dot > .8f)
					{
						if (hit.distance > COLLISION_RADIUS + COLLISION_PADDING) //Snap
							GlobalTranslate(hit.direction * (hit.distance - COLLISION_RADIUS));

						StrafeSpeed = 0;

						if (IsFreeMovementActive)
						{
							dot = Mathf.Abs(castVector.Flatten().Normalized().Dot(freeMoveDirection.Rotated(-_gimbal.GlobalRotation.y)));
							float speedClamp = Mathf.Clamp(1.2f - dot * .4f, 0f, 1f);
							if (dot > .8f)
								speedClamp = 0f;
							SetFreeMoveSpeed(FreeMoveSpeed * speedClamp);
						}
					}
					else
					{
						float maxSpeed = runningStrafeSettings.speed * (1 - dot);
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -maxSpeed, maxSpeed);
					}
				}

				//Always update strafe collisions
				if (strafeCollisions == StrafeCollisions.None)
					strafeCollisions = direction > 0 ? StrafeCollisions.Right : StrafeCollisions.Left;
				else
					strafeCollisions = StrafeCollisions.Both;
			}
		}

		private const float ORIENTATION_SMOOTHING = .4f;
		private bool ResetOrientation { get; set; }

		private void UpdateOrientation() //Orientates Root to world direction, then rotates the gimbal on the y-axis
		{
			Transform t = GlobalTransform;
			t.basis.z = Vector3.Back;
			t.basis.y = worldDirection;
			t.basis.x = -t.basis.z.Cross(t.basis.y);
			t.basis = t.basis.Orthonormalized();
			GlobalTransform = t;

			float rotationAmount = -_gimbal.Forward().Flatten().AngleTo(PathFollower.ForwardDirection.Flatten()); //Face path
			if (Lockon.IsHomingAttacking) //Face target
				rotationAmount = -_gimbal.Forward().Flatten().AngleTo(Lockon.HomingAttackDirection.Flatten());

			if (ResetOrientation) //Snap orientation
			{
				ResetOrientation = false;
				_gimbal.RotateObjectLocal(Vector3.Up, rotationAmount);
			}
			else
				_gimbal.RotateObjectLocal(Vector3.Up, rotationAmount * ORIENTATION_SMOOTHING);
		}

		public void OnObjectAreaEntered(Area a)
		{
			if (a is Triggers.StageTrigger trigger)
				trigger.OnEnter();
			else if ((Node)a is Objects.Pickup pickup) //This node cast is NOT redundant, and IS needed
				pickup.OnEnter();
		}

		public void OnObjectAreaExited(Area a)
		{
			if (a is Triggers.StageTrigger trigger)
				trigger.OnExit();
		}

		public void OnObjectCollisionEnter(PhysicsBody body)
		{
			/*
			Note for when I come back wondering why the player is being pushed through the floor
			Ensure all crushers' animationplayers are using the PHYSICS update mode
			If this is true, then proceed to panic.
			*/
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're ACTUALLy being crushed and not running into the side of the crusher
				RaycastHit hit = this.CastRay(CenterPosition, worldDirection * COLLISION_RADIUS * 2f, environmentMask, false);
				if (hit.collidedObject == body)
				{
					GD.Print($"Crushed by {body.Name}");
					AddCollisionExceptionWith(body); //Avoid clipping through the ground
					TakeDamage(body);
				}
			}

			if (Lockon.IsHomingAttacking && body.IsInGroup("wall"))
				Lockon.StartBounce();
		}

		public void OnObjectCollisionExit(PhysicsBody body)
		{
			if (body.IsInGroup("crusher") && GetCollisionExceptions().Contains(body))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
			}
		}
		#endregion
	}
}
