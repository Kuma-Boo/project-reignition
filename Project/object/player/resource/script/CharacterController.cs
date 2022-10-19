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

			PathFollower = GetNode<CharacterPathFollower>(pathFollower);
			Animator = GetNode<CharacterAnimator>(animator);
			Sound = GetNode<CharacterSound>(sound);
			Skills = GetNode<CharacterSkillManager>(skills);
			Lockon = GetNode<CharacterLockon>(lockon);

			_environmentCollider = GetNode<CollisionShape3D>(environmentCollider);

			ResetOrientation(); //Start with proper orientation

			if (Stage != null)
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
			CancelMovementState(MovementState);
			Skills.IsSpeedBreakEnabled = Skills.IsTimeBreakEnabled = true; //Reenable soul skills
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
				case MovementStates.Sidle:
					UpdateSidle();
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
		/// <summary> Are we in a 2D section? </summary>
		public bool isSideScroller;
		/// <summary> Determines which way is "Forward" during sidescrolling segments. </summary>
		public bool isFacingRight;

		/// <summary> Is the player holding forward, relative to the PathFollower's forward angle? </summary>
		private bool IsHoldingForward
		{
			get
			{
				if (isSideScroller)
					return (isFacingRight && Controller.MovementAxis.x > 0) || (!isFacingRight && Controller.MovementAxis.x < 0);

				float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle(), PathFollower.ForwardAngle);
				return !Controller.MovementAxis.IsEqualApprox(Vector2.Zero) && delta < Mathf.Pi * .4f;
			}
		}
		/// <summary> Is the player holding backward, relative to the PathFollower's forward angle? </summary>
		private bool IsHoldingBackward
		{
			get
			{
				if (isSideScroller)
					return (isFacingRight && Controller.MovementAxis.x < 0) || (!isFacingRight && Controller.MovementAxis.x > 0);

				float delta = ExtensionMethods.DeltaAngleRad(GetTargetInputAngle() + Mathf.Pi, PathFollower.ForwardAngle);
				return !Controller.MovementAxis.IsEqualApprox(Vector2.Zero) && delta < Mathf.Pi * .4f;
			}
		}
		/// <summary> Angle (in radians) of Controller.MovementAxis, relative to Vector2.Down. </summary>
		private float InputAngle => Controller.MovementAxis.AngleTo(Vector2.Up);

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

			actionBufferTimer = Mathf.MoveToward(actionBufferTimer, 0, PhysicsManager.physicsDelta);
			jumpBufferTimer = Mathf.MoveToward(jumpBufferTimer, 0, PhysicsManager.physicsDelta);

			if (Controller.actionButton.wasPressed)
				actionBufferTimer = ACTION_BUFFER_LENGTH;

			if (Controller.jumpButton.wasPressed)
				jumpBufferTimer = JUMP_BUFFER_LENGTH;
		}

		#region Control Lockouts
		private bool isRecentered;
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
			if (!IsLockoutActive || Mathf.IsZeroApprox(lockoutTimer))
				return;

			lockoutTimer = Mathf.MoveToward(lockoutTimer, currentLockoutData.length, PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(lockoutTimer, currentLockoutData.length))
				RemoveLockoutData(currentLockoutData);
		}
		#endregion

		#region External Control, Automation and Events
		private Vector3 externalOffset;
		private Node3D externalParent;

		[Signal]
		public delegate void ExternalControlFinishedEventHandler();
		public void StartExternal(Node3D followObject = null, bool snap = false)
		{
			ResetMovementState();
			MovementState = MovementStates.External;
			ActionState = ActionStates.Normal;

			externalParent = followObject;
			externalOffset = Vector3.Zero; //Reset offset
			if (externalParent != null && !snap) //Smooth out transition
				externalOffset = GlobalPosition - externalParent.GlobalPosition;

			ResetVelocity();
			UpdateExternalControl();
		}

		public void UpdateExternalControl()
		{
			useCustomPhysics = true;
			externalOffset = externalOffset.Lerp(Vector3.Zero, .2f); //Smooth out entry

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
		private bool isMovingBackward;
		[Export]
		public Curve turningSpeedCurve; //Curve of how speed is lost when turning
		private float turningVelocity;

		private const float TURN_SPEED = .1f; //How much to turn when moving slowly
		private const float TURN_SPEED_LOSS = .06f; //How much speed to lose when turning sharply
		private const float MAX_TURN_SPEED = .2f; //How much to turn when moving at top speed
		/// <summary> Maximum angle as to what registers as turning, before transitioning into turnaround</summary>
		private const float MAX_TURN_ANGLE = Mathf.Pi * .8f;
		/// <summary> Maximum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
		private const float MAX_BACKSTEP_ANGLE = Mathf.Pi * .6f;
		private bool isIdling;
		/// <summary> Updates MoveSpd. What else do you need know? </summary>
		private void UpdateMoveSpd()
		{
			isIdling = Mathf.IsZeroApprox(MoveSpd);
			if (ActionState == ActionStates.Crouching || ActionState == ActionStates.Backflip) return;
			if (Skills.IsSpeedBreakActive) return; //Overridden to max speed

			float inputAngle = GetTargetInputAngle();
			float dot = Mathf.Abs(ExtensionMethods.DotAngle(inputAngle, GetTargetForwardAngle()));
			float inputLength = GetMovementDirection().Length(); //Limits top speed; Modified depending on the LockoutResource.directionOverrideMode
			if (dot < .8f)
				inputLength *= dot;

			MovementResource activeMovementResource = GetActiveMovementSettings();
			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, inputAngle);

			if (IsLockoutActive && currentLockoutData.overrideSpeed)
			{
				//Override speed to the correct value
				float targetSpd = activeMovementResource.speed * currentLockoutData.speedRatio;
				if (Mathf.IsZeroApprox(currentLockoutData.tractionMultiplier)) //Negative traction, snap speed
				{
					MoveSpd = targetSpd;
					return;
				}

				float delta = PhysicsManager.physicsDelta;
				if (MoveSpd <= targetSpd) //Accelerate using traction
					delta *= activeMovementResource.traction * currentLockoutData.tractionMultiplier;
				else //Slow down with friction
					delta *= activeMovementResource.friction * currentLockoutData.frictionMultiplier;
				MoveSpd = Mathf.MoveToward(MoveSpd, targetSpd, delta);
				return;
			}

			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero) || dot <= 0.1f) //Basic slow down
				MoveSpd = activeMovementResource.Interpolate(MoveSpd, 0);
			else
			{
				bool isTurningAround = deltaAngle > MAX_TURN_ANGLE;
				if (isTurningAround) //Skid to a stop
					MoveSpd = activeMovementResource.Interpolate(MoveSpd, -1);
				else
					MoveSpd = activeMovementResource.Interpolate(MoveSpd, inputLength); //Accelerate based on input strength
			}

			isMovingBackward = MoveSpd > 0 && ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.ForwardAngle) > MAX_BACKSTEP_ANGLE; //Moving backwards, limit speed
		}

		/// <summary> Updates Turning. Read the function names. </summary>
		private void UpdateTurning()
		{
			float speedRatio = groundSettings.GetSpeedRatio(MoveSpd);
			float targetMovementAngle = GetTargetMovementAngle();
			float deltaAngle = ExtensionMethods.DeltaAngleRad(MovementAngle, targetMovementAngle);
			float turnDelta = Mathf.Lerp(TURN_SPEED, MAX_TURN_SPEED, speedRatio);

			if (!isIdling && deltaAngle > MAX_TURN_ANGLE) return; //Turning around

			if (isIdling)
			{
				turningVelocity = 0;
				if (IsOnGround)//Instantly set movement angle to target movement angle
					MovementAngle = targetMovementAngle;
				else //Ensure character turns facing forward, for more consistant homing attacks.
					MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .49f);
			}
			if (!IsLockoutActive || !currentLockoutData.overrideSpeed) //Don't apply turning speed loss when overriding speed
			{
				//Calculate turn delta, relative to ground speed
				float speedLossRatio = deltaAngle / MAX_TURN_ANGLE;
				MoveSpd -= MoveSpd * turningSpeedCurve.Sample(speedLossRatio) * TURN_SPEED_LOSS;
				if (MoveSpd < 0)
					MoveSpd = 0;
			}

			if (IsLockoutActive)
			{
				if (currentLockoutData.recenterPlayer)
				{
					//TODO Fix recentering player
					GD.Print("Recentering player is broken!");

					Vector3 recenterDirection = GroundDirection.Cross(GetMovementDirection());
					float offset = PathFollower.LocalPlayerPosition.x;
					if (!isRecentered) //Smooth out recenter speed
					{
						offset = Mathf.MoveToward(offset, 0, MoveSpd * PhysicsManager.physicsDelta);
						if (Mathf.IsZeroApprox(offset))
							isRecentered = true;
						offset = PathFollower.LocalPlayerPosition.x - offset;
					}

					GlobalTranslate(offset * recenterDirection);
				}

				if (currentLockoutData.directionOverrideMode != LockoutResource.DirectionOverrideMode.Free &&
			currentLockoutData.directionSpaceMode == LockoutResource.DirectionSpaceMode.PathFollower)
				{
					turnDelta = TURN_SPEED; //More responsive turning
					MovementAngle += PathFollower.CalculateDeltaAngle(); //Follow pathfollower around turns better
				}
			}

			MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, turnDelta);
		}

		private MovementResource GetActiveMovementSettings()
		{
			if (!IsOnGround)
				return airSettings;
			return isMovingBackward ? backstepSettings : groundSettings;
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

			float rotationAmount = PathFollower.Forward().SignedAngleTo(Vector3.Forward, Vector3.Up);
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
			if (IsHoldingForward && MoveSpd < landingBoost)
				MoveSpd = landingBoost;
		}

		#region Jump
		[Export]
		public float jumpHeight;
		[Export]
		public float jumpCurve = .95f;
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
			canLandingBoost = Skills.IsLandingDashEnabled;
			ActionState = ActionStates.Jumping;

			VerticalSpd = RuntimeConstants.GetJumpPower(jumpHeight);
		}

		private void UpdateJump()
		{
			if (isAccelerationJump && currentJumpLength >= ACCELERATION_JUMP_LENGTH)
			{
				//Acceleration jump?
				if (!IsHoldingBackward && Controller.MovementAxis.Length() > .5f)
				{
					ActionState = ActionStates.AccelJump;
					MoveSpd = Skills.accelerationJumpSpeed;
					MovementAngle = ExtensionMethods.ClampAngleRange(GetTargetInputAngle(), PathFollower.ForwardAngle, Mathf.Pi * .5f);
				}

				VerticalSpd = 5f; //Consistant accel jump height
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
			else if (VerticalSpd > 0f)
				VerticalSpd *= jumpCurve; //Kill jump height

			currentJumpLength += PhysicsManager.physicsDelta;
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
			IsAttacking = true;
			CanJumpDash = false; //Don't use get/set so we keep our target monitoring.
			ActionState = ActionStates.JumpDash;

			float delta = ExtensionMethods.DeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
			if (delta > MAX_BACKSTEP_ANGLE) //Backstepping; Jumpdash directly forward
				MovementAngle = PathFollower.ForwardAngle;
			else //Force MovementAngle to face forward
				MovementAngle = ExtensionMethods.ClampAngleRange(MovementAngle, PathFollower.ForwardAngle, Mathf.Pi * .5f);

			MoveSpd = jumpDashSpeed;

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
			MoveSpd = Mathf.MoveToward(MoveSpd, 0, SLIDE_FRICTION * PhysicsManager.physicsDelta);

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
		private float backflipTimer;
		/// <summary> How long does a backflip last before the player can act again? </summary>
		private const float BACKFLIP_LENGTH = .4f;
		/// <summary> How much can the player adjust their angle while backflipping? </summary>
		private const float MAX_BACKFLIP_ADJUSTMENT = Mathf.Pi * .25f;
		/// <summary> How much to turn when backflipping </summary>
		private const float BACKFLIP_TURN_SPEED = .25f;
		private void UpdateBackflip()
		{
			backflipTimer += PhysicsManager.physicsDelta;
			if (backflipTimer > BACKFLIP_LENGTH)
				ResetActionState();

			if (!IsHoldingForward) //Influence backflip direction slightly
			{
				float targetMovementAngle = ExtensionMethods.ClampAngleRange(GetTargetInputAngle(), PathFollower.BackAngle, MAX_BACKFLIP_ADJUSTMENT);
				MovementAngle = ExtensionMethods.SmoothDampAngle(MovementAngle, targetMovementAngle, ref turningVelocity, BACKFLIP_TURN_SPEED);
			}

			MoveSpd = backflipSpeed;
		}

		private void StartBackflip()
		{
			CanJumpDash = true;
			backflipTimer = 0;
			MoveSpd = backflipSpeed;
			MovementAngle = GetTargetInputAngle();
			VerticalSpd = RuntimeConstants.GetJumpPower(backflipHeight);

			IsOnGround = false;
			ActionState = ActionStates.Backflip;
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
			{
				IsOnGround = false;
				MoveSpd = -4f;
				VerticalSpd = 4f;
			}
			else if (MovementState == MovementStates.Sidle) //Start falling
			{
				sidleTimer = SIDLE_DAMAGE_LENGTH;
				MoveSpd = VerticalSpd = 0;
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

			Stage.RespawnObjects();

			ResetOrientation();
			Camera.ResetFlag = true;
		}
		#endregion

		#region Sidle
		[Export]
		public MovementResource sidleSettings;
		public Node3D CurrentRailing { get; set; }
		private float sidleTimer;
		private readonly float SIDLE_DAMAGE_LENGTH = .5f;
		private readonly float SIDLE_RAIL_FALL_SPEED = 4f;
		private readonly float SIDLE_HORIZONTAL_SPEED = .25f;

		public void StartSidle()
		{
			Skills.IsSpeedBreakEnabled = false;

			currentLockoutData.disableActions = true;
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

			MoveSpd = sidleSettings.Interpolate(MoveSpd, isFacingRight ? Controller.MovementAxis.x : -Controller.MovementAxis.x);
		}

		private void UpdateSidleDamage()
		{
			if (sidleTimer == 0)
			{
				PathFollower.HOffset = Mathf.Lerp(PathFollower.HOffset, -1, SIDLE_HORIZONTAL_SPEED);

				if (CurrentRailing != null)
				{
					float targetY = CurrentRailing.GlobalPosition.y;
					GlobalPosition = new Vector3(GlobalPosition.x, Mathf.MoveToward(GlobalPosition.y, targetY, SIDLE_RAIL_FALL_SPEED * PhysicsManager.physicsDelta), GlobalPosition.z); //Snap to railing

					if (Mathf.IsEqualApprox(GlobalPosition.y, targetY))
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
				if (VerticalSpd > 0)
					PathFollower.HOffset = Mathf.Lerp(PathFollower.HOffset, 0, SIDLE_HORIZONTAL_SPEED);
				else if (IsOnGround)
					PathFollower.HOffset = 0;

				ApplyGravity();
			}
			else if (jumpBufferTimer != 0)
			{
				//Sidle Recovery
				jumpBufferTimer = 0;
				VerticalSpd = 8;
				sidleTimer = 0;
			}
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

			if (!useAutoAlignment) return;

			Vector3 direction = launchData.launchDirection.RemoveVertical().Normalized();
			if (!direction.IsNormalized()) //Direction parallel with Vector3.Up! Use launcher's forward direction instead.
			{
				if (newLauncher == null) return;
				direction = newLauncher.Forward().RemoveVertical().Normalized();
			}
		}

		private void UpdateLauncher()
		{
			useCustomPhysics = true;
			if (activeLauncher != null && !activeLauncher.IsCharacterCentered)
				GlobalPosition = activeLauncher.RecenterCharacter();
			else
			{
				Vector3 targetPosition = launchData.InterpolatePosition(launcherTime);
				float heightDelta = targetPosition.y - GlobalPosition.y;
				GlobalPosition = targetPosition;

				if (heightDelta < 0) //Only check ground when falling
					CheckGround();

				if (IsOnGround || launchData.IsLauncherFinished(launcherTime)) //Revert to normal state
				{
					FinishLauncher();
					if (!IsOnGround)
					{
						MoveSpd = launchData.InitialHorizontalVelocity;
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
		public NodePath environmentCollider;
		private CollisionShape3D _environmentCollider;
		public bool IsEnvironmentColliderEnabled
		{
			get => !_environmentCollider.Disabled;
			set => _environmentCollider.Disabled = !value;
		}

		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask;

		/// <summary> Global "up" direction of the player. Same as groundHit.normal when on the ground </summary>
		public Vector3 GroundDirection { get; private set; }
		/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs </summary>
		public float MovementAngle { get; private set; }
		private float GetTargetInputAngle()
		{
			if (Controller.MovementAxis.IsEqualApprox(Vector2.Zero)) //Invalid input, no change
				return MovementAngle;

			return Camera.TransformAngle(InputAngle); //Target rotation angle (in radians)
		}
		private float GetTargetMovementAngle()
		{
			float targetAngle = GetTargetForwardAngle();
			if (IsLockoutActive && currentLockoutData.directionOverrideMode == LockoutResource.DirectionOverrideMode.Clamp)
			{
				if (!currentLockoutData.overrideSpeed || !Controller.MovementAxis.IsEqualApprox(Vector2.Zero)) //Allows recentering when holding neutral on the control stick
					targetAngle = ExtensionMethods.ClampAngleRange(GetTargetInputAngle(), targetAngle, Mathf.Pi * currentLockoutData.overrideAngleClampRange * .5f);
			}

			return targetAngle;
		}
		private float GetTargetForwardAngle()
		{
			float inputAngle = GetTargetInputAngle();
			float targetAngle = MovementAngle;

			if (IsLockoutActive && currentLockoutData.directionOverrideMode != LockoutResource.DirectionOverrideMode.Free)
			{
				targetAngle = Mathf.DegToRad(currentLockoutData.overrideAngle);
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

				return targetAngle;
			}

			//Default behaviour; Equivalent to GetTargetInputAngle()
			return inputAngle;
		}

		private Vector3 GetMovementDirection() => this.Forward().Rotated(GroundDirection, MovementAngle);

		public float MoveSpd { get; set; } //Character's primary movement speed.
										   //public float StrafeSpd { get; set; }
		public float VerticalSpd { get; set; } //Used for jumping and falling

		private void ResetVelocity() //Resets all speed values to zero
		{
			MoveSpd = VerticalSpd = 0;
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

			if (!IsOnGround && ActionState == ActionStates.JumpDash) //Jump dash ignores slopes
				movementDirection = movementDirection.RemoveVertical().Normalized();

			Velocity = movementDirection * MoveSpd;
			//Velocity += PathFollower.StrafeDirection * StrafeSpd;
			Velocity += GroundDirection * VerticalSpd;
			MoveAndSlide();
			CheckCeiling();

			PathFollower.Resync(); //Resync
		}

		public bool IsOnGround { get; private set; }
		public bool JustLandedOnGround { get; private set; } //Flag for doing stuff on land

		public Vector3 CenterPosition => GlobalPosition + GroundDirection * COLLISION_RADIUS; //Center of collision calculations
		private const float COLLISION_RADIUS = .3f;
		private void CheckGround()
		{
			if (JustLandedOnGround) //RESET FLAG
				JustLandedOnGround = false;

			Vector3 castOrigin = CenterPosition;
			float castLength = COLLISION_RADIUS;
			if (IsOnGround)
			{
				castLength += .5f; //For slopes that go downwards

				if (Skills.IsSpeedBreakActive) //Moving faster, more snapping needed
					castLength += .5f;
			}
			else if (VerticalSpd < 0)
				castLength += Mathf.Abs(VerticalSpd) * PhysicsManager.physicsDelta;
			else if (VerticalSpd > 0)
				castLength = -.1f; //Reduce snapping when moving upwards

			Vector3 castVector = -GroundDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (!ValidateGroundCast(ref groundHit))
			{
				//Whisker casts (For slanted walls and ground)
				float interval = Mathf.Tau / 4f;
				Vector3 castOffset = this.Forward();
				for (int i = 0; i < 4; i++)
				{
					castOffset = castOffset.Rotated(GroundDirection, interval).Normalized() * COLLISION_RADIUS * .5f;
					groundHit = this.CastRay(castOrigin + castOffset, castVector, environmentMask);
					Debug.DrawRay(castOrigin + castOffset, castVector, groundHit ? Colors.Red : Colors.White);
					if (ValidateGroundCast(ref groundHit)) break; //Found the floor
				}
			}

			if (groundHit) //Successful ground hit
			{
				GlobalPosition -= GroundDirection * (groundHit.distance - COLLISION_RADIUS); //Snap to ground

				if (!IsOnGround) //Landing on the ground
				{
					IsOnGround = true;
					VerticalSpd = 0;

					isJumpClamped = false;
					IsAttacking = false;
					CanJumpDash = false;
					isAccelerationJump = false;
					JustLandedOnGround = true;

					CheckLandingBoost(); //Landing boost skill

					ResetActionState();
					Lockon.ResetLockonTarget();
					GroundDirection = groundHit.normal;
				}
				else //Update world direction
					GroundDirection = GroundDirection.Lerp(groundHit.normal, .2f).Normalized();

				UpdateSlopeInfluence(groundHit.normal);
			}
			else
			{
				IsOnGround = false;

				//Smooth world direction based on vertical speed
				GroundDirection = GroundDirection.Lerp(Vector3.Up, Mathf.Clamp((VerticalSpd / RuntimeConstants.MAX_GRAVITY) - .15f, 0f, 1f)).Normalized();
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

			Vector3 castVector = GroundDirection * castLength;
			if (VerticalSpd > 0)
				castVector.y += VerticalSpd * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GlobalTranslate(ceilingHit.point - (CenterPosition + GroundDirection * COLLISION_RADIUS));

				if (VerticalSpd > 0)
					VerticalSpd = 0;
			}
		}

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (Mathf.IsZeroApprox(MoveSpd)) return; //No movement

			castVector *= Mathf.Sign(MoveSpd);
			float castLength = COLLISION_RADIUS + COLLISION_PADDING + Mathf.Abs(MoveSpd) * PhysicsManager.physicsDelta;

			RaycastHit centerHit = this.CastRay(CenterPosition, castVector * castLength, environmentMask, false, (Godot.Collections.Array)GetCollisionExceptions());
			Debug.DrawRay(CenterPosition, castVector * castLength, centerHit ? Colors.Red : Colors.White);
			if (!IsValidWallCast(centerHit))
				centerHit = new RaycastHit();

			if (centerHit && ActionState != ActionStates.JumpDash)
			{
				float wallRatio = Mathf.Abs(centerHit.normal.Dot(castVector));

				if (wallRatio > .9f) //Running into wall head-on
				{
					if (Skills.IsSpeedBreakActive) //Cancel speed break
						Skills.ToggleSpeedBreak();

					MoveSpd = 0f; //Kill speed
					GlobalTranslate(castVector * (centerHit.distance - COLLISION_RADIUS));
				}
				else //Reduce MoveSpd when moving against walls
				{
					float speedClamp = Mathf.Clamp(1.2f - wallRatio * .4f, 0f, 1f); //Arbitrary formula that works well
					MoveSpd *= speedClamp;
				}
			}
		}

		private bool IsValidWallCast(RaycastHit hit) => hit && !hit.collidedObject.IsInGroup("floor");
		private const float COLLISION_PADDING = .1f;

		private const float ORIENTATION_SMOOTHING = .4f;
		private void ResetOrientation() //Resets orientation
		{
			GroundDirection = Vector3.Up;
			//MovementAngle = GetParent<Node3D>().Rotation.y; //Copy movement angle from global parent

			//TODO Re-sync visual rotations
		}

		private void UpdateOrientation() //Orientates Root to world direction, then rotates the gimbal on the y-axis
		{
			Transform3D t = GlobalTransform;
			t.basis.z = Vector3.Back;
			t.basis.y = GroundDirection;
			if (Mathf.Abs(t.basis.z.Dot(t.basis.y)) > .9f) //BUGFIX Prevent player blipping out of existence when on 90 wall
				t.basis.z = Vector3.Up;
			t.basis.x = -t.basis.z.Cross(t.basis.y);
			t.basis = t.basis.Orthonormalized();
			GlobalTransform = t;
		}
		#endregion

		#region Signals
		private bool isCountdownActive;
		public void CountdownStarted()
		{
			isCountdownActive = true;
		}
		public void CountdownCompleted() => isCountdownActive = false;

		private void OnStageCompleted(bool _)
		{
			//Disable everything
			Lockon.IsMonitoring = false;
			Skills.IsTimeBreakEnabled = false;
			Skills.IsSpeedBreakEnabled = false;
		}

		public void OnObjectAreaEntered(Area3D a)
		{
			if (a is Triggers.StageTrigger trigger)
				trigger.OnEnter();
			else if ((Node)a is Objects.Pickup pickup) //This node cast is NOT redundant, and IS needed
				pickup.OnEnter();
		}

		public void OnObjectAreaExited(Area3D a)
		{
			if (a is Triggers.StageTrigger trigger)
				trigger.OnExit();
		}

		public void OnObjectCollisionEnter(PhysicsBody3D body)
		{
			/*
			Note for when I come back wondering why the player is being pushed through the floor
			Ensure all crushers' animationplayers are using the PHYSICS update mode
			If this is true, then proceed to panic.
			*/
			if (body.IsInGroup("crusher"))
			{
				//Check whether we're ACTUALLy being crushed and not running into the side of the crusher
				RaycastHit hit = this.CastRay(CenterPosition, GroundDirection * COLLISION_RADIUS * 2f, environmentMask, false);
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

		public void OnObjectCollisionExit(PhysicsBody3D body)
		{
			if (body.IsInGroup("crusher") && GetCollisionExceptions().Contains(body))
			{
				GD.Print($"Stopped ignoring {body.Name}");
				RemoveCollisionExceptionWith(body);
			}
		}
		#endregion

		//Components, rarely needs to be edited, so they go at the bottom of the inspector
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
		public NodePath skills;
		public CharacterSkillManager Skills { get; private set; }
		[Export]
		public NodePath lockon;
		public CharacterLockon Lockon { get; private set; }
	}
}
