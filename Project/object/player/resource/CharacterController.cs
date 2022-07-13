using Godot;
using Project.Core;
using Godot.Collections;

namespace Project.Gameplay
{
	/* Function Prefixes
	 * Start - Begins an action
	 * Stop - Ends an action
	 * Update - Process an action
	 * Cancel - Ends an action, but only when the current action is the same
	 */

	public class CharacterController : KinematicBody
	{
		public static CharacterController instance;

		[Export]
		public NodePath pathFollower;
		public PathFollow PathFollower { get; private set; }
		private float pathFollowerOffset; //Offset used when a non-looping path ends.
		[Export]
		public NodePath animator;
		public CharacterAnimator Animator { get; private set; }
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionHeight = .4f;
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionWidth = .3f;

		#region Controls
		public InputManager.Controller Controller => InputManager.controller;

		public bool isSideScroller; //Are we in a 2D section?
		public bool isFacingRight; //Determines which way on the controller is back (Only in sidescroller)
		public float GetMovementInputValue()
		{
			if (!isSideScroller)
			{
				if (MoveSpeed >= 0 && Controller.verticalAxis.value > -.2f && Controller.verticalAxis.value < 0)
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
			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private bool isCountdownActive;
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

			isControlsLocked = true;
			ControlLockoutData = data;
			controlLockoutTimer = data.length;

			ActionState = ActionStates.Normal;
		}

		private void UpdateControlLockTimer()
		{
			if (!isControlsLocked || controlLockoutTimer <= 0)
				return;

			controlLockoutTimer = Mathf.MoveToward(controlLockoutTimer, 0, PhysicsManager.physicsDelta);
			if (controlLockoutTimer == 0)
				ResetControlLockout();
		}

		public void OnCountdownStarted()
		{
			isCountdownActive = true;
			Animator.Countdown();
		}

		public void OnCountdownCompleted()
		{
			isCountdownActive = false;
		}

		#endregion

		#region Automation & Events
		private Vector3 automationDifference;
		private Spatial automationParent;

		public void StartFollowingEventObject(Spatial obj)
		{
			automationParent = obj;

			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;

			MovementState = MovementStates.Automation;
		}

		public void StartAutomation()
		{
			MovementState = MovementStates.Automation;
			ActionState = ActionStates.Normal;
			automationDifference = GlobalTransform.origin - PathFollower.GlobalTransform.origin;
			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;

			UpdateAutomation();
		}

		private void UpdateAutomation()
		{
			customPhysicsEnabled = true;
			automationDifference = automationDifference.LinearInterpolate(Vector3.Zero, .2f); //Smooth out entry

			Transform t;

			if (automationParent != null)
				t = automationParent.GlobalTransform;
			else
			{
				MoveSpeed = moveSettings.speed;
				PathFollower.Offset += MoveSpeed * PhysicsManager.physicsDelta;

				t = PathFollower.GlobalTransform;
			}
			
			t.origin += automationDifference;
			GlobalTransform = t;
		}

		public void CancelAutomation()
		{
			automationParent = null;
			
			if (MovementState == MovementStates.Automation)
				MovementState = MovementStates.Normal;
		}
		#endregion
		#endregion

		public override void _Ready()
		{
			instance = this;

			PathFollower = GetNode<PathFollow>(pathFollower);
			Animator = GetNode<CharacterAnimator>(animator);
		}

		public override void _PhysicsProcess(float _)
		{
			ProcessStateMachine();

			UpdatePhysics();
			Animator.UpdateAnimation();
			CameraController.instance.UpdateCamera();
		}

		public override void _ExitTree()
		{
			if (IsTimeBreakActive) //Just in case
				ToggleTimeBreak();
		}

		#region State Machine
		public MovementStates MovementState { get; private set; }
		public enum MovementStates
		{
			Normal, //Standard on rails movement
			Automation,  //Cutscene? Cinematics? May be replaced with input lockout.
			Sidle, //Scooting along the wall
			Launcher, //Springs, Ramps, etc.
			Drift, //Sharp 90 degree corner. Press jump at the right moment to get a burst of speed?
			Grinding, //Grinding on rails
			Catapult, //Aiming a catapult
			FlyingPot, //A flying pot -_-
			AirLauncher, //Pressing jump at the right time will move to a launcher state.
			Rope, //A swinging rope
			ZipLine, //Swinging zip line
			Surfing, //Left and right movement. (Set OnGround to false for a flying carpet)
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
			EnemyBounce, //Struck an enemy with an attack
			Stomping, //Jump cancel
			Backflip,

			//State specific
			Hanging, //Hanging from a ledge (In Sidle Mode)
		}

		private bool customPhysicsEnabled;

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
				case MovementStates.Automation:
					UpdateAutomation();
					break;
			}

