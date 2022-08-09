using Godot;
using Project.Core;
using Godot.Collections;

namespace Project.Gameplay
{
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
		public CameraController Camera { get; set; } //Camera is set externally

		public override void _Ready()
		{
			instance = this;

			PathFollower = GetNode<CharacterPathFollower>(pathFollower);
			Animator = GetNode<CharacterAnimator>(animator);
			Sound = GetNode<CharacterSound>(sound);
			Soul = GetNode<CharacterSoulSkill>(soul);
			Lockon = GetNode<CharacterLockon>(lockon);
		}

		public override void _PhysicsProcess(float _)
		{
			ProcessStateMachine();

			UpdatePhysics();
			Animator.UpdateAnimation();
			Soul.UpdateSoulSkills();
			Camera.UpdateCamera();
		}

		#region State Machine
		public MovementStates MovementState { get; private set; }
		public enum MovementStates
		{
			Normal, //Standard on rails movement
			External, //Cutscenes, Cinematics, and stage objects that override player control
			Sidle, //Scooting along the wall
			Drift, //Sharp 90 degree corner. Press jump at the right moment to get a burst of speed?
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
			JumpDashing, //Also includes homing attack
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
			UpdateInputBuffers();

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
				case MovementStates.Drift:
					UpdateDrift();
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
		public float GetMovementInputValue()
		{
			if (!isSideScroller)
			{
				if (MoveSpeed >= 0 && Controller.verticalAxis.value < -.2f && Controller.verticalAxis.value > 0)
					return 0;
				return Controller.verticalAxis.value;
			}

			return isFacingRight ? Controller.horizontalAxis.value : -Controller.horizontalAxis.value;
		}
		public float GetStrafeInputValue()
		{
			//Returns 1 for moving right, -1 for moving left
			if (!isSideScroller)
				return Controller.horizontalAxis.value;

			return 0; //No strafe when sidescrolling
		}

		private float jumpBufferTimer;
		private float actionBufferTimer;
		private const float ACTION_BUFFER_LENGTH = .2f; //How long to allow actions to be buffered
		private const float JUMP_BUFFER_LENGTH = .1f; //How long to allow actions to be buffered

