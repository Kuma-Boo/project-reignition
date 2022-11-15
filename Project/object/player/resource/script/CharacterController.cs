using Godot;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for handling the player's state, physics and basic movement.
	/// </summary>
	//Currently rewriting the entire movement controller...Again.
	public partial class CharacterController : CharacterBody3D
	{
		public static CharacterController instance;

		public CameraController Camera => CameraController.instance;
		public InputManager.Controller Controller => InputManager.controller;
		public StageSettings Stage => StageSettings.instance;

		public int score;

		public override void _EnterTree()
		{
			instance = this;
		}

		public override void _Ready()
		{
			PathFollower.Initialize(); //Attempt to autoload the default path
			CallDeferred(MethodName.ResetOrientation); //Start with proper orientation

			Stage.SetCheckpoint(GetParent<Node3D>()); //Initial checkpoint configuration
			Stage.Connect(StageSettings.SignalName.StageCompleted, new Callable(this, MethodName.OnStageCompleted));
		}

		[Signal]
		public delegate void ProcessedEventHandler(); //Called every frame after the player is done processing

		public override void _PhysicsProcess(double _)
		{
			ProcessStateMachine();
			UpdatePhysics();
			UpdateOrientation();

			Animator.UpdateAnimation();
			Skills.UpdateSoulSkills();

			EmitSignal(SignalName.Processed);
		}

		#region State Machine
		public MovementStates MovementState { get; private set; }
		public enum MovementStates
		{
			Normal, //Standard on rails movement
			External, //Cutscenes, and stage objects that override player control
			Sidle, //Scooting along the wall
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

			//State specific
			Hanging, //Hanging from a ledge (In Sidle Mode)
		}

		public void ResetActionState() //Reset action state
		{
			ActionState = ActionStates.Normal;
		}

		private void ProcessStateMachine()
		{
			if (isCountdownActive) return;
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
		/// <summary> Is the player holding forward, relative to the PathFollower's forward angle? </summary>
		private bool IsHoldingForward
		{
			get
			{
				float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle(), PathFollower.ForwardAngle);
				return !Controller.MovementAxis.IsEqualApprox(Vector2.Zero) && delta < Mathf.Pi * .4f;
			}
		}
		/// <summary> Is the player holding backward, relative to the PathFollower's forward angle? </summary>
		private bool IsHoldingBackward
		{
			get
			{
				float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle() + Mathf.Pi, PathFollower.ForwardAngle);
				return !Controller.MovementAxis.IsEqualApprox(Vector2.Zero) && delta < Mathf.Pi * .4f;
			}
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

			if (IsLockoutActive && currentLockoutData.disableActions) return;

			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private float lockoutTimer;
		private LockoutResource currentLockoutData;
		private bool IsLockoutActive => currentLockoutData != null;
		private readonly List<LockoutResource> lockoutDataList = new List<LockoutResource>();

		/// <summary> Adds a ControlLockoutResource to the list, and switches to it depending on it's priority
		public void AddLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource))
			{
				lockoutDataList.Add(resource); //Add the new lockout data
				if (lockoutDataList.Count >= 2) //List only needs to be sorted if there are multiple elements on it
					lockoutDataList.Sort(new LockoutResource.Comparer());

				if (currentLockoutData != null && currentLockoutData.priority == -1) //Remove current lockout?
					RemoveLockoutData(currentLockoutData);

				if (resource.priority == -1) //Exempt from priority, take over immediately
					SetLockoutData(resource);
				else
					RefreshCurrentLockoutData();
			}
		}

		/// <summary> Removes a ControlLockoutResource from the list </summary>
		public void RemoveLockoutData(LockoutResource resource)
		{
			if (!lockoutDataList.Contains(resource)) return;
			lockoutDataList.Remove(resource);
			RefreshCurrentLockoutData();
		}

		private void RefreshCurrentLockoutData() //Called whenever the lockout list is modified.
		{
			if (IsLockoutActive && lockoutDataList.Count == 0) //Disable lockout
				SetLockoutData(null);
			else if (currentLockoutData != lockoutDataList[lockoutDataList.Count - 1]) //Change to current data (Highest priority, last on the list)
			{
				SetLockoutData(lockoutDataList[lockoutDataList.Count - 1]);
				if (currentLockoutData.resetActions) //Reset actions if needed
					ResetActionState();
			}
		}

		private void SetLockoutData(LockoutResource resource)
		{
			currentLockoutData = resource;

			if (resource != null) //Reset flags
			{
				lockoutTimer = 0;
				isRecentered = false;
			}
		}

		private void UpdateLockoutTimer()
		{
			if (!IsLockoutActive || Mathf.IsZeroApprox(currentLockoutData.length))
				return;

			lockoutTimer = Mathf.MoveToward(lockoutTimer, currentLockoutData.length, PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(lockoutTimer, currentLockoutData.length))
				RemoveLockoutData(currentLockoutData);
		}

		private bool isRecentered; //Is the recenter complete?
		/// <summary> Recenters the player. Only call this AFTER movement has occurred. </summary>
		private void UpdateRecenter()
		{
			if (!IsLockoutActive || !currentLockoutData.recenterPlayer) return;

			Vector3 recenterDirection = PathFollower.Forward().Rotated(UpDirection, Mathf.Pi * .5f);

			float currentOffset = PathFollower.GetLocalPosition(GlobalPosition).x;
			float movementOffset = currentOffset;
			if (!isRecentered) //Smooth out recenter speed
			{
				movementOffset = Mathf.MoveToward(movementOffset, 0, MoveSpeed * PhysicsManager.physicsDelta);
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
		public void StartExternal(Node3D followObject = null, float smoothing = 0f)
		{
			ResetMovementState();
			MovementState = MovementStates.External;
			ActionState = ActionStates.Normal;

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

		[Export]
		public MovementResource groundSettings;
		[Export]
		public MovementResource airSettings;
		[Export]
		public MovementResource backstepSettings;
		/// <summary> Is the player moving backwards? </summary>
		public bool IsMovingBackward { get; private set; }
		[Export]
		public Curve turningSpeedCurve; //Curve of how speed is lost when turning
		private float turningVelocity;
		private bool isIdling;

		private const float TURN_SPEED = .1f; //How much to turn when moving slowly
		private const float TURN_SPEED_LOSS = .06f; //How much speed to lose when turning sharply
		private const float MAX_TURN_SPEED = .2f; //How much to turn when moving at top speed
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_TURNAROUND_ANGLE = Mathf.Pi * .6f;
		/// <summary> Updates MoveSpd. What else do you need know? </summary>
		private void UpdateMoveSpd()
		{
			isIdling = Mathf.IsZeroApprox(MoveSpeed);
			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Backflip) return;
			if (Skills.IsSpeedBreakActive) return; //Overridden to max speed

			float inputAngle = GetTargetInputAngle();
			float dot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, GetTargetMovementAngle()));
			float inputLength = GetMovementDirection().Length(); //Limits top speed; Modified depending on the LockoutResource.directionOverrideMode
			if (dot < .8f)
				inputLength *= dot;

			MovementResource activeMovementResource = GetActiveMovementSettings();
			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);

			if (IsLockoutActive && currentLockoutData.overrideSpeed)
			{
				//Override speed to the correct value
				float targetSpd = activeMovementResource.speed * currentLockoutData.speedRatio;
				if (Mathf.IsZeroApprox(currentLockoutData.tractionMultiplier)) //Snap speed (i.e. Dash Panels)
				{
					MoveSpeed = targetSpd;
					return;
				}

				float delta = PhysicsManager.physicsDelta;
				if (MoveSpeed <= targetSpd) //Accelerate using traction
					delta *= activeMovementResource.traction * currentLockoutData.tractionMultiplier;
				else //Slow down with friction
					delta *= activeMovementResource.friction * currentLockoutData.frictionMultiplier;
				MoveSpeed = Mathf.MoveToward(MoveSpeed, targetSpd, delta);
				return;
			}

			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero) || dot <= 0.1f) //Basic slow down
				MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, 0);
			else
			{
				bool isTurningAround = deltaAngle > MAX_TURNAROUND_ANGLE;
				if (isTurningAround) //Skid to a stop
					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, -1);
				else
					MoveSpeed = activeMovementResource.Interpolate(MoveSpeed, inputLength); //Accelerate based on input strength
			}

			IsMovingBackward = MoveSpeed > 0 && ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.ForwardAngle) > MAX_TURNAROUND_ANGLE; //Moving backwards, limit speed
		}

		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			float speedRatio = IsOnGround ? groundSettings.GetSpeedRatio(MoveSpeed) : airSettings.GetSpeedRatio(MoveSpeed);
			float targetMovementAngle = GetTargetMovementAngle();
			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			float turnDelta = Mathf.Lerp(TURN_SPEED, MAX_TURN_SPEED, speedRatio);

			if (IsLockoutActive)
			{
				if (currentLockoutData.directionSpaceMode == LockoutResource.DirectionSpaceMode.PathFollower)
					MovementAngle += PathFollower.CalculateDeltaAngle(); //Follow pathfollower around turns better
			}

			if (ActionState == ActionStates.Backflip) return;
			if (!isIdling && deltaAngle > MAX_TURNAROUND_ANGLE) return; //Turning around

			if (isIdling) //Instantly set movement angle to target movement angle
			{
				turningVelocity = 0;
				MovementAngle = targetMovementAngle;
			}
			if (!IsLockoutActive || !currentLockoutData.overrideSpeed) //Don't apply turning speed loss when overriding speed
			{
				//Calculate turn delta, relative to ground speed
				float speedLossRatio = deltaAngle / MAX_TURNAROUND_ANGLE;
				MoveSpeed -= MoveSpeed * turningSpeedCurve.Sample(speedLossRatio) * TURN_SPEED_LOSS;
				if (MoveSpeed < 0)
					MoveSpeed = 0;
			}

			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, turnDelta);
		}

		private MovementResource GetActiveMovementSettings()
		{
			if (!IsOnGround)
				return airSettings;
			return IsMovingBackward ? backstepSettings : groundSettings;
		}

		private float slopeInfluence; //Current influence of the slope, from 0 <-> 1
		private const float SLOPE_DEADZONE = .9f; //Ignore slopes when dot product is greater than this value
		private void UpdateSlopeInfluence(Vector3 groundNormal)
		{
			//Calculate slope influence
			float dot = groundNormal.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > SLOPE_DEADZONE) //Slope is too insignificant to affect movement
			{
				slopeInfluence = 0; //Reset influence
				return;
			}

			float rotationAmount = PathFollower.Back().SignedAngleTo(Vector3.Forward, Vector3.Up);
			Vector3 slopeDirection = groundNormal.Rotated(Vector3.Up, rotationAmount).Normalized();
			slopeInfluence = slopeDirection.z;
		}

		private void UpdateSlopeSpd()
		{
			if (Mathf.Abs(slopeInfluence) < SLOPE_DEADZONE || !IsOnGround) return; //Slope is too shallow or not on the ground
			if (IsLockoutActive && currentLockoutData.ignoreSlopes) return; //Lockout is ignoring slopes

			/*
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
			*/
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
				if (currentLockoutData.resetOnLand && JustLandedOnGround) //Cancel lockout
					RemoveLockoutData(currentLockoutData);
				else if (currentLockoutData.disableActions)
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

				if (IsHoldingBackward)
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
			if (IsHoldingForward && MoveSpeed < landingBoost)
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
			canLandingBoost = Skills.IsLandingDashEnabled;
			ActionState = ActionStates.Jumping;
			VerticalSpd = RuntimeConstants.GetJumpPower(jumpHeight);
			Sound.PlayActionSFX("jump");
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpTime >= ACCELERATION_JUMP_LENGTH) //Acceleration jump?
			{
				if (!IsHoldingBackward && Controller.MovementAxis.Length() > .5f)
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
			if (ActionState == ActionStates.Backflip) //Backflipping - Jumpdash directly forward
				MovementAngle = PathFollower.ForwardAngle;
			else //Force MovementAngle to face forward
				MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .5f);

			MoveSpeed = jumpDashSpeed;
			IsAttacking = true;
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
		private void UpdateStomp() => VerticalSpd = Mathf.MoveToward(VerticalSpd, STOMP_SPEED, STOMP_GRAVITY * PhysicsManager.physicsDelta);
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
			if (!IsHoldingForward) //Influence backflip direction slightly
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

		public void TakeDamage(Node3D node)
		{
			EmitSignal(SignalName.Damaged, node);

			if (IsInvincible) return; //Don't take damage.

			ActionState = ActionStates.Damaged;
			invincibliltyTimer = INVINCIBILITY_LENGTH;

			if (MovementState == MovementStates.Normal)
				Knockback(true);
		}

		public void Knockback(bool damaged) //Knocks the player backwards
		{
			IsOnGround = false;
			MoveSpeed = -4f;
			VerticalSpd = 4f;

			//TODO Play hurt animation
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

			GlobalPosition = Stage.Checkpoint.GlobalPosition;
			PathFollower.SetActivePath(Stage.CheckpointPath); //Revert path
			Stage.RespawnObjects(true);

			ResetOrientation();
			Camera.ResetFlag = true;
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
					if (!IsOnGround)
					{
						MoveSpeed = launchData.HorizontalVelocity * .5f; //Prevent too much movement
						VerticalSpd = launchData.FinalVerticalVelocity;
					}
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

		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask;

		/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs </summary>
		public float MovementAngle { get; set; }
		private float GetTargetInputAngle()
		{
			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero)) //Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(Controller.MovementAxis.AngleTo(Vector2.Up)); //Target rotation angle (in radians)
		}
		private float GetTargetMovementAngle()
		{
			float inputAngle = GetTargetInputAngle();

			if (IsLockoutActive && currentLockoutData.directionOverrideMode != LockoutResource.DirectionOverrideMode.Free)
			{
				float targetAngle = Mathf.DegToRad(currentLockoutData.overrideAngle);
				if (currentLockoutData.directionSpaceMode == LockoutResource.DirectionSpaceMode.Camera)
					targetAngle = Camera.TransformAngle(targetAngle);
				else if (currentLockoutData.directionSpaceMode == LockoutResource.DirectionSpaceMode.PathFollower)
					targetAngle = PathFollower.ForwardAngle + targetAngle;

				if (currentLockoutData.allowReversing) //Check if we're trying to turn around
				{
					float dot = Mathf.Sign(ExtensionMethods.DotAngle(inputAngle, PathFollower.ForwardAngle));
					if (dot < 0) //Invert targetAngle when moving backwards
						targetAngle += Mathf.Pi;
				}

				if (currentLockoutData.directionOverrideMode == LockoutResource.DirectionOverrideMode.Clamp)
				{
					if (!currentLockoutData.overrideSpeed || !Controller.MovementAxis.IsEqualApprox(Vector2.Zero)) //Allows recentering when holding neutral on the control stick
						targetAngle = ExtensionMethods.ClampAngleRange(inputAngle, targetAngle, Mathf.Pi * currentLockoutData.overrideAngleClampRange * .5f);
				}

				return targetAngle;
			}

			return inputAngle;
		}

		public Vector3 GetMovementDirection() => this.Forward().Rotated(UpDirection, MovementAngle);

		public float MoveSpeed { get; set; } //Character's primary movement speed.
		public float VerticalSpd { get; set; } //Used for jumping and falling

		private void ResetVelocity() //Resets all speed values to zero
		{
			MoveSpeed = VerticalSpd = 0;
		}

		private bool useCustomPhysics; //TRUE whenever external objects are overridding physics
		private void UpdatePhysics()
		{
			Lockon.ProcessLockonTargets();
			if (useCustomPhysics) return; //When physics are handled in the state machine

			Vector3 movementDirection = GetMovementDirection();

			//Collision checks
			CheckGround();
			CheckMainWall(movementDirection);

			if (ActionState == ActionStates.JumpDash) //Jump dash ignores slopes
				movementDirection = movementDirection.RemoveVertical().Normalized();

			Velocity = movementDirection * MoveSpeed;
			Velocity += UpDirection * VerticalSpd;

			MoveAndSlide();
			CheckCeiling();

			PathFollower.Resync(); //Resync
			UpdateRecenter();
		}

		public bool IsOnGround { get; private set; }
		public bool JustLandedOnGround { get; private set; } //Flag for doing stuff on land

		public Vector3 CenterPosition
		{
			get => GlobalPosition + UpDirection * COLLISION_RADIUS; //Center of collision calculations
			set => GlobalPosition = value - UpDirection * COLLISION_RADIUS;
		}
		private const float COLLISION_RADIUS = .3f;
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS + COLLISION_PADDING;
			if (IsOnGround)
				castLength += MoveSpeed * PhysicsManager.physicsDelta; //Atttempt to remain stuck to the ground
			else if (VerticalSpd < 0)
				castLength += Mathf.Abs(VerticalSpd) * PhysicsManager.physicsDelta;
			else if (VerticalSpd > 0)
				castLength = -.1f; //Reduce snapping when moving upwards

			Vector3 castVector = -UpDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!ValidateGroundCast(ref groundHit))
			{
				//Whisker casts (For slanted walls and ground)
				float interval = Mathf.Tau / 4f;
				Vector3 castOffset = this.Forward() * (COLLISION_RADIUS - COLLISION_PADDING);
				for (int i = 0; i < 4; i++)
				{
					castOffset = castOffset.Rotated(UpDirection, interval).Normalized() * COLLISION_RADIUS * .5f;
					groundHit = this.CastRay(castOrigin + castOffset, castVector, environmentMask);
					Debug.DrawRay(castOrigin + castOffset, castVector, groundHit ? Colors.Red : Colors.White);
					if (ValidateGroundCast(ref groundHit)) break; //Found the floor
				}
			}

			if (groundHit) //Successful ground hit
			{
				Sound.UpdateGroundType(groundHit.collidedObject);
				GlobalPosition -= UpDirection * (groundHit.distance - COLLISION_RADIUS); //Snap to ground

				if (!IsOnGround) //Landing on the ground
				{
					IsOnGround = true;
					VerticalSpd = 0;

					isJumpClamped = false;
					IsAttacking = false;
					CanJumpDash = false;
					isAccelerationJump = false;
					JustLandedOnGround = true;

					Sound.PlayLandingSFX();
					CheckLandingBoost(); //Landing boost skill

					ResetActionState();
					Lockon.ResetLockonTarget();
					UpDirection = groundHit.normal;
				}
				else //Update world direction
					UpDirection = UpDirection.Lerp(groundHit.normal, .2f).Normalized();

				UpdateSlopeInfluence(groundHit.normal);
			}
			else
			{
				IsOnGround = false;
				//Smooth world direction based on vertical speed
				UpDirection = UpDirection.Lerp(Vector3.Up, Mathf.Clamp((VerticalSpd / RuntimeConstants.MAX_GRAVITY) - .15f, 0f, 1f)).Normalized();
			}
		}

		private bool ValidateGroundCast(ref RaycastHit hit) //Don't count walls as the ground
		{
			if (hit)
			{
				//Unless the collider is supposed to be both
				if (hit.collidedObject.IsInGroup("wall") && !hit.collidedObject.IsInGroup("floor"))
					hit = new RaycastHit();

				if (hit.normal.AngleTo(UpDirection) > Mathf.Pi * .6f) //Don't allow registering collisions with the ceiling
					hit = new RaycastHit();
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

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
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

			RaycastHit wallHit = this.CastRay(CenterPosition, castVector * castLength, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
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

						MoveAndCollide(castVector * MoveSpeed * PhysicsManager.physicsDelta);
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

			if (Stage.Checkpoint == null) //Default to parent node's position
				Transform = Transform3D.Identity;
			else
				GlobalTransform = Stage.Checkpoint.GlobalTransform;

			PathFollower.Resync(); //Update path follower
			MovementAngle = PathFollower.ForwardAngle; //Reset movement angle

			//TODO Re-sync visual rotations
		}

		private void UpdateOrientation() //Orientates Root to world direction, then rotates the gimbal on the y-axis
		{
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
		private bool isCountdownActive;
		public void CountdownStarted() => isCountdownActive = true;
		public void CountdownCompleted() => isCountdownActive = false;

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
				RaycastHit hit = this.CastRay(CenterPosition, UpDirection * checkLength, environmentMask, false);
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
