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
		private AnimationTree _animator;
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionHeight = .4f;
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionWidth = .3f;
		[Export]
		public NodePath root;
		private Spatial _root;

		#region Controls
		public InputManager.Controller Controller => InputManager.controller;

		public bool isSideScroller; //Are we in a 2D section?
		public bool isFacingRight; //Determines which way on the controller is back (Only in sidescroller)
		private int GetMovementInputDirection()
		{
			//Returns 1 for moving forward, -1 for moving backwards
			if (!isSideScroller)
				return Controller.verticalAxis.sign;

			return isFacingRight ? Controller.horizontalAxis.sign : -Controller.horizontalAxis.sign;
		}
		private int GetStrafeInputDirection()
		{
			//Returns 1 for moving right, -1 for moving left
			if (!isSideScroller)
				return Controller.horizontalAxis.sign;

			return isFacingRight ? Controller.verticalAxis.sign : -Controller.verticalAxis.sign;
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
			_animator.Set("parameters/countdown/active", true);
		}

		public void OnCountdownCompleted()
		{
			isCountdownActive = false;
		}

		#endregion

		#region Automation
		private Vector3 automationDifference;
		public void StartAutomation()
		{
			MovementState = MovementStates.Automation;
			automationDifference = GlobalTransform.origin - PathFollower.GlobalTransform.origin;
			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;

			UpdateAutomation();
		}

		private void UpdateAutomation()
		{
			MoveSpeed = moveSettings.speed;
			PathFollower.Offset += MoveSpeed * PhysicsManager.physicsDelta;

			customPhysicsEnabled = true;
			automationDifference = automationDifference.MoveToward(Vector3.Zero, MoveSpeed * PhysicsManager.physicsDelta);
			Transform t = GlobalTransform;
			t.basis = PathFollower.GlobalTransform.basis;
			t.origin = PathFollower.GlobalTransform.origin + automationDifference;
			GlobalTransform = t;
		}

		public void StopAutomation()
		{
			MovementState = MovementStates.Normal;
		}
		#endregion
		#endregion

		public override void _Ready()
		{
			instance = this;

			PathFollower = GetNode<PathFollow>(pathFollower);

			_animator = GetNode<AnimationTree>(animator);
			_animator.Active = true;

			_root = GetNode<Spatial>(root);
		}

		public override void _PhysicsProcess(float _)
		{
			ProcessStateMachine();

			if (JustLandedOnGround)
			{
				landingResetTimer--;
				if(landingResetTimer == 0)
					LandOnGround();
			}

			UpdatePhysics();
			UpdateAnimation();
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
			Damaged, //Being knocked back by damage
			Respawning,
			Automation,  //Cutscene? Cinematics? May be replaced with input lockout.
			Launcher, //Springs, Ramps, etc.
			Drift, //Sharp 90 degree corner. Press jump at the right moment to get a burst of speed?
			Grinding, //Grinding on rails
			Catapult, //Aiming a catapult
			FlyingPot, //A flying pot -_-
			AirLauncher, //Pressing jump at the right time will move to a launcher state. May have this state combined into LAUNCHER.
			Rope, //A swinging rope
			ZipLine, //Swinging zip line
			Surfing, //Left and right movement
			FlyingCarpet //4 way air movement
		}

		public ActionStates ActionState { get; private set; }
		public enum ActionStates //Actions that can happen in the Normal MovementState
		{
			Normal,
			JumpDashing, //Also includes homing attack
			EnemyBounce, //Struck an enemy with an attack
			Stomping,
			Backflip,
			Damage,
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
				case MovementStates.Damaged:
					UpdateDamage();
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

			UpdateControlLockTimer();
			UpdateBreakTimer();
		}
		#endregion

		#region Normal State
		private void UpdateNormalState()
		{
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
					MoveSpeed = moveSettings.Interpolate(MoveSpeed, GetMovementInputDirection());
				else
					MoveSpeed = backstepSettings.Interpolate(MoveSpeed, -GetMovementInputDirection(), true); //Input direction needs to be inverted for negative movement
			}
			else
				MoveSpeed = airMoveSettings.Interpolate(MoveSpeed, GetMovementInputDirection());
		}

		private void UpdateStrafeSpeed()
		{
			if (isControlsLocked && ControlLockoutData.strafeSettings != ControlLockoutResource.StrafeSettings.Default)
			{
				if (ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.Recenter)
				{
					//Calculate distance along the plane defined by StrafeDirection
					Vector3 calculationPoint = GlobalTransform.origin - PathFollower.GlobalTransform.origin;
					Vector3 rotationAxis = StrafeDirection.Cross(Vector3.Right).Normalized();
					if (rotationAxis.IsNormalized())
						calculationPoint = calculationPoint.Rotated(rotationAxis, StrafeDirection.AngleTo(Vector3.Right));

					float distanceFromCenter = calculationPoint.x;

					if (MoveSpeed != 0)
					{
						StrafeSpeed = Mathf.MoveToward(StrafeSpeed, -distanceFromCenter / PhysicsManager.physicsDelta, strafeSettings.traction * PhysicsManager.physicsDelta);

						//Speed clamp
						//Center Smoothing
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -Mathf.Abs(MoveSpeed), Mathf.Abs(MoveSpeed));
						float speedClamp = (Mathf.Abs(distanceFromCenter) - collisionWidth) / STRAFE_SMOOTHING_LENGTH;
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -speedClamp, speedClamp);
					}
					else
						StrafeSpeed = strafeSettings.Interpolate(StrafeSpeed, 0);
				}
				else if (ControlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.KeepPosition)
					StrafeSpeed = 0f;

				return;
			}

			if (IsOnGround)
				StrafeSpeed = strafeSettings.Interpolate(StrafeSpeed, GetStrafeInputDirection());
			else
				StrafeSpeed = airStrafeSettings.Interpolate(StrafeSpeed, GetStrafeInputDirection());
		}

		#region Actions
		[Export]
		public float gravity;
		[Export]
		public float maxGravity;
		private void UpdateActions()
		{
			if (IsStomping)
				return;

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

			if (jumpBufferTimer != 0)
			{
				jumpBufferTimer = 0;
				if (GetMovementInputDirection() < 0)
					StartBackflip();
				else
					Jump();
			}
		}

		private void UpdateAirActions()
		{
			CheckStomp();
			CheckJumpDash();

			if (jumpedFromGround)
				UpdateJump();

			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, maxGravity, gravity * PhysicsManager.physicsDelta); //Apply Gravity
		}

		[Export]
		public float landingBoost; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private void LandOnGround()
		{
			isJumping = false;
			IsAttacking = false;
			canJumpDash = true;
			isGrindStepping = false;
			jumpedFromGround = false;
			isAccelerationJump = false;
			currentJumpLength = 0;

			ActionState = ActionStates.Normal;

			if (GetMovementInputDirection() > 0)
			{
				if (MoveSpeed < landingBoost)
					MoveSpeed = landingBoost;
				//Boost forwards slightly when holding forward (See Sonic and the Black Knight)
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
		private bool isJumping;
		private bool canJumpDash;
		private bool jumpedFromGround;
		private bool isAccelerationJump;
		private float jumpGroundNormal;
		private float currentJumpLength; //Amount of time the jump button was held
		private const float ACCELERATION_JUMP_LENGTH = .06f; //How fast the jump button needs to be pressed for an "acceleration jump"
		private void Jump()
		{
			isJumping = true;
			IsOnGround = false;
			jumpedFromGround = true;
			ActionState = ActionStates.Normal;
			jumpGroundNormal = ForwardDirection.y;

			VerticalSpeed = jumpPower;
			if (MoveSpeed < 0) //Disallow jumping backwards
				MoveSpeed = 0;
		}

		private void UpdateJump()
		{
			bool checkForAccelerationJump = currentJumpLength < ACCELERATION_JUMP_LENGTH;
			currentJumpLength += PhysicsManager.physicsDelta;
			if (checkForAccelerationJump && currentJumpLength >= ACCELERATION_JUMP_LENGTH)
			{
				if (isAccelerationJump)
				{
					//Acceleration jump dash
					if (Controller.verticalAxis.value > 0 && MoveSpeed < accelerationJumpSpeed)
						MoveSpeed = accelerationJumpSpeed;
					VerticalSpeed = 5f;
					isAccelerationJump = false;
				}
				else
					IsAttacking = true; //Spin Attack
			}

			if (isJumping)
			{
				if (!Controller.jumpButton.isHeld)
				{
					isJumping = false;
					if (currentJumpLength <= ACCELERATION_JUMP_LENGTH)
						isAccelerationJump = true;
				}
			}
			else if (IsRising)
				VerticalSpeed *= jumpCurve;
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
		private bool IsJumpDashing => ActionState == ActionStates.JumpDashing;
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
			if (activeTarget != null)
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

		#region Stomp
		[Export]
		public float stompSpeed;
		private bool IsStomping => ActionState == ActionStates.Stomping;
		private void StartStomping()
		{
			MoveSpeed = 0;
			VerticalSpeed = stompSpeed;
			actionBufferTimer = 0;
			ActionState = ActionStates.Stomping;
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

			Vector3 castVector = -worldDirection * 10f;
			RaycastHit hit = this.CastRay(CenterPosition, castVector, environmentMask);
			Debug.DrawRay(CenterPosition, castVector, hit ? Colors.Red : Colors.White);
			if (hit)
				worldDirection = worldDirection.LinearInterpolate(hit.normal, .2f).Normalized();
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
		[Export]
		public ControlLockoutResource attackLockoutSettings;
		public bool IsDamaged => MovementState == MovementStates.Damaged;
		private void UpdateDamage()
		{
			if (IsOnGround)
				MovementState = MovementStates.Normal;

			VerticalSpeed -= gravity * PhysicsManager.physicsDelta;
		}

		public void TakeDamage()
		{
			if (MovementState == MovementStates.Normal)
			{
				MoveSpeed = -4f;
				StrafeSpeed = 0f;
				VerticalSpeed = 8f;
				IsOnGround = false;
				ActionState = ActionStates.Normal;
			}

			MovementState = MovementStates.Damaged;
		}

		public void Kill()
		{
			if (StageManager.instance.missionModifier == StageManager.MissionModifier.Deathless)
			{
				//Stage over
				return;
			}

			MoveSpeed = 0;
			StrafeSpeed = 0;
			VerticalSpeed = 0;

			//TODO Play death animation depending on the result
			Respawn();
		}

		public void Respawn()
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Normal;

			if (CheckpointTrigger.activeCheckpoint != null)
				GlobalTransform = CheckpointTrigger.activeCheckpoint.GlobalTransform;
			else
				Transform = Transform.Identity;

			StageManager.instance.RespawnObjects();
		}
		#endregion

		#region Enemy Interaction
		[Export]
		public float enemyBouncePower;
		[Export]
		public float enemyBounceGravity;
		private bool IsBouncingOffEnemy => ActionState == ActionStates.EnemyBounce;
		private float bounceTimer;
		private const float BOUNCE_LOCKOUT_TIME = .32f;
		private void UpdateEnemyBounce()
		{
			bounceTimer = Mathf.MoveToward(bounceTimer, 0, PhysicsManager.physicsDelta);
			if (bounceTimer == 0) //Bouncing off an enemy
			{
				CheckStomp();
				CheckJumpDash();
			}

			MoveSpeed = StrafeSpeed = 0;
			VerticalSpeed -= enemyBounceGravity * PhysicsManager.physicsDelta;
			if (VerticalSpeed < 0)
				ActionState = ActionStates.Normal;
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

		#region Launcher
		private Launcher activeLauncher;
		private Vector3 launcherMovementDelta;
		public void StartLauncher(Launcher newLauncher)
		{
			ActionState = ActionStates.Normal;
			MovementState = MovementStates.Launcher;
			activeLauncher = newLauncher;

			MoveSpeed = 0;
			VerticalSpeed = 0;
			StrafeSpeed = 0;

			IsOnGround = false;
			jumpedFromGround = false;
			launcherMovementDelta = Vector3.Zero;
		}

		private void UpdateLauncher()
		{
			customPhysicsEnabled = true;
			if (!activeLauncher.IsCharacterCentered)
			{
				Transform t = GlobalTransform;
				t.origin = activeLauncher.CenterCharacter();
				GlobalTransform = t;
				return;
			}

			Vector3 movementDelta = activeLauncher.CalculateMovementDelta();
			MoveAndSlide(movementDelta);
			if (movementDelta != Vector3.Zero)
				launcherMovementDelta = movementDelta;

			if (activeLauncher.IsLaunchFinished) //Revert to normal state
			{
				MovementState = MovementStates.Normal;

				//Calculate velocity to carry through based on the launch direction
				float exitSpeed = launcherMovementDelta.Length() * activeLauncher.momentumMultiplier;  //"True" exit speed
				Vector3 exitNormal = launcherMovementDelta.Normalized();
				VerticalSpeed = exitSpeed * exitNormal.y;
				float forwardDotProd = exitNormal.RemoveVertical().Normalized().Dot(ForwardDirection.RemoveVertical().Normalized());
				MoveSpeed = exitSpeed * forwardDotProd;
				StrafeSpeed = exitSpeed * (1 - Mathf.Abs(forwardDotProd)) * Mathf.Sign(forwardDotProd);
			}
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
		private bool isGrindStepping;
		private GrindRail grindRail;
		private const float MINIMUM_GRIND_SPEED = 2f;

		public void StartGrinding(GrindRail newRail, Vector3 railPosition)
		{
			if (isGrindStepping)
				GameplayInterface.instance.AddBonus(GameplayInterface.BonusTypes.GrindStep);

			isGrindStepping = false;
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
			_animator.Set("parameters/balancing/current", 1); //Turn on grinding animations
			_animator.Set("parameters/balance_state/active", true);
			_animator.Set("parameters/balance_state/balance_left/blend_position", 0);
			_animator.Set("parameters/balance_state/balance_right/blend_position", 0);
		}

		private void UpdateGrinding()
		{
			customPhysicsEnabled = true;
			MoveSpeed = grindingSettings.Interpolate(MoveSpeed, GetMovementInputDirection());
			
			//Update Shuffle
			AnimationNodeStateMachinePlayback grindState = _animator.Get("parameters/balance_state/playback") as AnimationNodeStateMachinePlayback;
			string currentAnimation = grindState.GetCurrentNode();

			if (jumpBufferTimer != 0)
			{
				StopGrinding();
				jumpBufferTimer = 0;

				int direction = GetStrafeInputDirection();
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
					isGrindStepping = true;
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

		public bool CanGrind() =>  MovementState != MovementStates.Grinding && !IsRising && (!IsOnGround || JustLandedOnGround);

		public void StopGrinding()
		{
			grindRail = null;
			_animator.Set("parameters/balancing/current", 0); //Turn off grinding animations
			MovementState = MovementStates.Normal;
		}
		#endregion
		#endregion

		#region Drift
		private DriftTrigger activeDriftCorner;
		public void StartDrift(DriftTrigger corner)
		{
			activeDriftCorner = corner;
			MovementState = MovementStates.Drift;

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
				MoveSpeed = moveSettings.speed;
				t.origin = t.origin.MoveToward(targetPosition, MoveSpeed * PhysicsManager.physicsDelta);

				if (distance < activeDriftCorner.slideDistance * .2f)
					StopDrift();
			}
			else
			{
				//Arbitrary, but it works.
				float distanceRatio = (distance - MoveSpeed * PhysicsManager.physicsDelta) / activeDriftCorner.slideDistance;
				distanceRatio = Mathf.Clamp(distanceRatio * 2f + .1f, 0f, 1f);
				MoveSpeed = activeDriftCorner.entrySpeed * distanceRatio;

				if (distance < 1f)
				{
					if (jumpBufferTimer != 0)
					{
						jumpBufferTimer = 0;
						activeDriftCorner.cornerCleared = true;
					}
					else
						StopDrift();

					t.origin = new Vector3(targetPosition.x, t.origin.y, targetPosition.z); //Snap to target position
				}
				else
					t.origin = t.origin.MoveToward(targetPosition, MoveSpeed * PhysicsManager.physicsDelta);

				UpdateTimeBreak();
			}

			GlobalTransform = t;
		}

		private void StopDrift()
		{
			//Drift finished
			ResyncPathFollower();
			activeDriftCorner.Deactivate(true);
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
		public Vector3 Velocity => velocity.Rotated(Vector3.Up, ForwardDirection.RemoveVertical().AngleTo(Vector2.Up));

		private bool IsFalling => VerticalSpeed < 0;
		private bool IsRising => VerticalSpeed > 0;

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

		private bool IsTargetInvalid(Spatial t) => !activeTargets.Contains(t) || !t.IsVisibleInTree() || IsOnGround || IsDamaged;

		public void OnCollisionObjectEnter(PhysicsBody body)
		{
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're being crushed
				RaycastHit hit = this.CastRay(CenterPosition, worldDirection * collisionHeight * 2f, environmentMask, false);
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
			}
		}

		public void OnObjectTriggerEnter(Area area)
		{
			if (!((Node)area is RespawnableObject)) return;

			RespawnableObject target = (Node)area as RespawnableObject;
			target.OnEnter();

			if (!activeTriggers.Contains(target))
				activeTriggers.Add(target);
		}

		public void OnObjectTriggerExit(Area area)
		{
			if (!((Node)area is RespawnableObject)) return;

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
			Move path follower first,
			Perform all nessecary collision checks
			Then move character controller to follow
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

			//Get the average direction of the pathfollow before changing the offset, AND after.
			//Increases accuracy around turns.
			movementDirection = movementDirection.LinearInterpolate(ForwardDirection, .5f);
			//FIX Smooths "bumping" that occours when jumping up a surface that has a sudden change in steepness.
			if (jumpedFromGround && movementDirection.y > jumpGroundNormal)
			{
				jumpGroundNormal = Mathf.Lerp(jumpGroundNormal, movementDirection.y, .1f);
				movementDirection.y = jumpGroundNormal;
			}

			movementDirection = movementDirection.Normalized();

			CheckMainWall(movementDirection);
			CheckGround();

			StrafeDirection = ForwardDirection.Cross(worldDirection).Normalized();
			strafeCollision = StrafeCollisions.None;
			CheckStrafeWall(1);
			CheckStrafeWall(-1);

			MoveAndSlide(movementDirection * MoveSpeed + StrafeDirection * StrafeSpeed + worldDirection * VerticalSpeed);
			CheckCeiling();

			ResyncPathFollower();
		}

		public Vector3 worldDirection = Vector3.Up;
		private const float COLLISION_PADDING = .01f;

		public bool IsOnGround { get; private set; }
		private bool JustLandedOnGround => landingResetTimer != 0; //Flag for doing stuff on land
		private int landingResetTimer;
		private const float GROUND_SNAP_LENGTH = .2f;
		private const float MAX_ANGLE_CHANGE = 60f;

		private void CheckGround()
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = collisionHeight;

			if (IsOnGround || IsStomping) //FIX allow interaction with grind rails
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
				//Fix don't allow 90 degree angle changes in a single frame
				if (Mathf.Rad2Deg(groundHit.normal.AngleTo(Vector3.Up) - worldDirection.AngleTo(Vector3.Up)) > MAX_ANGLE_CHANGE)
					return;

				//Fix microscopic jittering on flat surfaces
				Vector3 newNormal = (groundHit.normal * 100).Round() * .01f; //Round to nearest hundredth
				worldDirection = newNormal.Normalized();

				if(!IsOnGround)
				{
					landingResetTimer = 2;
					IsOnGround = true;
					VerticalSpeed = 0;
				}

				Transform t = GlobalTransform;
				t.origin = groundHit.point;
				GlobalTransform = t;
			}
			else
			{
				IsOnGround = false;

				if (IsBackflipping) return;

				worldDirection = worldDirection.LinearInterpolate(Vector3.Up, Mathf.Clamp((VerticalSpeed / maxGravity) - .1f, 0f, 1f)).Normalized();
				/*
				if (!jumpedFromGround || IsFalling)
				else //Stay aligned with the path (Somewhat)
					worldDirection = worldDirection.LinearInterpolate(-ForwardDirection.Cross(StrafeDirection), .05f).Normalized();
				*/
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
			if (centerHit && Mathf.Abs(CompareNormal2D(centerHit.normal, castVector)) < .5f)
				centerHit = new RaycastHit();

			if (!centerHit)
			{
				//Whiskers
				RaycastHit leftHit = this.CastRay(CenterPosition - sidewaysOffset, castVector * castLength, environmentMask, false, GetCollisionExceptions());
				RaycastHit rightHit = this.CastRay(CenterPosition + sidewaysOffset, castVector * castLength, environmentMask, false, GetCollisionExceptions());
				Debug.DrawRay(CenterPosition - sidewaysOffset, castVector * castLength, leftHit ? Colors.Red : Colors.White);
				Debug.DrawRay(CenterPosition + sidewaysOffset, castVector * castLength, rightHit ? Colors.Red : Colors.White);

				//Ignore collisions that are "side walls"
				if (leftHit && Mathf.Abs(CompareNormal2D(leftHit.normal, castVector)) < .5f)
					leftHit = new RaycastHit();
				if (rightHit && Mathf.Abs(CompareNormal2D(rightHit.normal, castVector)) < .5f)
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

		private StrafeCollisions strafeCollision;
		private enum StrafeCollisions
		{
			None,
			Left,
			Right,
			Both
		}
		private const float STRAFE_SMOOTHING_LENGTH = .08f; //At what distance to apply smoothing to strafe (To avoid "Bumpy Corners")

		//Checks for wall collision side to side. (Always active)
		private void CheckStrafeWall(int direction)
		{
			bool isActiveDirection = Mathf.Sign(StrafeSpeed) == direction;
			Vector3 castOrigin = CenterPosition;
			float castLength = (collisionWidth + STRAFE_SMOOTHING_LENGTH + COLLISION_PADDING);
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

		#region Animation
		private void UpdateAnimation()
		{
			if (ringParticleTimer != 0)
			{
				ringParticleTimer = Mathf.MoveToward(ringParticleTimer, 0, PhysicsManager.physicsDelta);

				if (ringParticleTimer == 0)
					_ringParticleEffect.Emitting = false;
			}

			if(MovementState != MovementStates.Automation)
			{
				Transform t = GlobalTransform;
				t.basis.z = ForwardDirection;
				t.basis.y = worldDirection;
				t.basis.x = -t.basis.z.Cross(t.basis.y);
				t.basis = t.basis.Orthonormalized();
				GlobalTransform = t;
			}
		}

		#region VFX
		[Export]
		public NodePath ringParticleEffect;
		private Particles _ringParticleEffect;
		private float ringParticleTimer;

		public void PlayRingParticleEffect()
		{
			if (_ringParticleEffect == null)
				_ringParticleEffect = GetNode<Particles>(ringParticleEffect);

			ringParticleTimer = .2f;
			_ringParticleEffect.Emitting = true;
		}
		#endregion
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
		}
		#endregion
	}
}