		private void UpdateInputBuffers()
		{
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


		private bool isControlsLocked;
		private float controlLockoutTimer;
		public ControlLockoutResource ControlLockoutData { get; private set; }

		public void ResetControlLockout()
		{
			controlLockoutTimer = 0;
			isControlsLocked = false;
		}
		public void SetControlLockout(ControlLockoutResource data)
		{
			if(data == null)
			{
				ResetControlLockout();
				return;
			}

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
		private Vector3 automationOffset;
		private Spatial externalParent;

		[Signal]
		public delegate void ExternalControlCompleted();
		public void StartExternal(Spatial followObject = null, bool snap = false)
		{
			ResetMovementState();
			MovementState = MovementStates.External;
			ActionState = ActionStates.Normal;

			externalParent = followObject;
			if (externalParent != null)
				MoveSpeed = 0;
			
			StrafeSpeed = 0;
			VerticalSpeed = 0;
			automationOffset = snap ? Vector3.Zero : GlobalTranslation - PathFollower.GlobalTranslation;

			UpdateExternalControl();
		}

		public void UpdateExternalControl()
		{
			customPhysicsEnabled = true;
			automationOffset = automationOffset.LinearInterpolate(Vector3.Zero, .2f); //Smooth out entry

			if (externalParent != null)
				GlobalTransform = externalParent.GlobalTransform;
			else
			{
				if(MoveSpeed < moveSettings.speed)
					MoveSpeed = moveSettings.speed;
				PathFollower.Offset += MoveSpeed * PhysicsManager.physicsDelta;
				GlobalTransform = PathFollower.GlobalTransform;
			}

			GlobalTranslation += automationOffset;
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
		public MovementResource standingStrafeSettings;
		[Export]
		public MovementResource airStrafeSettings;
		[Export]
		public MovementResource backstepSettings;

		private void UpdateMoveSpeed()
		{
			if (IsCrouching) return;
			if (Soul.IsSpeedBreakActive) return; //Overridden to max speed

			if (isControlsLocked && !Mathf.IsZeroApprox(ControlLockoutData.speedRatio))
			{
				//Change speed to the correct value
				if (ControlLockoutData.tractionRatio == 0)
					MoveSpeed = moveSettings.speed * ControlLockoutData.speedRatio;
				else
				{
					float spd = moveSettings.speed * ControlLockoutData.speedRatio;
					float delta = moveSettings.traction * ControlLockoutData.tractionRatio * PhysicsManager.physicsDelta;
					MoveSpeed = Mathf.MoveToward(MoveSpeed, spd, delta);
				}
				return;
			}

			if (IsOnGround)
			{
				if (MoveSpeed >= 0)
				{
					if (GetMovementInputValue() > .5f && SpeedRatio > .8f) //Fix slowing down when turning at high speeds
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

			if (IsOnGround)
			{
				float standingStrafe = standingStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
				float runningStrafe = runningStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
				StrafeSpeed = Mathf.Lerp(standingStrafe, runningStrafe, Mathf.Abs(SpeedRatio));
			}
			else
				StrafeSpeed = airStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
		}

		private bool isRecentered;
		private const float RECENTER_SMOOTHING = .2f;
		public void RecenterStrafe()
		{
			//Calculate distance along the plane defined by StrafeDirection
			Vector3 localOffset = PathFollower.LocalPlayerPosition;

			if(isSideScroller || isRecentered)
			{
				GlobalTranslate(StrafeDirection * localOffset.x); //Instantly snap when sidescrolling
				return;
			}

			float recenterSmoothing = runningStrafeSettings.speed * Mathf.Abs(SpeedRatio) * RECENTER_SMOOTHING;
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

			if (ActionState == ActionStates.JumpDashing)
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
				if (GetMovementInputValue() < 0)
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

		private void ApplyGravity()
		{
			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, maxGravity, GRAVITY * PhysicsManager.physicsDelta); //Apply Gravity
		}

		[Export]
		public float landingBoost; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private void CheckLandingBoost()
		{
			if (MovementState == MovementStates.Normal && ActionState != ActionStates.Damaged && GetMovementInputValue() > 0.5f)
			{
				//Landing boost when holding forward (See Sonic and the Black Knight)
				if (MoveSpeed < landingBoost)
					MoveSpeed = landingBoost;
			}
		}

		private void LandOnGround()
		{
			IsOnGround = true;
			VerticalSpeed = 0;

			isJumpClamped = false;
			IsAttacking = false;
			CanJumpDash = false;
			IsGrindStepping = false;
			isAccelerationJump = false;

			landingTimer = 2;

			CheckLandingBoost();

			ResetActionState();
			Lockon.ResetLockonTarget();
		}

		#region Jump
		[Export]
		public float accelerationJumpSpeed;
		[Export]
		public float jumpHeight;
		[Export]
		public float jumpPower;
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
			ActionState = ActionStates.Jumping;

			VerticalSpeed = JumpPower;
			if (MoveSpeed < 0) //Disallow jumping backwards
				MoveSpeed = 0;

			Animator.Jump();
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpLength >= ACCELERATION_JUMP_LENGTH)
			{
				//Acceleration jump dash?
				if (Controller.verticalAxis.value > 0 || Controller.horizontalAxis.value != 0)
				{
					ActionState = ActionStates.AccelJump;
					Vector2 accelJumpDirection = Vector2.Down.Rotated(Animator.Rotation.y); //Allow accel jumping sideways (For those rare wide fields)
					StrafeSpeed = accelerationJumpSpeed * accelJumpDirection.x;
					MoveSpeed = accelerationJumpSpeed * accelJumpDirection.y;
					Animator.JumpAccel();
				}
				VerticalSpeed = 5f;
				isAccelerationJump = false;
			}

			if (!isJumpClamped)
			{
				if (!Controller.jumpButton.isHeld)
				{
					isJumpClamped = true;
					if (currentJumpLength <= ACCELERATION_JUMP_LENGTH)
						isAccelerationJump = true;
				}
			}
			else if (IsRising)
				VerticalSpeed *= jumpCurve;

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
			ActionState = ActionStates.JumpDashing;

			if (Lockon.LockonTarget == null) //Normal jumpdash
			{
				MoveSpeed = jumpDashSpeed;
				VerticalSpeed = jumpDashPower;
				Animator.ResetLocalRotation();
			}
			else
				Lockon.HomingAttack(); //Start Homing attack
		}

		private void UpdateJumpDash()
		{
			if (Lockon.LockonTarget != null && IsAttacking)
			{
				MoveSpeed = Lockon.homingAttackSpeed;
				StrafeSpeed = VerticalSpeed = 0;
				customPhysicsEnabled = true;
				Vector3 travelDirection = (Lockon.LockonTarget.GlobalTranslation - GlobalTranslation).Normalized();
				MoveAndCollide(travelDirection * MoveSpeed * PhysicsManager.physicsDelta);
			}
			else
			{
				MoveSpeed = jumpDashSpeed;
				VerticalSpeed = Mathf.MoveToward(VerticalSpeed, jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);
			}

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
			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;
			actionBufferTimer = 0;

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
		private bool IsInvincible => invincibliltyTimer != 0;
		private float invincibliltyTimer;
		private const float INVINCIBILITY_LENGTH = 5f;

		private void UpdateInvincibility()
		{
			if (IsInvincible)
			{
				invincibliltyTimer = Mathf.MoveToward(invincibliltyTimer, 0, PhysicsManager.physicsDelta);

				if (!IsInvincible && collidingHitboxCount.Count != 0) //Invincibility ran out while still touching a hurtbox
					TakeDamage(); //Take Damage again
			}
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

		private readonly Array<Node> collidingHitboxCount = new Array<Node>(); //Number of hitboxes that are colliding with the player
		//called when a hitbox stops colliding with the player
		public void DequeueHitbox(Node n)
		{
			if (collidingHitboxCount.Contains(n))
				collidingHitboxCount.Remove(n);
		}

		public void TakeDamage(Node n = null)
		{
			if(n != null && !collidingHitboxCount.Contains(n))
				collidingHitboxCount.Add(n);

			if (MovementState == MovementStates.Normal)
			{
				IsOnGround = false;
				MoveSpeed = -4f;
				StrafeSpeed = 0f;
				VerticalSpeed = 4f;
			}

			ActionState = ActionStates.Damaged;

			if (IsInvincible) return; //Don't take damage.
			invincibliltyTimer = INVINCIBILITY_LENGTH;

			if (MovementState == MovementStates.Sidle) //Start falling
			{
				sidleTimer = SIDLE_DAMAGE_LENGTH;
				MoveSpeed = VerticalSpeed = 0;
			}
		}

		public void Kill()
		{
			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;
			PathFollower.HOffset = PathFollower.VOffset = 0;

			//TODO Check deathless mission modifier/Play death animation

			Respawn();
		}

		public void Respawn()
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			GlobalTransform = Triggers.CheckpointTrigger.activeCheckpoint.GlobalTransform;

			PathFollower.pathFollowerOffset = 0f; //Reset excess offset
			StageSettings.instance.RespawnObjects();
			//Camera.ResetFlag = true;
		}
		#endregion

		#region Sidle
		[Export]
		public MovementResource sidleSettings;
		private Spatial currentRailing;
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

				if (currentRailing != null)
				{
					float targetY = currentRailing.GlobalTranslation.y;
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
		public delegate void OnLauncherFinished();
		
		private float launcherTime;
		private Launcher activeLauncher;
		private Launcher.LaunchData launchData;
		public void StartLauncher(Launcher.LaunchData data, Launcher newLauncher = null)
		{
			if (activeLauncher != null && activeLauncher == newLauncher) return; //Already launching that!

			ResetMovementState();

			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Launcher;

			activeLauncher = newLauncher;
			launchData = data;

			MoveSpeed = 0;
			VerticalSpeed = 0;
			StrafeSpeed = 0;

			IsOnGround = false;
			launcherTime = 0;

			CanJumpDash = false;
			Lockon.ResetLockonTarget();
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
			if(activeLauncher != null)
				CanJumpDash = activeLauncher.allowJumpDashing;
				
			ResetMovementState();
			activeLauncher = null;
			EmitSignal(nameof(OnLauncherFinished));
		}

		public void JumpTo(Vector3 destination, float midHeight = 0f, bool relativeToDst = false) //Generic JumpTo
		{
			Launcher.LaunchData data = Launcher.CreateData(GlobalTranslation, destination, midHeight, relativeToDst);
			StartLauncher(data);
		}
		#endregion

		/*
		 * Any action that uses the balancing feature. (Flying, Surfing, etc)
		 * Grinding is included because it uses the same animations, even though balancing on a rail is not possible
		*/
		#region Balancing
		private Vector2 balanceLeaning;
		private const float BALANCE_LEAN_SPEED = 8f;

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
		private GrindRail grindRail;
		private const float MINIMUM_GRIND_SPEED = 2f;

		public void StartGrinding(GrindRail newRail, Vector3 railPosition)
		{
			if (IsGrindStepping)
				HeadsUpDisplay.instance.AddBonus(HeadsUpDisplay.BonusTypes.GrindStep);

			IsGrindStepping = false;
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Grinding;
			grindRail = newRail;

			StrafeSpeed = 0;
			VerticalSpeed = 0;

			MoveSpeed = grindShuffleSpeed;
			GlobalTranslation = railPosition;

			balanceLeaning = Vector2.Zero;
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

				int direction = Controller.horizontalAxis.sign;
				if (direction == 0)
				{
					Jump();
					StrafeSpeed = 0;
				}
				else
				{
					//Grind step
					SetControlLockout(grindStepLockoutSettings);
					StrafeSpeed = grindStepSpeed * direction;
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
		#endregion

		#region Drift
		private Triggers.DriftTrigger activeDriftCorner;
		public void StartDrift(Triggers.DriftTrigger corner)
		{
			activeDriftCorner = corner;
			MovementState = MovementStates.Drift;

			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;
		}

		private void UpdateDrift()
		{
			customPhysicsEnabled = true;

			Vector3 targetPosition = activeDriftCorner.TargetPosition;
			float distance = GlobalTranslation.RemoveVertical().DistanceTo(targetPosition.RemoveVertical());

			if (activeDriftCorner.cornerCleared)
			{
				MoveSpeed = moveSettings.speed * Triggers.DriftTrigger.SPEED_RATIO;
				GlobalTranslation = GlobalTranslation.MoveToward(targetPosition, MoveSpeed * PhysicsManager.physicsDelta);

				if (distance < activeDriftCorner.slideDistance * .1f)
					activeDriftCorner.CompleteDrift(true);
			}
			else
			{
				GlobalTranslation = activeDriftCorner.Interpolate(GlobalTranslation);

				if (distance < .5f)
				{
					if (jumpBufferTimer != 0)
					{
						jumpBufferTimer = 0;
						activeDriftCorner.cornerCleared = true;

						GlobalTranslation = new Vector3(targetPosition.x, GlobalTranslation.y, targetPosition.z); //Snap to target position
					}
					else if (distance < .1f)
						activeDriftCorner.CompleteDrift(false);
				}
			}

			PathFollower.Resync();
		}
		#endregion

		#region Physics
		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask;

		public float StrafeSpeed //Player's strafing speed
		{
			get => velocity.x;
			set => velocity.x = value;
		}
		public float VerticalSpeed //Player's speed towards the goal
		{
			get => velocity.y;
			set => velocity.y = value;
		}
		public float MoveSpeed //Player's speed towards the goal
		{
			get => velocity.z;
			set => velocity.z = value;
		}
		public float SpeedRatio => MoveSpeed < 0 ? backstepSettings.GetSpeedRatio(MoveSpeed) : moveSettings.GetSpeedRatio(MoveSpeed);
		private Vector3 velocity; //x -> strafe, y -> jump/fall, z -> speed
		public Vector3 Velocity => PathFollower.Xform(velocity);

		public bool IsFalling => VerticalSpeed < 0;
		public bool IsRising => VerticalSpeed > 0;
		public bool IsIdling => Mathf.Abs(StrafeSpeed) < .1f && Mathf.Abs(MoveSpeed) < .1f;

		public Vector3 CenterPosition => GlobalTranslation + worldDirection * COLLISION_RADIUS; //Center of collision calculations
		public Vector3 StrafeDirection => PathFollower.MovementDirection.Cross(worldDirection).Normalized();

		private bool customPhysicsEnabled;
		private const float COLLISION_RADIUS = .4f;
		private void UpdatePhysics()
		{
			Lockon.ProcessLockonTargets();
			if (customPhysicsEnabled) return; //When physics are handled in the state machine

			/*Movement method
			Move path follower
			Perform collision checks
			Move character
			Resync path follower
			*/
			float movementDelta = MoveSpeed * PhysicsManager.physicsDelta;
			Vector3 movementDirection = PathFollower.MovementDirection;

			PathFollower.UpdateOffset(movementDelta);

			//Use the average direction sampled from before and after changing the offset.
			//Increases accuracy around turns.
			movementDirection = movementDirection.LinearInterpolate(movementDirection, .5f).Normalized();

			CheckMainWall(movementDirection);
			CheckGround();

			strafeCollision = StrafeCollisions.None;

			CheckStrafeWall(1);
			CheckStrafeWall(-1);

			if (!IsOnGround && ActionState == ActionStates.JumpDashing)
				movementDirection = movementDirection.Flatten().Normalized();
			MoveAndSlide(movementDirection * MoveSpeed + StrafeDirection * StrafeSpeed + worldDirection * VerticalSpeed);
			CheckCeiling();

			PathFollower.Resync();
		}

		public Vector3 worldDirection = Vector3.Up;

		private float slopeInfluence;
		private const float SLOPE_DEADZONE = .1f; //Ignore slope influence when less than this value
		private const float SLOPE_INFLUENCE = .8f;

		public bool IsOnGround { get; private set; }
		public bool JustLandedOnGround => landingTimer > 0; //Flag for doing stuff on land
		private int landingTimer;
		private const float GROUND_SNAP_LENGTH = .2f;
		private const float MAX_ANGLE_CHANGE = 80f;

		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				landingTimer--;

			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS;

			if (IsOnGround)
				castLength += GROUND_SNAP_LENGTH;
			else if (IsFalling)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;
			else if (IsRising)
				castLength = -.1f; //Fix allow jumping

			if(Soul.IsSpeedBreakActive)
				castLength += GROUND_SNAP_LENGTH;

			Vector3 castVector = -worldDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!groundHit || groundHit.collidedObject.IsInGroup("wall")) //Whisker casts
			{
				Vector3 startingDirection = (PathFollower.MovementDirection.y > 0 ? 1 : -1) * PathFollower.MovementDirection; //Fix weird snapping
				for (int i = 0; i < 8; i++)
				{
					Vector3 castOffset = startingDirection.Rotated(worldDirection, Mathf.Tau * .125f * i) * COLLISION_RADIUS * .5f;
					RaycastHit hit = this.CastRay(castOrigin + castOffset, castVector, environmentMask, false, GetCollisionExceptions());
					Debug.DrawRay(castOrigin + castOffset, castVector, hit ? Colors.Red : Colors.White);
					if (hit && !hit.collidedObject.IsInGroup("wall"))
					{
						groundHit = hit;
						groundHit.point -= castOffset;
						break;
					}
				}
			}

			if (groundHit && !groundHit.collidedObject.IsInGroup("wall")) //Don't count walls as the ground
			{
				//FIX don't allow 90 degree angle changes in a single frame
				if (Mathf.Rad2Deg(groundHit.normal.AngleTo(Vector3.Up) - worldDirection.AngleTo(Vector3.Up)) > MAX_ANGLE_CHANGE)
					return;

				Vector3 newNormal = ((groundHit.normal * 100).Round() * .01f).Normalized(); //FIX Round to nearest hundredth to reduce jittering
				if(!IsOnGround)
				{
					LandOnGround();
					worldDirection = newNormal;
				}
				else
					worldDirection = worldDirection.LinearInterpolate(newNormal, .2f).Normalized();

				float rotationAmount = PathFollower.GlobalTransform.Forward().SignedAngleTo(Vector3.Forward, Vector3.Up);
				Vector3 slopeDirection = groundHit.normal.Rotated(Vector3.Up, rotationAmount).Normalized();
				slopeInfluence = slopeDirection.z * SLOPE_INFLUENCE;

				GlobalTranslation = groundHit.point;
			}
			else
			{
				slopeInfluence = 0f;
				if (IsOnGround && !IsBackflipping)
					Animator.FallAnimation();
				
				IsOnGround = false;

				if (IsBackflipping) return;

				if (ControlLockoutData != null && ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.Recenter)
					worldDirection = PathFollower.Up(); //Follow path
				else
					worldDirection = worldDirection.LinearInterpolate(Vector3.Up, Mathf.Clamp((VerticalSpeed / maxGravity) - .1f, 0f, 1f)).Normalized();
			}
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
			if (MoveSpeed == 0) return; //No movement.

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;
			Vector3 sidewaysOffset = StrafeDirection * COLLISION_RADIUS * .5f;

			RaycastHit centerHit = this.CastRay(CenterPosition, castVector * castLength, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector * castLength, centerHit ? Colors.Red : Colors.White);
			if (!IsValidWallCast(centerHit))
				centerHit = new RaycastHit();

			if (!centerHit)
			{
				//Whiskers
				RaycastHit leftHit = this.CastRay(CenterPosition - sidewaysOffset, castVector * castLength, environmentMask, false, GetCollisionExceptions());
				RaycastHit rightHit = this.CastRay(CenterPosition + sidewaysOffset, castVector * castLength, environmentMask, false, GetCollisionExceptions());
				Debug.DrawRay(CenterPosition - sidewaysOffset, castVector * castLength, leftHit ? Colors.Red : Colors.White);
				Debug.DrawRay(CenterPosition + sidewaysOffset, castVector * castLength, rightHit ? Colors.Red : Colors.White);

				//Ignore collisions that are "side walls"
				if (!IsValidWallCast(leftHit))
					leftHit = new RaycastHit();
				if (!IsValidWallCast(rightHit))
					rightHit = new RaycastHit();

				if (leftHit || rightHit)
				{
					bool useRightRaycast = rightHit;
					bool isInCorner = (strafeCollision == StrafeCollisions.Left && rightHit) || (strafeCollision == StrafeCollisions.Right && leftHit);
					if (rightHit && leftHit)
					{
						useRightRaycast = rightHit.distance <= leftHit.distance;

						if (!isInCorner) //True when both raycasts are hit and the signs of the dot products aren't equal
							isInCorner = Mathf.Sign(rightHit.normal.Dot(StrafeDirection)) != Mathf.Sign(leftHit.normal.Dot(StrafeDirection));
					}

					centerHit = useRightRaycast ? rightHit : leftHit;

					if (isInCorner)
						MoveSpeed = 0;
				}
			}

			if (centerHit)
			{
				float wallRatio = DotProd2D(centerHit.normal, PathFollower.MovementDirection);
				if (wallRatio > .9f)
				{
					if (Soul.IsSpeedBreakActive) //Cancel speed break
						Soul.ToggleSpeedBreak();

					MoveSpeed = 0;
					GlobalTranslate(castVector * (centerHit.distance - COLLISION_RADIUS)); //Snap to wall
				}
			}
		}

		private bool IsValidWallCast(RaycastHit hit) => hit && (DotProd2D(hit.normal, hit.direction) >= .5f && !hit.collidedObject.IsInGroup("floor"));

		private StrafeCollisions strafeCollision;
		private enum StrafeCollisions
		{
			None,
			Left,
			Right,
			Both
		}
		private const float COLLISION_PADDING = .1f; //At what distance to apply smoothing to strafe (To avoid "Bumpy Corners")

		//Checks for wall collision side to side. (Always active)
		private void CheckStrafeWall(int direction)
		{
			if (isSideScroller) return; //No wall checks when sidescrolling.

			bool isActiveDirection = Mathf.Sign(StrafeSpeed) == direction;
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(StrafeSpeed) * PhysicsManager.physicsDelta;
			Vector3 castVector = StrafeDirection * castLength * direction;
			RaycastHit hit = this.CastRay(CenterPosition, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector, hit ? Colors.Red : Colors.White);

			if (hit)
			{
				//Only process active collision
				if (isActiveDirection)
				{
					float dot = DotProd2D(hit.normal, StrafeDirection);

					if (dot > .8f)
					{
						GlobalTranslate(hit.direction * (hit.distance - COLLISION_RADIUS));

						if(ActionState != ActionStates.AccelJump) //Maintain speed when accel jumping
							StrafeSpeed = 0;
					}
					else
					{
						float maxSpeed = runningStrafeSettings.speed * (1 - dot);
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -maxSpeed, maxSpeed);
					}
				}

				//Always update strafe collisions
				if (hit.distance <= COLLISION_RADIUS)
				{
					if (strafeCollision == StrafeCollisions.None)
						strafeCollision = direction > 0 ? StrafeCollisions.Right : StrafeCollisions.Left;
					else
						strafeCollision = StrafeCollisions.Both;
				}
			}
		}

		//Returns the absolute dot product of a normal relative to an axis ignoring Y values.
		private float DotProd2D(Vector3 normal, Vector3 axis) => Mathf.Abs(normal.RemoveVertical().Normalized().Dot(axis.RemoveVertical().Normalized()));

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

			if (Lockon.IsHomingAttacking)
				Lockon.StartBounce();
		}

		public void OnObjectCollisionExit(PhysicsBody body)
		{
			if (body.IsInGroup("crusher") && GetCollisionExceptions().Contains(body))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
				DequeueHitbox(body);
			}
		}

		public void OnObjectTriggerEnter(Area area)
		{
			if (area.IsInGroup("railing"))
				currentRailing = area;
		}

		public void OnObjectTriggerExit(Area area)
		{
			if (area.IsInGroup("railing"))
				currentRailing = null;
		}
		#endregion
	}
}
