using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay
{
	public class CharacterController : KinematicBody
	{
		public static CharacterController instance;

		[Export]
		public NodePath pathFollower;
		public PathFollow PathFollower { get; private set; }
		private float pathFollowerOffset; //Offset used when a non-looping path ends.
		[Export]
		public NodePath objectArea; //Area node for interacting with objects
		private Area _objectArea;
		[Export]
		public NodePath targetArea; //Area node for targeting objects
		private Area _targetArea;
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionHeight = .4f;
		[Export(PropertyHint.Range, "0, .5")]
		public float collisionWidth = .3f;
		[Export]
		public NodePath root;
		private Spatial _root;

		#region Controls
		public InputManager.Controller Controller => InputManager.controller;

		public bool sideScroller; //Are we in a 2D section?
		public bool facingRight; //Determines which way on the controller is back (Only in sidescroller)
		private int GetMovementInputDirection()
		{
			//Returns 1 for moving forward, -1 for moving backwards
			if (!sideScroller)
				return Controller.verticalAxis.sign;

			return facingRight ? Controller.horizontalAxis.sign : -Controller.horizontalAxis.sign;
		}
		private int GetStrafeInputDirection()
		{
			//Returns 1 for moving right, -1 for moving left
			if (!sideScroller)
				return Controller.horizontalAxis.sign;

			return facingRight ? Controller.verticalAxis.sign : -Controller.verticalAxis.sign;
		}

		private bool isControlsLocked;
		private float controlLockoutTimer;
		public ControlLockoutResource controlLockoutData;

		public void ResetControlLockout()
		{
			controlLockoutTimer = 0;
			isControlsLocked = false;
		}
		public void SetControlLockout(ControlLockoutResource data)
		{
			isControlsLocked = true;
			controlLockoutData = data;
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
		#endregion

		public override void _Ready()
		{
			instance = this;
			_objectArea = GetNode<Area>(objectArea);
			_objectArea.Connect("area_entered", this, nameof(OnObjectTriggerEnter));
			_objectArea.Connect("area_exited", this, nameof(OnObjectTriggerExit));
			_targetArea = GetNode<Area>(targetArea);
			_targetArea.Connect("area_entered", this, nameof(OnTargetTriggerEnter));
			_targetArea.Connect("area_exited", this, nameof(OnTargetTriggerExit));

			PathFollower = GetNode<PathFollow>(pathFollower);

			_root = GetNode<Spatial>(root);
			activeCollisionMask = environmentMask;
		}

		public override void _PhysicsProcess(float _)
		{
			//if(paused) return;

			ProcessStateMachine();

			UpdatePhysics();
			UpdateAnimation();
			CameraController.instance.UpdateCamera();
		}

		#region State Machine
		public enum MovementState
		{
			Normal, //Standard on rails movement
			Event,  //Cutscene? Cinematics? May be replaced with input lockout.
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
		private MovementState movementState;
		private bool customPhysicsEnabled;

		private void ProcessStateMachine()
		{
			if (!GameplayInterface.instance.IsCountDownComplete)
			{
				//Fall to the ground
				return;
			}

			customPhysicsEnabled = false;

			switch (movementState)
			{
				case MovementState.Normal:
					UpdateNormalState();
					break;
				case MovementState.Launcher:
					UpdateLauncher();
					break;
				case MovementState.Drift:
					UpdateDrift();
					break;
			}

			UpdateControlLockTimer();
		}
		#endregion

		#region Normal State
		private void UpdateNormalState()
		{
			UpdateMoveSpeed();
			UpdateStrafeSpeed();
			UpdateJump();
		}

		[Export]
		public float runSpeed; //Max speed forward
		[Export]
		public float traction; //Speed up rate
		[Export]
		public float friction; //Slow down rate
		[Export]
		public float brakes; //Skidding

		[Export]
		public float airSpeed; //Max speed forward
		[Export]
		public float airTraction; //Speed up rate
		[Export]
		public float airFriction; //Slow down rate
		[Export]
		public float airBrakes; //Skidding

		[Export]
		public float strafeSpeed; //Max speed side to side
		[Export]
		public float strafeTraction; //Side to side acceleration
		[Export]
		public float strafeFriction; //Side to side decceleration
		[Export]
		public float strafeBrakes; //Side to side skidding
		[Export]
		public float airStrafeSpeed; //Max speed side to side
		[Export]
		public float airStrafeTraction; //Side to side acceleration
		[Export]
		public float airStrafeFriction; //Side to side decceleration
		[Export]
		public float airStrafeBrakes; //Side to side skidding

		[Export]
		public float backstepSpeed; //Max speed backwards
		[Export]
		public float backstepTraction; //Speed up rate
		[Export]
		public float backstepFriction; //Speed up rate

		private void UpdateMoveSpeed()
		{
			if (isControlsLocked && !Mathf.IsZeroApprox(controlLockoutData.speedRatio))
			{
				//Change speed to the correct value
				MoveSpeed = Mathf.MoveToward(MoveSpeed, runSpeed * controlLockoutData.speedRatio, traction * (controlLockoutData.tractionRatio == 0 ? 1 : controlLockoutData.tractionRatio) * PhysicsManager.physicsDelta);
				return;
			}

			int inputDirection = GetMovementInputDirection();

			if (MoveSpeed >= 0)
			{
				//Moving forward
				if (IsOnGround)
				{
					if (inputDirection > 0) //Accelerate
						MoveSpeed = Mathf.MoveToward(MoveSpeed, runSpeed, traction * PhysicsManager.physicsDelta);
					else if (inputDirection < 0) //Skidding
						MoveSpeed = Mathf.MoveToward(MoveSpeed, backstepSpeed, brakes * PhysicsManager.physicsDelta);
					else //Stop
						MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, friction * PhysicsManager.physicsDelta);
				}
				else
				{
					//Air movement
					if (MoveSpeed > airSpeed)
						MoveSpeed = Mathf.MoveToward(MoveSpeed, airSpeed, airBrakes * PhysicsManager.physicsDelta);
					else if (inputDirection > 0)
						MoveSpeed = Mathf.MoveToward(MoveSpeed, airSpeed, airTraction * PhysicsManager.physicsDelta);
					else if (inputDirection < 0) //Skidding
						MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, airBrakes * PhysicsManager.physicsDelta);
					else //Stop
						MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, airFriction * PhysicsManager.physicsDelta);
				}
			}
			else if (IsOnGround)
			{
				if (inputDirection >= 0)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, backstepFriction * PhysicsManager.physicsDelta);
				else if (inputDirection < 0)
					MoveSpeed = Mathf.MoveToward(MoveSpeed, backstepSpeed, backstepTraction * PhysicsManager.physicsDelta);
			}
			else
				MoveSpeed = Mathf.MoveToward(MoveSpeed, 0, airFriction * PhysicsManager.physicsDelta);
		}

		private void UpdateStrafeSpeed()
		{
			if (isControlsLocked && controlLockoutData.strafeSettings != ControlLockoutResource.StrafeSettings.Default)
			{
				if (controlLockoutData.strafeSettings == ControlLockoutResource.StrafeSettings.Recenter)
				{
					//Calculate distance along the plane defined by StrafeDirection
					Vector3 calculationPoint = GlobalTransform.origin - PathFollower.GlobalTransform.origin;
					Vector3 rotationAxis = StrafeDirection.Cross(Vector3.Right).Normalized();
					if (rotationAxis.IsNormalized())
						calculationPoint = calculationPoint.Rotated(rotationAxis, StrafeDirection.AngleTo(Vector3.Right));

					float distanceFromCenter = calculationPoint.x;

					if (MoveSpeed != 0)
					{
						StrafeSpeed = Mathf.MoveToward(StrafeSpeed, -distanceFromCenter / PhysicsManager.physicsDelta, strafeTraction * PhysicsManager.physicsDelta);

						//Speed clamp
						//Center Smoothing
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -Mathf.Abs(MoveSpeed), Mathf.Abs(MoveSpeed));
						float speedClamp = (Mathf.Abs(distanceFromCenter) - collisionWidth) / STRAFE_SMOOTHING_LENGTH;
						StrafeSpeed = Mathf.Clamp(StrafeSpeed, -speedClamp, speedClamp);
					}
					else
						StrafeSpeed = Mathf.MoveToward(StrafeSpeed, 0, strafeFriction * PhysicsManager.physicsDelta);
				}

				return;
			}

			float delta = 0;
			float target = 0;
			if (StrafeSpeed == 0 || Mathf.Sign(StrafeSpeed) == Controller.horizontalAxis.sign)
			{
				target = (IsOnGround ? strafeSpeed : airStrafeSpeed) * Controller.horizontalAxis.value;
				delta = IsOnGround ? strafeTraction : airStrafeTraction;
			}
			else if (Controller.horizontalAxis.sign == 0)
			{
				target = 0;
				delta = IsOnGround ? strafeFriction : airStrafeFriction;
			}
			else
			{
				target = (IsOnGround ? strafeSpeed : airStrafeSpeed) * Controller.horizontalAxis.value;
				delta = IsOnGround ? strafeBrakes : airStrafeBrakes;
			}

			StrafeSpeed = Mathf.MoveToward(StrafeSpeed, target, delta * PhysicsManager.physicsDelta);
		}

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
		private const float ACCELERATION_JUMP_LENGTH = .1f; //How fast the jump button needs to be pressed for an "acceleration jump"

		[Export]
		public float stompSpeed;
		private bool isStomping;

		[Export]
		public float landingTurbo; //Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster.
		private void UpdateJump()
		{
			if (IsOnGround)
			{
				if (isControlsLocked)
				{
					if (controlLockoutData.resetOnLand && justLandedOnGround)
					{
						ResetControlLockout();
						return;
					}

					if (controlLockoutData.disableJumping)
						return;
				}

				if (justLandedOnGround)
				{
					isJumping = false;
					canJumpDash = true;
					jumpedFromGround = false;
					isAccelerationJump = false;
					currentJumpLength = 0;

					if (isStomping)
						isStomping = false;
					else if (Controller.verticalAxis.value > 0 && MoveSpeed < landingTurbo)
					{
						//Boost forwards slightly when holding forward (See Sonic and the Black Knight)
						MoveSpeed = landingTurbo;
						//Play vfx
					}
				}

				if (Controller.jumpButton.wasPressed)
				{
					if (GetMovementInputDirection() < 0 || MoveSpeed < 0)
					{
						//Backflip
					}
					else
					{
						//Jump
						isJumping = true;
						IsOnGround = false;
						jumpedFromGround = true;
						jumpGroundNormal = -ForwardDirection.y;

						VerticalSpeed = jumpPower;
					}
				}

				return;
			}

			if (Controller.actionButton.wasPressed) //Pressing the action button in the air will cause sonic to drop
			{
				//Fast fall
				isStomping = true;
				MoveSpeed = 0;
				VerticalSpeed = stompSpeed;
				return;
			}

			if (Controller.jumpButton.wasPressed)
			{
				//Midair attacks and stuff
				if (canJumpDash)
				{
					//TODO Check for Homing attack
					canJumpDash = false;
				}
			}

			if (jumpedFromGround)
			{
				currentJumpLength += PhysicsManager.physicsDelta;
				if (currentJumpLength > ACCELERATION_JUMP_LENGTH && isAccelerationJump)
				{
					//Acceleration jump dash
					if (Controller.verticalAxis.value > 0 && MoveSpeed < accelerationJumpSpeed)
						MoveSpeed = accelerationJumpSpeed;
					VerticalSpeed = 5f;
					isAccelerationJump = false;
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

			UpdateGravity();
		}

		[Export]
		public float gravity;
		[Export]
		public float maxGravity;
		private void UpdateGravity()
		{
			VerticalSpeed = Mathf.MoveToward(VerticalSpeed, maxGravity, gravity * PhysicsManager.physicsDelta);
		}
		#endregion

		#region Launcher
		private float launcherTime;
		private Launcher activeLauncher;
		private Vector3 launcherMovementDelta;
		public void StartLauncher(Launcher newLauncher)
		{
			movementState = MovementState.Launcher;
			activeLauncher = newLauncher;
			MoveSpeed = 0;
			VerticalSpeed = 0;
			StrafeSpeed = 0;

			launcherTime = 0;
			IsOnGround = false;
			jumpedFromGround = false;
			launcherMovementDelta = Vector3.Zero;
			canJumpDash = activeLauncher.refreshJumpDash;
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

			Vector3 startingPosition = activeLauncher.CalculatePosition(launcherTime);
			launcherTime += PhysicsManager.physicsDelta;
			Vector3 targetPosition = activeLauncher.CalculatePosition(launcherTime);
			Vector3 movementDelta = (targetPosition - startingPosition) / PhysicsManager.physicsDelta;
			MoveAndSlide(movementDelta);

			if (movementDelta != Vector3.Zero)
				launcherMovementDelta = movementDelta;

			if (activeLauncher.IsLaunchFinished(launcherTime)) //Revert to normal state
			{
				movementState = MovementState.Normal;
				ResyncPathFollower();

				//Calculate velocity to carry through based on the launch direction
				float exitSpeed = launcherMovementDelta.Length() * activeLauncher.momentumMultiplier;  //"True" exit speed
				Vector3 exitNormal = launcherMovementDelta.Normalized();
				VerticalSpeed = exitSpeed * exitNormal.y;
				float forwardDotProd = exitNormal.RemoveVertical().Normalized().Dot(-ForwardDirection.RemoveVertical().Normalized());
				MoveSpeed = exitSpeed * forwardDotProd;
				StrafeSpeed = exitSpeed * (1 - Mathf.Abs(forwardDotProd)) * Mathf.Sign(forwardDotProd);
			}
		}

		public void LauncherReset()
		{
			launcherTime = 0;
		}
		#endregion

		#region Drift
		private DriftCorner activeCorner;
		private bool isDriftTurned;
		public void StartDrift(DriftCorner corner)
		{
			isDriftTurned = false;
			activeCorner = corner;
			movementState = MovementState.Drift;

			StrafeSpeed = 0;
			VerticalSpeed = 0;
		}

		private void UpdateDrift()
		{
			customPhysicsEnabled = true;

			Vector3 targetPosition = isDriftTurned ? activeCorner.EndPosition : activeCorner.MiddlePosition;
			Transform t = GlobalTransform;
			float distance = GlobalTransform.origin.RemoveVertical().DistanceTo(targetPosition.RemoveVertical());

			if (isDriftTurned)
			{
				MoveSpeed = runSpeed;
				t.origin = t.origin.MoveToward(targetPosition, MoveSpeed * PhysicsManager.physicsDelta);

				if (distance < activeCorner.slideDistance * .2f)
				{
					ResyncPathFollower();
					movementState = MovementState.Normal;
				}
			}
			else
			{
				MoveSpeed = (distance - MoveSpeed * PhysicsManager.physicsDelta) / activeCorner.slideDistance;

				if (distance < .1f)
				{
					isDriftTurned = true;
					t.origin = new Vector3(targetPosition.x, t.origin.y, targetPosition.z);
				}
				else
					t.origin = t.origin.MoveToward(targetPosition, MoveSpeed);
			}

			GlobalTransform = t;
		}
		#endregion

		#region Physics
		[Export(PropertyHint.Layers3dPhysics)]
		public uint environmentMask; //Collision mask, excluding one ways.
		[Export(PropertyHint.Layers3dPhysics)]
		public uint oneWayMask; //Collisions mask with one way included
		private uint activeCollisionMask; //Collision mask currently being used
		[Export]
		public OneWayCollisionMode oneWayCollisionMode;
		public enum OneWayCollisionMode
		{
			Always,
			Disable,
			MovingForward,
			MovingBackward
		}
		private bool oneWayCollisionsEnabled;
		private void UpdateOneWayCollisionMode()
		{
			bool targetValue = oneWayCollisionsEnabled;
			switch (oneWayCollisionMode)
			{
				case OneWayCollisionMode.Always:
					targetValue = true;
					break;
				case OneWayCollisionMode.Disable:
					targetValue = false;
					break;
				case OneWayCollisionMode.MovingForward:
					targetValue = MoveSpeed > 0;
					break;
				case OneWayCollisionMode.MovingBackward:
					targetValue = MoveSpeed < 0;
					break;
			}

			if (oneWayCollisionsEnabled == targetValue) return; //Nothing changed

			oneWayCollisionsEnabled = targetValue;
			if (oneWayCollisionsEnabled)
				activeCollisionMask = oneWayMask; //disable one way mask
			else
				activeCollisionMask = environmentMask; //Enable one way mask
		}

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
		private Vector3 velocity; //x -> strafe, y -> jump/fall, z -> speed

		private bool IsFalling => VerticalSpeed < 0;
		private bool IsRising => VerticalSpeed > 0;

		public Vector3 CenterPosition => GlobalTransform.origin + worldDirection * collisionHeight; //Center of collision calculations

		public Vector3 ForwardDirection => PathFollower.Forward() * PathTravelDirection;
		public Vector3 StrafeDirection { get; private set; }

		private readonly List<StageObject> activeTriggers = new List<StageObject>();
		private readonly List<Spatial> activeTargets = new List<Spatial>(); //List of targetable objects
		private void UpdateTriggers()
		{
			for (int i = 0; i < activeTriggers.Count; i++)
			{
				activeTriggers[i].Character = this;
				activeTriggers[i].OnStay();
			}

			//Calculate homing attack targets
		}

		private void OnObjectTriggerEnter(Area area)
		{
			Node node = (Node)area;

			if (node is StageObject)
			{
				StageObject target = node as StageObject;

				target.Character = this;
				target.OnEnter();

				if (!activeTriggers.Contains(target))
					activeTriggers.Add(target);
			}
		}

		private void OnObjectTriggerExit(Area area)
		{
			Node node = (Node)area;
			if (node is StageObject)
			{
				StageObject target = node as StageObject;

				target.Character = this;
				target.OnExit();

				if (activeTriggers.Contains(target))
					activeTriggers.Remove(target);
			}
		}

		private void OnTargetTriggerEnter(Area area)
		{
			Node node = (Node)area;
			if (node is Spatial)
			{
				Spatial target = node as Spatial;

				if (!activeTargets.Contains(target))
					activeTargets.Add(target);
			}
		}

		private void OnTargetTriggerExit(Area area)
		{
			Node node = (Node)area;
			if (node is Spatial)
			{
				Spatial target = node as Spatial;

				if (activeTargets.Contains(target))
					activeTargets.Remove(target);
			}
		}

		private void UpdatePhysics()
		{
			UpdateOneWayCollisionMode();
			UpdateTriggers();

			if (customPhysicsEnabled) return; //When physics are handled in the state machine

			/*Movement method
			Move path follower first,
			Perform all nessecary collision checks
			Then move character controller to follow
			Resync

			Small issue is that the character doesn't exactly follow the path,
			*/
			float movementDelta = MoveSpeed * PhysicsManager.physicsDelta;
			Vector3 movementDirection = -ForwardDirection;

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
			movementDirection = movementDirection.LinearInterpolate(-ForwardDirection, .5f);
			//FIX Smooths "bumping" that occours when jumping up a surface that has a sudden change in steepness.
			if (jumpedFromGround && movementDirection.y > jumpGroundNormal)
			{
				jumpGroundNormal = Mathf.Lerp(jumpGroundNormal, movementDirection.y, .1f);
				movementDirection.y = jumpGroundNormal;
			}

			movementDirection = movementDirection.Normalized();
			//Note: If you want to allow the player to get lots of air when jumping down a slope, set movementDirection's Y value to zero when falling.

			CheckMainWall(movementDirection);
			CheckGround();

			StrafeDirection = -ForwardDirection.Cross(worldDirection).Normalized();
			//Check both sides of strafing
			CheckStrafeWall(1);
			CheckStrafeWall(-1);

			MoveAndSlide(movementDirection * MoveSpeed + StrafeDirection * StrafeSpeed + worldDirection * VerticalSpeed);
			CheckCeiling();

			ResyncPathFollower();
		}

		public Vector3 worldDirection = Vector3.Up;
		private const float COLLISION_PADDING = .01f;

		public bool IsOnGround { get; private set; }
		private bool justLandedOnGround; //Flag for doing stuff on land
		private const float GROUND_SNAP_LENGTH = 1f;
		private const float MAX_ANGLE_CHANGE = 60f;

		private void CheckGround()
		{
			Vector3 castOrigin = CenterPosition;
			float castLength = collisionHeight;
			if (IsFalling)
				castLength += Mathf.Abs(VerticalSpeed) * PhysicsManager.physicsDelta;
			else if (IsOnGround)
				castLength += GROUND_SNAP_LENGTH;
			else
				castLength -= .1f;

			Vector3 castVector = -worldDirection * castLength;
			RaycastHit groundHit = this.CastRay(castOrigin, castVector, activeCollisionMask);
			Debug.DrawRay(castOrigin, castVector, groundHit ? Colors.Red : Colors.White);

			if (groundHit)
			{
				//Fix don't allow 90 degree angle changes in a single frame
				if (Mathf.Rad2Deg(groundHit.normal.AngleTo(Vector3.Up) - worldDirection.AngleTo(Vector3.Up)) > MAX_ANGLE_CHANGE)
					return;

				//Fix microscopic jittering on flat surfaces
				Vector3 newNormal = (groundHit.normal * 100).Round() * .01f; //Round to nearest hundredth
				worldDirection = newNormal.Normalized();

				justLandedOnGround = !IsOnGround;

				if (justLandedOnGround)
				{
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

				if (!jumpedFromGround || IsFalling)
					worldDirection = worldDirection.LinearInterpolate(Vector3.Up, .1f).Normalized();
			}
		}

		private void CheckCeiling() //Checks the ceiling.
		{
			/*Auto Kinematic method
			if (IsOnCeiling() && IsRising)
				VerticalSpeed = 0;
			*/

			//Manual raycast method
			Vector3 castOrigin = CenterPosition;
			float castLength = collisionHeight;

			Vector3 castVector = Vector3.Up * castLength;
			if (IsRising)
				castVector.y += VerticalSpeed * PhysicsManager.physicsDelta;

			RaycastHit ceilingHit = this.CastRay(castOrigin, castVector, activeCollisionMask);
			Debug.DrawRay(castOrigin, castVector, ceilingHit ? Colors.Red : Colors.White);

			if (ceilingHit)
			{
				GlobalTranslate(ceilingHit.point - (CenterPosition + Vector3.Up * collisionHeight));

				if (IsRising)
					VerticalSpeed = 0;
			}
		}

		private const float COLLISION_ROUNDNESS = .1f; //Extra padding value for the main wall cast

		//Checks for walls forward and backwards (only in the direction the player is moving).
		private void CheckMainWall(Vector3 castVector)
		{
			if (MoveSpeed == 0) return; //No movement.

			castVector *= Mathf.Sign(MoveSpeed);
			float castLength = collisionHeight + Mathf.Abs(MoveSpeed) * PhysicsManager.physicsDelta;
			Vector3 sidewaysOffset = StrafeDirection * (collisionWidth - COLLISION_PADDING - COLLISION_ROUNDNESS);

			RaycastHit leftHit = this.CastRay(CenterPosition - sidewaysOffset, castVector * castLength, activeCollisionMask);
			RaycastHit rightHit = this.CastRay(CenterPosition + sidewaysOffset, castVector * castLength, activeCollisionMask);
			Debug.DrawRay(CenterPosition - sidewaysOffset, castVector * castLength, leftHit ? Colors.Red : Colors.White);
			Debug.DrawRay(CenterPosition + sidewaysOffset, castVector * castLength, rightHit ? Colors.Red : Colors.White);

			//Ignore collisions that are "side walls", which are dealt with in the Strafe Wall check
			if (leftHit && CompareNormal2D(leftHit.normal, castVector) < .5f)
				leftHit = new RaycastHit();
			if (rightHit && CompareNormal2D(rightHit.normal, castVector) < .5f)
				rightHit = new RaycastHit();

			if (leftHit || rightHit)
			{
				bool useRightRaycast = rightHit;
				bool pushingCorner = false; //True when both raycasts are hit and the signs of the dot products aren't equal
				if (rightHit && leftHit)
				{
					useRightRaycast = rightHit.distance <= leftHit.distance;
					pushingCorner = Mathf.Sign(rightHit.normal.Dot(StrafeDirection)) != Mathf.Sign(leftHit.normal.Dot(StrafeDirection));
				}

				float wallRatio;

				Transform t = GlobalTransform;
				if (useRightRaycast)
				{
					wallRatio = Mathf.Abs(rightHit.normal.Dot(ForwardDirection));
					t.origin = rightHit.point - castVector * collisionHeight - sidewaysOffset;
				}
				else
				{
					wallRatio = Mathf.Abs(leftHit.normal.Dot(ForwardDirection));
					t.origin = leftHit.point - castVector * collisionHeight + sidewaysOffset;
				}

				if (oneWayCollisionsEnabled) //Only needs wall snapping for one way walls. Normal collisions are handled by the kinematicbody
					GlobalTransform = t;

				if (wallRatio > .9f || pushingCorner)
					MoveSpeed = 0;
			}
		}

		private const float STRAFE_SMOOTHING_LENGTH = .05f; //At what distance to apply smoothing to strafe (To avoid "Bumpy Corners")

		//Checks for wall collision side to side. (Always active)
		private void CheckStrafeWall(int direction)
		{
			bool isActiveDirection = Mathf.Sign(StrafeSpeed) == direction;
			Vector3 castOrigin = CenterPosition;
			float castLength = (collisionWidth + STRAFE_SMOOTHING_LENGTH + COLLISION_PADDING);
			if (isActiveDirection)
				castLength += Mathf.Abs(StrafeSpeed) * PhysicsManager.physicsDelta;

			Vector3 castOffset = ForwardDirection * (collisionHeight - COLLISION_PADDING);
			Vector3 castVector = StrafeDirection * castLength * direction;
			RaycastHit forwardHit = this.CastRay(castOrigin - castOffset, castVector, activeCollisionMask);
			RaycastHit backwardHit = this.CastRay(castOrigin + castOffset, castVector, activeCollisionMask);
			Debug.DrawRay(castOrigin - castOffset, castVector, forwardHit ? Colors.Red : Colors.White);
			Debug.DrawRay(castOrigin + castOffset, castVector, backwardHit ? Colors.Red : Colors.White);

			//Ignore collisions that should be dealt with by Main Wall check
			if (forwardHit && CompareNormal2D(forwardHit.normal, StrafeDirection) < .4f)
				forwardHit = new RaycastHit();
			if (backwardHit && CompareNormal2D(backwardHit.normal, StrafeDirection) < .4f)
				backwardHit = new RaycastHit();

			if (forwardHit || backwardHit)
			{
				bool useForwardCast = forwardHit;
				if (forwardHit && backwardHit)
					useForwardCast = forwardHit.distance <= backwardHit.distance ? forwardHit : backwardHit;

				if (useForwardCast)
					ProcessStrafeWallCollision(isActiveDirection, direction, forwardHit, castOffset);
				else
					ProcessStrafeWallCollision(isActiveDirection, direction, backwardHit, -castOffset);
			}

			//Sideline raycasts for geometry that pokes outwards. If these are hit, strafe speed is instantly set to zero
			castOffset += StrafeDirection * direction * (collisionWidth - COLLISION_PADDING);
			castVector = -ForwardDirection * (collisionHeight * 2);
			RaycastHit hit = this.CastRay(castOrigin + castOffset, castVector, activeCollisionMask);
			Debug.DrawRay(castOrigin + castOffset, castVector, hit ? Colors.Blue : Colors.White);

			if (hit && isActiveDirection && CompareNormal2D(hit.normal, StrafeDirection) >= .5f)
				StrafeSpeed = 0;
		}

		private void ProcessStrafeWallCollision(bool isActiveDirection, int direction, RaycastHit raycast, Vector3 snapOffset)
		{
			if (!isActiveDirection && raycast.distance > collisionWidth) return;

			if (raycast.distance > collisionWidth)
			{
				//Smooth
				float speedClamp = (raycast.distance - collisionWidth) / STRAFE_SMOOTHING_LENGTH;
				StrafeSpeed = Mathf.Clamp(StrafeSpeed, -speedClamp, speedClamp);
			}
			else
			{
				GlobalTranslate(raycast.point - CenterPosition - StrafeDirection * collisionWidth * direction + snapOffset);

				if (isActiveDirection)
					StrafeSpeed = 0;
			}
		}

		//Returns the absolute dot product of a normal relative to an axis ignoring Y values.
		private float CompareNormal2D(Vector3 normal, Vector3 axis) => Mathf.Abs(normal.RemoveVertical().Normalized().Dot(axis.RemoveVertical().Normalized()));
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

			Debug.DrawRay(CenterPosition, ForwardDirection * 10, Colors.Blue);

			Transform t = GlobalTransform;
			t.basis.z = -ForwardDirection;
			t.basis.y = worldDirection;
			t.basis.x = -t.basis.z.Cross(worldDirection);
			t.basis = t.basis.Orthonormalized();
			GlobalTransform = GlobalTransform.InterpolateWith(t, .3f);
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
			if (PathFollower.IsInsideTree())
				PathFollower.GetParent().RemoveChild(PathFollower);

			ActivePath = newPath;
			Curve3D curve = newPath.Curve;
			PathFollower.Loop = curve.GetPointPosition(0).IsEqualApprox(curve.GetPointPosition(curve.GetPointCount() - 1));

			//Reset offset transform
			pathFollowerOffset = 0;

			newPath.AddChild(PathFollower);
			ResyncPathFollower();

			GD.Print($"New active path set to {newPath.Name}");
		}

		private void ResyncPathFollower()
		{
			if (ActivePath == null || pathFollowerOffset != 0) return;

			/*While the vertical position of the curve is ignored for the most part,
			certain overlapping sections require curve to be placed roughly following the ground
			so GetClosestOffset() can get the correct point.
			*/
			PathFollower.Offset = ActivePath.Curve.GetClosestOffset(GlobalTransform.origin - ActivePath.GlobalTransform.origin);
		}
		#endregion
	}
}