			UpdateInvincibility();
			UpdateControlLockTimer();
			UpdateBreakTimer();
		}
		#endregion

		#region Normal State
		private void UpdateNormalState()
		{
			if (IsBeingDamaged) //Damage action overrides all other states
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
		public MovementResource strafeSettings;
		[Export]
		public MovementResource airStrafeSettings;
		[Export]
		public MovementResource backstepSettings;

		private void UpdateMoveSpeed()
		{
			if (IsCrouching) return;
			if (IsSpeedBreakActive) return; //Overridden

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
							MoveSpeed = Mathf.MoveToward(MoveSpeed, Mathf.Abs(StrafeSpeed), strafeSettings.traction * .5f * GetMovementInputValue() * PhysicsManager.physicsDelta);

						MoveSpeed = moveSettings.Interpolate(MoveSpeed, GetMovementInputValue());
					}
				}
				else
					MoveSpeed = backstepSettings.Interpolate(MoveSpeed, -GetMovementInputValue(), true); //Input direction needs to be inverted for negative movement
			}
			else
				MoveSpeed = airMoveSettings.Interpolate(MoveSpeed, GetMovementInputValue());
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
				StrafeSpeed = strafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
			else
				StrafeSpeed = airStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputValue());
		}

		private void RecenterStrafe()
		{
			//Calculate distance along the plane defined by StrafeDirection
			Vector3 calculationPoint = GlobalTransform.origin - PathFollower.GlobalTransform.origin;
			Vector3 rotationAxis = StrafeDirection.Cross(Vector3.Right).Normalized();
			if (rotationAxis.IsNormalized())
				calculationPoint = calculationPoint.Rotated(rotationAxis, StrafeDirection.AngleTo(Vector3.Right));

			float distanceFromCenter = calculationPoint.x;

			if(isSideScroller)
			{
				GlobalTranslate(-StrafeDirection * distanceFromCenter); //Instantly snap when sidescrolling
				return;
			}

			if (MoveSpeed != 0)
			{
				StrafeSpeed = Mathf.MoveToward(StrafeSpeed, -distanceFromCenter / PhysicsManager.physicsDelta, strafeSettings.traction * PhysicsManager.physicsDelta);

				//Center Smoothing
				StrafeSpeed = Mathf.Clamp(StrafeSpeed, -Mathf.Abs(MoveSpeed), Mathf.Abs(MoveSpeed));
				float speedClamp = (Mathf.Abs(distanceFromCenter) - collisionWidth) / STRAFE_SMOOTHING_LENGTH;
				StrafeSpeed = Mathf.Clamp(StrafeSpeed, -speedClamp, speedClamp);
			}
			else
				StrafeSpeed = strafeSettings.Interpolate(StrafeSpeed, 0);
		}

		#region Actions
		[Export]
		public float gravity;
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

			if (IsJumpDashing)
			{
				UpdateJumpDash();
				return;
			}

			if (IsBouncingOffEnemy)
			{
				UpdateEnemyBounce();
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

			UpdateTimeBreak();
			UpdateSpeedBreak();
			if (IsSpeedBreakActive) return;

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
			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, maxGravity, gravity * PhysicsManager.physicsDelta); //Apply Gravity
		}

		[Export]
		public float landingBoost; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private void LandOnGround()
		{
			IsOnGround = true;
			VerticalSpeed = 0;

			isJumpClamped = false;
			IsAttacking = false;
			canJumpDash = false;
			IsGrindStepping = false;
			isAccelerationJump = false;

			landingTimer = 2;

			ActionState = ActionStates.Normal;

			if (MovementState == MovementStates.Normal && GetMovementInputValue() > 0.5f)
			{
				//Landing boost when holding forward (See Sonic and the Black Knight)
				if (MoveSpeed < landingBoost)
					MoveSpeed = landingBoost;
				//Always Play VFX
			}
		}

		#region Jump
		[Export]
		public float accelerationJumpSpeed;
		[Export]
		public float jumpPower;
		[Export]
		public float jumpCurve = .95f;
		public bool IsJumping => ActionState == ActionStates.Jumping;
		public bool IsAccelerationJumping => ActionState == ActionStates.AccelJump;
		private bool isJumpClamped; //True after the player releases the jump button
		private bool canJumpDash;
		private bool isAccelerationJump;
		private float currentJumpLength; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .06f; //How fast the jump button needs to be released for an "acceleration jump"
		private void Jump()
		{
			currentJumpLength = 0;
			isJumpClamped = false;
			IsOnGround = false;
			canJumpDash = true;
			ActionState = ActionStates.Jumping;

			VerticalSpeed = jumpPower;
			if (MoveSpeed < 0) //Disallow jumping backwards
				MoveSpeed = 0;

			Animator.Jump();
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpLength >= ACCELERATION_JUMP_LENGTH)
			{
				//Acceleration jump dash?
				if (Controller.verticalAxis.value > 0)
				{
					ActionState = ActionStates.AccelJump;
					MoveSpeed = accelerationJumpSpeed;
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

		public void ResetJumpDash() => canJumpDash = true;
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
		[Export]
		public float homingAttackSpeed;
		public bool IsJumpDashing => ActionState == ActionStates.JumpDashing;
		public bool IsAttacking { get; private set; } //Should the player damage enemies?

		private void CheckJumpDash()
		{
			if (canJumpDash && jumpBufferTimer != 0)
			{
				StartJumpDash();
				jumpBufferTimer = 0;
			}
		}

		private void StartJumpDash()
		{
			canJumpDash = false;
			IsAttacking = true;
			ActionState = ActionStates.JumpDashing;

			if (activeTarget == null)
			{
				MoveSpeed = jumpDashSpeed;
				VerticalSpeed = jumpDashPower;
			}
		}

		private void UpdateJumpDash()
		{
			if (activeTarget != null && IsAttacking)
			{
				MoveSpeed = homingAttackSpeed;
				StrafeSpeed = VerticalSpeed = 0;
				customPhysicsEnabled = true;
				Vector3 travelDirection = (activeTarget.GlobalTransform.origin - GlobalTransform.origin).Normalized();
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
				StrafeSpeed = Mathf.MoveToward(StrafeSpeed, 0, strafeSettings.friction * PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasReleased)
				ActionState = ActionStates.Normal;
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
		}

		private void StartBackflip()
		{
			backflipTimer = 0;
			StrafeSpeed = 0;
			MoveSpeed = -backflipSpeed;

			ActionState = ActionStates.Backflip;
		}

		public void CancelBackflip()
		{
			if(ActionState == ActionStates.Backflip)
				ActionState = ActionStates.Normal;
		}
		#endregion
		#endregion

		#region Break Skills
		[Export]
		public float speedBreakSpeed; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;
		private float breakTimer = 0; //Timer for break skills
		private const float SPEEDBREAK_DELAY = 0.32f;
		private const float BREAK_SKILLS_COOLDOWN = 1f; //Prevent skill spam

		public const float TIME_BREAK_RATIO = .5f; //Time scale

		private void UpdateTimeBreak()
		{
			if (!IsTimeBreakActive && breakTimer != 0) return; //Cooldown

			if (Controller.breakButton.wasPressed && !IsSpeedBreakActive)
			{
				if (GameplayInterface.instance.IsSoulGaugeCharged)
					ToggleTimeBreak();
			}
		}

		private void UpdateSpeedBreak()
		{
			if (!IsSpeedBreakActive && breakTimer != 0) return; //Cooldown

			if (Controller.boostButton.wasPressed && !IsTimeBreakActive)
			{
				if (GameplayInterface.instance.IsSoulGaugeCharged)
					ToggleSpeedBreak();
			}

			if (IsSpeedBreakActive)
			{
				if (breakTimer == 0)
					MoveSpeed = speedBreakSpeed;
				else
				{
					MoveSpeed *= .8f;
					StrafeSpeed = 0;
					breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
				}
			}
		}

		public void ToggleTimeBreak()
		{
			soulGaugeDrainTimer = 0;
			IsTimeBreakActive = !IsTimeBreakActive;
			Engine.TimeScale = IsTimeBreakActive ? TIME_BREAK_RATIO : 1f;

			if (!IsTimeBreakActive)
			{
				breakTimer = BREAK_SKILLS_COOLDOWN;
				GameplayInterface.instance.UpdateSoulGaugeColor();
			}
		}

		public void ToggleSpeedBreak()
		{
			if (IsSpeedBreakActive)
				MoveSpeed = moveSettings.speed;

			IsSpeedBreakActive = !IsSpeedBreakActive;
			breakTimer = IsSpeedBreakActive ? SPEEDBREAK_DELAY : BREAK_SKILLS_COOLDOWN;
			GameplayInterface.instance.UpdateSoulGaugeColor();
		}

		private int soulGaugeDrainTimer;
		private const int TIME_BREAK_SOUL_DRAIN_INTERVAL = 3; //Drain 1 point every x frames

		private void UpdateBreakTimer()
		{
			if (!IsUsingBreakSkills)
				breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
			else if (breakTimer == 0)
			{
				if (IsSpeedBreakActive)
				{
					GameplayInterface.instance.ModifySoulPearl(-1);
					if (GameplayInterface.instance.IsSoulGaugeEmpty)
						ToggleSpeedBreak();
				}
				else
				{
					if (soulGaugeDrainTimer == 0)
					{
						GameplayInterface.instance.ModifySoulPearl(-1);
						soulGaugeDrainTimer = TIME_BREAK_SOUL_DRAIN_INTERVAL;

						if (GameplayInterface.instance.IsSoulGaugeEmpty)
							ToggleTimeBreak();
					}
					soulGaugeDrainTimer--;
				}
			}
		}
		#endregion
		#endregion

		#region Damage
		private bool IsInvincible => invincibliltyTimer != 0;
		private float invincibliltyTimer;
		private const float INVINCIBILITY_LENGTH = 5f;

		private void UpdateInvincibility()
		{
			if (IsInvincible)
			{
				invincibliltyTimer = Mathf.MoveToward(invincibliltyTimer, 0, PhysicsManager.physicsDelta);

				if (!IsInvincible && queuedDamage.Count != 0)
					TakeDamage(); //Do it again!
			}
		}

		[Export]
		public ControlLockoutResource attackLockoutSettings;
		public bool IsBeingDamaged => ActionState == ActionStates.Damaged;
		private readonly Array<Node> queuedDamage = new Array<Node>();

		private void UpdateDamage()
		{
			if (IsOnGround)
				ActionState = ActionStates.Normal;

			VerticalSpeed -= gravity * PhysicsManager.physicsDelta;
		}

		public void TakeDamage(Node s = null)
		{
			if(IsInvincible && s != null)
			{
				queuedDamage.Add(s);
				return;
			}

			if (MovementState == MovementStates.Normal)
			{
				//Bonk~
				MoveSpeed = -4;
				StrafeSpeed = 0;
				VerticalSpeed = 8;
				IsOnGround = false;
			}
			else if(MovementState == MovementStates.Sidle)
			{
				sidleTimer = SIDLE_DAMAGE_LENGTH;
				MoveSpeed = 0;
				VerticalSpeed = 0;
			}

			invincibliltyTimer = INVINCIBILITY_LENGTH;
			ActionState = ActionStates.Damaged;
		}

		public void CancelDamage(Node n)
		{
			if (queuedDamage.Contains(n))
				queuedDamage.Remove(n);
		}

		public void Kill()
		{
			if (StageSettings.instance.missionModifier == StageSettings.MissionModifier.Deathless)
			{
				//Stage over
				return;
			}

			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;
			PathFollower.HOffset = PathFollower.VOffset = 0;

			//TODO Play death animation depending on the result
			Respawn();
		}

		public void Respawn()
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			if (Triggers.CheckpointTrigger.activeCheckpoint != null)
				GlobalTransform = Triggers.CheckpointTrigger.activeCheckpoint.RespawnTransform;
			else
				Transform = Transform.Identity;

			StageSettings.instance.RespawnObjects();
		}
		#endregion

		#region Enemy Interaction
		[Export]
		public float enemyBouncePower;
		[Export]
		public float enemyBounceGravity;
		private bool IsBouncingOffEnemy => ActionState == ActionStates.EnemyBounce;
		private float bounceTimer;
		private const float BOUNCE_LOCKOUT_TIME = .1f;
		private void UpdateEnemyBounce()
		{
			MoveSpeed = StrafeSpeed = 0;
			VerticalSpeed -= enemyBounceGravity * PhysicsManager.physicsDelta;
			if (VerticalSpeed < 0)
				ActionState = ActionStates.Normal;

			bounceTimer = Mathf.MoveToward(bounceTimer, 0, PhysicsManager.physicsDelta);
			if (bounceTimer == 0) //Bouncing off an enemy
			{
				CheckJumpDash();
				CheckStomp();
			}
		}

		public void HitEnemy(Vector3 enemyPos) //Called when defeating an enemy
		{
			Transform t = GlobalTransform;
			t.origin = enemyPos;
			GlobalTransform = t;
			bounceTimer = BOUNCE_LOCKOUT_TIME;

			if (activeTarget != null) //Reset Active Target
			{
				activeTarget = null;
				GameplayInterface.instance.DisableHomingReticle();
			}

			MoveSpeed = 0;
			canJumpDash = true;
			VerticalSpeed = enemyBouncePower;
			SetControlLockout(attackLockoutSettings);
			ActionState = ActionStates.EnemyBounce;
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
			MovementState = MovementStates.Sidle;
		}

		private void UpdateSidle()
		{
			switch (ActionState)
			{
				case ActionStates.Normal:
					break;
				case ActionStates.Damaged: //Fall
					sidleTimer = Mathf.MoveToward(sidleTimer, 0, PhysicsManager.physicsDelta);

					if (sidleTimer == 0)
					{
						PathFollower.HOffset = Mathf.Lerp(PathFollower.HOffset, -1, SIDLE_HORIZONTAL_SPEED);

						if (currentRailing != null)
						{
							float targetY = currentRailing.GlobalTransform.origin.y;
							Transform t = GlobalTransform;
							t.origin.y = Mathf.MoveToward(t.origin.y, targetY, SIDLE_RAIL_FALL_SPEED * PhysicsManager.physicsDelta); //Snap to railing
							GlobalTransform = t;

							if (Mathf.IsEqualApprox(t.origin.y, targetY))
							{
								ActionState = ActionStates.Hanging;
								sidleTimer = 5f;
								PathFollower.HOffset = -1f; //All railings MUST be 1 unit away from the wall.
							}
						}
						else
							ApplyGravity();
					}
					return;
				case ActionStates.Hanging:
					sidleTimer = Mathf.MoveToward(sidleTimer, 0, PhysicsManager.physicsDelta);

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

					return;
				default:
					UpdateNormalState(); //Busy with a previous action
					return;
			}

			MoveSpeed = sidleSettings.Interpolate(MoveSpeed, GetMovementInputValue());
		}

		public void StopSidle()
		{
			MovementState = MovementStates.Normal;
		}

		#endregion

		#region Launcher
		private float launcherTime;
		private Launcher activeLauncher;
		public void StartLauncher(Launcher newLauncher)
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Launcher;
			activeLauncher = newLauncher;

			MoveSpeed = 0;
			VerticalSpeed = 0;
			StrafeSpeed = 0;

			IsOnGround = false;
			launcherTime = 0;
		}

		private void UpdateLauncher()
		{
			customPhysicsEnabled = true;
			Transform t = GlobalTransform;
			if (!activeLauncher.IsCharacterCentered)
				t.origin = activeLauncher.RecenterCharacter();
			else
			{
				t.origin = activeLauncher.InterpolatePosition(launcherTime);
				if (activeLauncher.IsLauncherFinished(launcherTime)) //Revert to normal state
				{
					MovementState = MovementStates.Normal;
					MoveSpeed = activeLauncher.InitialHorizontalVelocity;
					VerticalSpeed = activeLauncher.FinalVerticalVelocity;
				}

				launcherTime += PhysicsManager.physicsDelta;
			}
			GlobalTransform = t;
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
		public bool IsGrindStepping { get; private set; }
		private GrindRail grindRail;
		private const float MINIMUM_GRIND_SPEED = 2f;

		public void StartGrinding(GrindRail newRail, Vector3 railPosition)
		{
			if (IsGrindStepping)
				GameplayInterface.instance.AddBonus(GameplayInterface.BonusTypes.GrindStep);

			IsGrindStepping = false;
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Grinding;
			grindRail = newRail;

			StrafeSpeed = 0;
			VerticalSpeed = 0;

			MoveSpeed = grindShuffleSpeed;
			Transform t = GlobalTransform;
			t.origin = railPosition;
			GlobalTransform = t;

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
				StopGrinding();
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
			ResyncPathFollower();

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
			MovementState = MovementStates.Normal;
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
			Transform t = GlobalTransform;
			float distance = t.origin.RemoveVertical().DistanceTo(targetPosition.RemoveVertical());

			if (activeDriftCorner.cornerCleared)
			{
				MoveSpeed = moveSettings.speed * Triggers.DriftTrigger.SPEED_RATIO;
				t.origin = t.origin.MoveToward(targetPosition, MoveSpeed * PhysicsManager.physicsDelta);

				if (distance < activeDriftCorner.slideDistance * .1f)
					StopDrift();
			}
			else
			{
				t.origin = activeDriftCorner.Interpolate(t.origin);

				if (distance < .5f)
				{
					if (jumpBufferTimer != 0)
					{
						jumpBufferTimer = 0;
						activeDriftCorner.cornerCleared = true;

						t.origin = new Vector3(targetPosition.x, t.origin.y, targetPosition.z); //Snap to target position
					}
					else if(distance < .1f)
						StopDrift();
				}

				UpdateTimeBreak();
			}

			GlobalTransform = t;
			ResyncPathFollower();
		}

		private void StopDrift()
		{
			//Drift finished
			activeDriftCorner.CompleteDrift(true);
			MovementState = MovementStates.Normal;
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
		public float SpeedRatio => moveSettings.GetSpeedRatio(MoveSpeed);
		private Vector3 velocity; //x -> strafe, y -> jump/fall, z -> speed
		public Vector3 Velocity => isPathMovingForward ? PathFollower.GlobalTransform.basis.Xform(velocity) : PathFollower.GlobalTransform.basis.XformInv(velocity);

		public bool IsFalling => VerticalSpeed < 0;
		public bool IsRising => VerticalSpeed > 0;
		public bool IsIdling => Mathf.Abs(StrafeSpeed) < .1f && Mathf.Abs(MoveSpeed) < .1f;

		public Vector3 CenterPosition => GlobalTransform.origin + worldDirection * collisionHeight; //Center of collision calculations

		public Vector3 ForwardDirection => PathFollower.Forward() * PathTravelDirection;
		public Vector3 StrafeDirection { get; private set; }

		private readonly Array<RespawnableObject> activeTriggers = new Array<RespawnableObject>();
		private readonly Array<Spatial> activeTargets = new Array<Spatial>(); //List of targetable objects
		private Spatial activeTarget; //Active homing attack target
		private void UpdateTriggers()
		{
			//Stage objects
			for (int i = 0; i < activeTriggers.Count; i++)
				activeTriggers[i].OnStay();

			bool isLockedOn = activeTarget != null;
			//Validate current lockon target
			if (isLockedOn && IsTargetInvalid(activeTarget))
				activeTarget = null;

			//Update homing attack
			if (activeTarget == null)
			{
				float closestDistance = Mathf.Inf;
				//Pick new target
				for (int i = 0; i < activeTargets.Count; i++)
				{
					if (IsTargetInvalid(activeTargets[i]) || !canJumpDash)
						continue;

					float dst = activeTargets[i].GlobalTransform.origin.RemoveVertical().DistanceSquaredTo(GlobalTransform.origin.RemoveVertical());
					if (dst > closestDistance)
						continue;

					closestDistance = dst;
					activeTarget = activeTargets[i];
				}
			}

			//Disable Homing Attack
			if (activeTarget == null && isLockedOn)
				GameplayInterface.instance.DisableHomingReticle();
			else if (activeTarget != null)
			{
				Vector2 screenPos = CameraController.instance.ConvertToScreenSpace(activeTarget.GlobalTransform.origin);
				GameplayInterface.instance.UpdateHomingReticle(screenPos, !isLockedOn);
			}
		}

		private bool IsTargetInvalid(Spatial t) => !activeTargets.Contains(t) || !t.IsVisibleInTree() || IsOnGround || IsBeingDamaged;

		public void OnCollisionObjectEnter(PhysicsBody body)
		{
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're being crushed
				RaycastHit hit = this.CastRay(CenterPosition, worldDirection * 5f, environmentMask, false);
				if (hit.collidedObject == body)
				{
					GD.Print($"Crushed by {body.Name}");
					AddCollisionExceptionWith(body); //Avoid clipping through the ground
					TakeDamage();
				}
			}
		}

		public void OnCollisionObjectExit(PhysicsBody body)
		{
			if (body.IsInGroup("crusher") && GetCollisionExceptions().Contains(body))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
				CancelDamage(body);
			}
		}

		public void OnObjectTriggerEnter(Area area)
		{
			if (!((Node)area is RespawnableObject))
			{
				if (area.IsInGroup("railing"))
					currentRailing = area;

				return;
			}

			RespawnableObject target = (Node)area as RespawnableObject;
			target.OnEnter();

			if (!activeTriggers.Contains(target))
				activeTriggers.Add(target);
		}

		public void OnObjectTriggerExit(Area area)
		{
			if (!((Node)area is RespawnableObject))
			{
				if (area.IsInGroup("railing"))
					currentRailing = null;

				return;
			}

			RespawnableObject target = (Node)area as RespawnableObject;
			target.OnExit();

			if (activeTriggers.Contains(target))
				activeTriggers.Remove(target);
		}

		public void OnTargetTriggerEnter(Area area)
		{
			if (!activeTargets.Contains(area))
				activeTargets.Add(area);
		}

		public void OnTargetTriggerExit(Area area)
		{
			if (activeTargets.Contains(area))
				activeTargets.Remove(area);
		}

		private void UpdatePhysics()
		{
			UpdateTriggers();
			if (customPhysicsEnabled) return; //When physics are handled in the state machine

			/*Movement method
			Move path follower
			Perform collision checks
			Move character
			Resync
			*/
			float movementDelta = MoveSpeed * PhysicsManager.physicsDelta;
			Vector3 movementDirection = ForwardDirection;

			if (PathFollower.Loop)
				PathFollower.Offset += movementDelta * PathTravelDirection;
			else
			{
				if (Mathf.IsZeroApprox(pathFollowerOffset))
				{
					float oldOffset = PathFollower.Offset;
					PathFollower.Offset += movementDelta * PathTravelDirection;

					if (PathFollower.UnitOffset >= 1f || PathFollower.UnitOffset <= 0)
					{
						//Extrapolate path
						float extra = Mathf.Abs(movementDelta) - Mathf.Abs(oldOffset - PathFollower.Offset);
						pathFollowerOffset += Mathf.Sign(movementDelta) * extra;
					}
				}
				else
				{
					int oldSign = Mathf.Sign(pathFollowerOffset);
					pathFollowerOffset += movementDelta;

					//Merge back onto the path
					if (Mathf.Sign(pathFollowerOffset) != oldSign)
					{
						PathFollower.Offset += pathFollowerOffset;
						pathFollowerOffset = 0;
					}
				}
			}

			//Use the average direction sampled from before and after changing the offset.
			//Increases accuracy around turns.
			movementDirection = movementDirection.LinearInterpolate(ForwardDirection, .5f).Normalized();

			CheckMainWall(movementDirection);
			CheckGround();

			StrafeDirection = ForwardDirection.Cross(worldDirection).Normalized();
			strafeCollision = StrafeCollisions.None;

			CheckStrafeWall(1);
			CheckStrafeWall(-1);

			if (!IsOnGround && ActionState == ActionStates.JumpDashing)
				movementDirection = movementDirection.Flatten().Normalized();
			MoveAndSlide(movementDirection * MoveSpeed + StrafeDirection * StrafeSpeed + worldDirection * VerticalSpeed);
			CheckCeiling();

			ResyncPathFollower();
		}

		public Vector3 worldDirection = Vector3.Up;

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
			float castLength = collisionHeight;

			if (IsOnGround)
				castLength += GROUND_SNAP_LENGTH;
			else if (IsFalling)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;
			else if (IsRising)
				castLength = -.1f; //Fix allow jumping

			Vector3 castVector = -worldDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!groundHit || groundHit.collidedObject.IsInGroup("wall")) //Whisker casts
			{
				Vector3 startingDirection = (ForwardDirection.y > 0 ? 1 : -1) * ForwardDirection; //Fix weird snapping
				for (int i = 0; i < 8; i++)
				{
					Vector3 castOffset = startingDirection.Rotated(worldDirection, Mathf.Tau * .125f * i) * collisionWidth * .5f;
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

				Vector3 newNormal = (groundHit.normal * 100).Round() * .01f; //FIX Round to nearest hundredth to reduce jittering
				worldDirection = newNormal.Normalized();

				if(!IsOnGround)
					LandOnGround();

				Transform t = GlobalTransform;
				t.origin = groundHit.point;
				GlobalTransform = t;
			}
			else
			{
				if (IsOnGround && !IsBackflipping)
					Animator.FallAnimation();
				
				IsOnGround = false;

				if (IsBackflipping) return;

				Debug.DrawRay(CenterPosition, worldDirection * 5, Colors.Blue);

				worldDirection = worldDirection.LinearInterpolate(Vector3.Up, Mathf.Clamp((VerticalSpeed / maxGravity) - .1f, 0f, 1f)).Normalized();
				/*
				if (!jumpedFromGround || IsFalling)
				else //Stay aligned with the path (Somewhat)
				*/
				//worldDirection = worldDirection.LinearInterpolate(-ForwardDirection.Cross(StrafeDirection), .05f).Normalized();
			}
		}

		private void CheckCeiling() //Checks the ceiling.
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = collisionHeight;

			Vector3 castVector = worldDirection * castLength;
			if (IsRising)
				castVector.y += VerticalSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GlobalTranslate(ceilingHit.point - (CenterPosition + worldDirection * collisionHeight));

				if (IsRising)
					VerticalSpeed = 0;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (MoveSpeed == 0) return; //No movement.

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = collisionHeight + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;
			Vector3 sidewaysOffset = StrafeDirection * collisionWidth * .5f;

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
				float wallRatio = Mathf.Abs(CompareNormal2D(centerHit.normal, ForwardDirection));
				if (wallRatio > .9f)
				{
					if (IsSpeedBreakActive && breakTimer == 0) //Cancel speed break
						ToggleSpeedBreak();

					MoveSpeed = 0;
				}
			}
		}

		private bool IsValidWallCast(RaycastHit hit) => hit && (Mathf.Abs(CompareNormal2D(hit.normal, hit.direction)) >= .5f && !hit.collidedObject.IsInGroup("ignore raycast"));

		private StrafeCollisions strafeCollision;
		private enum StrafeCollisions
		{
			None,
			Left,
			Right,
			Both
		}
		private const float STRAFE_SMOOTHING_LENGTH = .1f; //At what distance to apply smoothing to strafe (To avoid "Bumpy Corners")

		//Checks for wall collision side to side. (Always active)
		private void CheckStrafeWall(int direction)
		{
			if (isSideScroller) return; //No wall checks when sidescrolling.

			bool isActiveDirection = Mathf.Sign(StrafeSpeed) == direction;
			Vector3 castOrigin = CenterPosition;
			float castLength = (collisionWidth + STRAFE_SMOOTHING_LENGTH);
			castLength += Mathf.Abs(StrafeSpeed) * PhysicsManager.physicsDelta;

			Vector3 castVector = StrafeDirection * castLength * direction;
			RaycastHit hit = this.CastRay(castOrigin, castVector, environmentMask, false, GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, hit ? Colors.Red : Colors.White);

			if (hit)
			{
				if (isActiveDirection)
				{
					//Only process active collision
					float dot = Mathf.Abs(CompareNormal2D(hit.normal, StrafeDirection));

					if (dot > .8f)
					{
						if (hit.distance > collisionWidth)
						{
							//Smooth out wall collision
							float speedClamp = (hit.distance - collisionWidth) / STRAFE_SMOOTHING_LENGTH;
							StrafeSpeed = Mathf.Clamp(StrafeSpeed, -speedClamp, speedClamp);
						}
						else
							StrafeSpeed = 0;
					}
					else
					{
						float maxSpeed = strafeSettings.speed * (1 - dot);
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -maxSpeed, maxSpeed);
					}
				}

				if (hit.distance <= collisionWidth)
				{
					if (strafeCollision == StrafeCollisions.None)
						strafeCollision = direction > 0 ? StrafeCollisions.Right : StrafeCollisions.Left;
					else
						strafeCollision = StrafeCollisions.Both;
				}
			}
		}

		//Returns the absolute dot product of a normal relative to an axis ignoring Y values.
		private float CompareNormal2D(Vector3 normal, Vector3 axis) => normal.RemoveVertical().Normalized().Dot(axis.RemoveVertical().Normalized());
		#endregion

		#region Paths
		public Path ActivePath { get; private set; }
		public bool isPathMovingForward = true; //Set this to false to move backwards along the path (Useful for reverse acts and stuff)
		public int PathTravelDirection => isPathMovingForward ? 1 : -1;
		public void SetActivePath(Path newPath)
		{
			if (newPath == null) return;

			if (PathFollower.IsInsideTree())
				PathFollower.GetParent().RemoveChild(PathFollower);

			ActivePath = newPath;
			PathFollower.Loop = newPath.Curve.IsLoopingPath();

			//Reset offset transform
			pathFollowerOffset = 0;

			newPath.AddChild(PathFollower);
			ResyncPathFollower();
		}

		private void ResyncPathFollower()
		{
			if (!PathFollower.IsInsideTree()) return;
			if (ActivePath == null || pathFollowerOffset != 0) return;

			PathFollower.Offset = ActivePath.Curve.GetClosestOffset(GlobalTransform.origin - ActivePath.GlobalTransform.origin);

			if (isSideScroller)
				RecenterStrafe();
		}
		#endregion
	}
}
