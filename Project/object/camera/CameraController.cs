using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class CameraController : Spatial
	{
		public static CameraController instance;

		private PathFollow PlayerPathFollower => Player.PathFollower;
		[Export]
		public NodePath calculationRoot;
		private Spatial _calculationRoot; //Responsible for pitch rotation
		[Export]
		public NodePath calculationGimbal;
		private Spatial _calculationGimbal; //Responsible for yaw rotation
		[Export]
		public NodePath cameraRoot;
		private Spatial _cameraRoot;
		[Export]
		public NodePath cameraGimbal;
		private Spatial _cameraGimbal;
		[Export]
		public NodePath camera;
		private Camera _camera;

		private CharacterController Player => CharacterController.instance;

		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => _camera.UnprojectPosition(worldSpace);

		public override void _Ready()
		{
			instance = this;

			_calculationRoot = GetNode<Spatial>(calculationRoot);
			_calculationGimbal = GetNode<Spatial>(calculationGimbal);

			_cameraRoot = GetNode<Spatial>(cameraRoot);
			_cameraGimbal = GetNode<Spatial>(cameraGimbal);
			_camera = GetNode<Camera>(camera);
		}

		public void UpdateCamera()
		{
			UpdateGameplayCamera();

			if (!OS.IsDebugBuild())
				return;

			UpdateFreeCam();
		}

		#region Settings
		[Export]
		public CameraSettingsResource backstepCameraSettings;

		public CameraSettingsResource targetSettings; //End lerp here
		private readonly CameraSettingsResource previousSettings = new CameraSettingsResource(); //Start lerping here
		private readonly CameraSettingsResource currentSettings = new CameraSettingsResource(); //Apply transforms based on this
		public void SetCameraData(CameraSettingsResource data, bool useCrossfade)
		{
			transitionTime = transitionTimeStepped = 0f; //Reset transition timers
			previousSettings.CopyFrom(currentSettings);

			previousSettings.viewAngle.x = currentRotation.x;
			previousSettings.viewAngle.y = currentRotation.y;
			previousTilt = currentTilt;
			previousStrafe = currentStrafe;
			previousYawTracking = currentYawTracking;

			targetSettings = data;

			//Copy modes
			currentSettings.heightMode = data.heightMode;
			currentSettings.followMode = data.followMode;
			currentSettings.strafeMode = data.strafeMode;
			currentSettings.tiltMode = data.tiltMode;
			currentSettings.viewPosition = data.viewPosition;

			if (useCrossfade) //Crossfade transition
			{
				Image img = _calculationGimbal.GetViewport().GetTexture().GetData();
				var tex = new ImageTexture();
				tex.CreateFromImage(img);
				GameplayInterface.instance.PlayCameraTransition(tex);
			}
		}

		private float transitionTime; //Ratio (from 0 -> 1) of transition that has been completed
		private float transitionTimeStepped; //Smoothstepped transition time
		private void UpdateActiveSettings() //Calculate the active camera settings
		{
			if (targetSettings == null) return; //ERROR! No data set.

			/*
			UpdateBackstep();

			if (IsBackStepping)
				currentSettings = backstepCameraSettings;
			*/

			float transitionSpeed = targetSettings.transitionSpeed;
			if (Mathf.IsZeroApprox(transitionSpeed))
				resetFlag = true;

			if (resetFlag)
				transitionTime = 1f;
			else
				transitionTime = Mathf.MoveToward(transitionTime, 1f, (1f / transitionSpeed) * PhysicsManager.physicsDelta);
			transitionTimeStepped = Mathf.SmoothStep(0, 1f, transitionTime);

			currentSettings.distance = Mathf.Lerp(previousSettings.distance, targetSettings.distance, transitionTime);
			currentSettings.height = Mathf.Lerp(previousSettings.height, targetSettings.height, transitionTime);
			currentSettings.heightTrackingStrength = Mathf.Lerp(previousSettings.heightTrackingStrength, targetSettings.heightTrackingStrength, transitionTime);
		}
		#endregion

		#region Backstep Camera
		private float backstepTimer;
		private bool IsBackStepping => backstepTimer >= BACKSTEP_CAMERA_DELAY;
		private const float BACKSTEP_CAMERA_DELAY = .5f;

		private void UpdateBackstep()
		{
			//Backstep camera
			if (Player.MoveSpeed < 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, BACKSTEP_CAMERA_DELAY, PhysicsManager.physicsDelta);
			else if (backstepTimer < BACKSTEP_CAMERA_DELAY || Player.MoveSpeed > 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, 0f, PhysicsManager.physicsDelta);
		}
		#endregion

		#region Gameplay Camera
		private Vector3 localPlayerPosition; //Player's local position, relative to it's path follower
		private void CalculateLocalPlayerPosition()
		{
			localPlayerPosition = Player.GlobalTransform.origin - PlayerPathFollower.GlobalTransform.origin;
			localPlayerPosition = PlayerPathFollower.GlobalTransform.basis.XformInv(localPlayerPosition);
		}

		private bool resetFlag = true; //Set to true to skip smoothing

		private void UpdateGameplayCamera()
		{
			if (Player.ActivePath == null) return; //Uninitialized

			CalculateLocalPlayerPosition();
			UpdateActiveSettings();
			UpdateBasePosition();

			if (!freeCamEnabled) //Apply transform
			{
				_cameraRoot.GlobalTransform = _calculationRoot.GlobalTransform;
				_cameraGimbal.GlobalTransform = _calculationGimbal.GlobalTransform;
			}

			if (resetFlag) //Reset flag
				resetFlag = false;
		}

		private float currentDistance;
		private float currentHeight;
		private float heightVelocity;
		private float distanceVelocity;
		private Vector2 currentRotation;
		private Vector2 rotationVelocity;
		private const float POSITION_SMOOTHING = .2f;
		private const float ROTATION_SMOOTHING = .06f;
		private void UpdateBasePosition()
		{
			UpdateOffsets(); //Height, distance
			UpdateRotation();
			UpdatePosition();
		}

		private Vector3 GetBasePosition(CameraSettingsResource resource)
		{
			if (resource.followMode == CameraSettingsResource.FollowMode.Static)
				return resource.viewPosition;
			else if (resource.followMode == CameraSettingsResource.FollowMode.Pathfollower)
				return PlayerPathFollower.GlobalTransform.origin;
			else
				return Player.GlobalTransform.origin - PlayerPathFollower.Up() * localPlayerPosition.y;
		}

		private Vector3 GetStrafeOffset(CameraSettingsResource resource)
		{
			if (resource.strafeMode == CameraSettingsResource.StrafeMode.Move)
			{
				Vector3 strafeOffset = Player.StrafeDirection * -localPlayerPosition.x * resource.strafeTrackingStrength;
				return strafeOffset;
			}

			return Vector3.Zero;
		}

		private Vector3 GetHeightOffset(CameraSettingsResource resource)
		{
			Vector3 vector = Vector3.Up;
			if (resource.heightMode == CameraSettingsResource.HeightMode.PathFollower)
				vector = PlayerPathFollower.Up();
			else if (resource.heightMode == CameraSettingsResource.HeightMode.Camera)
				vector = _calculationGimbal.Up();

			return vector * localPlayerPosition.y * resource.heightTrackingStrength;
		}

		private void UpdateOffsets()
		{
			if (resetFlag)
			{
				currentHeight = currentSettings.height;
				currentDistance = currentSettings.distance;
				heightVelocity = distanceVelocity = 0f;
			}
			else if(targetSettings.IsStaticCamera)
			{
				currentDistance = 0f;
				currentHeight = 0f;
			}
			else
			{
				currentHeight = ExtensionMethods.SmoothDamp(currentHeight, currentSettings.height, ref heightVelocity, POSITION_SMOOTHING);
				currentDistance = ExtensionMethods.SmoothDamp(currentDistance, currentSettings.distance, ref distanceVelocity, POSITION_SMOOTHING);
			}
		}

		private float previousTilt;
		private float currentTilt;
		private float previousYawTracking;
		private float currentYawTracking;
		private void UpdateRotation()
		{
			Vector3 forwardDirection = PlayerPathFollower.Forward();
			if (Mathf.Abs(PlayerPathFollower.Forward().y) > .8f) //Case of running up a wall
				forwardDirection = Mathf.Sign(PlayerPathFollower.Forward().y) * PlayerPathFollower.Down();

			if (targetSettings.IsStaticCamera)
				forwardDirection = (PlayerPathFollower.GlobalTransform.origin - _calculationRoot.GlobalTransform.origin).Normalized();

			Vector3 forwardFlattened = forwardDirection.Flatten().Normalized();
			Vector3 rightDirection = forwardFlattened.Cross(Vector3.Up);

			Vector2 targetRotation = Vector2.Zero;
			if (targetSettings.overrideYaw)
				targetRotation.y = Mathf.LerpAngle(previousSettings.viewAngle.y, Mathf.Deg2Rad(targetSettings.viewAngle.y), transitionTime);
			else
				targetRotation.y = forwardFlattened.SignedAngleTo(Vector3.Forward, Vector3.Up);

			if (targetSettings.overridePitch)
				targetRotation.x = Mathf.LerpAngle(previousSettings.viewAngle.x, Mathf.Deg2Rad(targetSettings.viewAngle.x), transitionTime);
			else if (!targetSettings.IsStaticCamera)
			{
				float cachedRotation = _calculationRoot.Rotation.y;
				_calculationRoot.Rotation = Vector3.Down * targetRotation.y; //Temporarily apply the rotation so pitch calculation can be correct
				targetRotation.x = forwardFlattened.SignedAngleTo(PlayerPathFollower.Forward(), rightDirection);

				//Reset rotation
				_calculationRoot.Rotation = Vector3.Up * cachedRotation;
			}

			targetRotation.x = Mathf.LerpAngle(previousSettings.viewAngle.x, targetRotation.x, transitionTimeStepped);
			targetRotation.y = Mathf.LerpAngle(previousSettings.viewAngle.y, targetRotation.y, transitionTimeStepped);

			if (resetFlag)
				currentRotation = targetRotation;
			else //Smooth out rotations
			{
				currentRotation = new Vector2(ExtensionMethods.SmoothDampAngle(currentRotation.x, targetRotation.x, ref rotationVelocity.x, ROTATION_SMOOTHING),
					ExtensionMethods.SmoothDampAngle(currentRotation.y, targetRotation.y, ref rotationVelocity.y, ROTATION_SMOOTHING));
			}

			//Update Pitch
			float pitchTracking = 0;
			if (!targetSettings.IsStaticCamera)
				pitchTracking = -new Vector2(currentDistance, localPlayerPosition.y).AngleTo(Vector2.Right) * (1 - currentSettings.heightTrackingStrength);

			//Apply Rotation
			_calculationRoot.Rotation = Vector3.Down * currentRotation.y;
			_calculationGimbal.Rotation = Vector3.Zero;

			//Calculate tilt
			Vector3 tiltVector = PlayerPathFollower.Right().Rotated(Vector3.Up, forwardFlattened.SignedAngleTo(Vector3.Forward, Vector3.Up));
			float tiltAmount = tiltVector.SignedAngleTo(Vector3.Left, Vector3.Forward);
			float targetTilt = targetSettings.tiltMode == CameraSettingsResource.TiltMode.Disable ? 0 : tiltAmount;
			currentTilt = Mathf.LerpAngle(previousTilt, targetTilt, transitionTimeStepped);
			_calculationGimbal.RotateObjectLocal(Vector3.Back, currentTilt);

			float targetYawTracking = 0f;
			if (targetSettings.strafeMode == CameraSettingsResource.StrafeMode.Rotate && Mathf.Abs(localPlayerPosition.x) > 1f) //Track left/right
			{
				Vector2 v = new Vector2((Mathf.Abs(localPlayerPosition.x) - 1f) * Mathf.Sign(localPlayerPosition.x), currentDistance);
				targetYawTracking = v.AngleTo(Vector2.Down);
			}

			currentYawTracking = Mathf.LerpAngle(currentYawTracking, targetYawTracking, .2f);

			_calculationGimbal.RotateObjectLocal(Vector3.Up, Mathf.Lerp(previousYawTracking, currentYawTracking, transitionTimeStepped));
			_calculationGimbal.RotateObjectLocal(Vector3.Right, currentRotation.x + pitchTracking);
		}

		private Vector3 previousStrafe;
		private Vector3 currentStrafe;
		private void UpdatePosition()
		{
			//Calculate positions
			Vector3 targetPosition = GetBasePosition(previousSettings).LinearInterpolate(GetBasePosition(targetSettings), transitionTimeStepped);

			//Distance
			Vector3 offset = _calculationRoot.Forward().Rotated(_calculationRoot.Right(), currentRotation.x);
			targetPosition += offset * currentDistance;

			//Height
			offset = offset.Rotated(_calculationRoot.Right(), Mathf.Pi * .5f - Mathf.Deg2Rad(currentSettings.viewAngle.x));
			targetPosition -= offset * currentHeight;

			//Height Tracking
			offset = GetHeightOffset(previousSettings).LinearInterpolate(GetHeightOffset(targetSettings), transitionTimeStepped);
			targetPosition += offset;

			currentStrafe = previousStrafe.LinearInterpolate(GetStrafeOffset(targetSettings), transitionTimeStepped); //Update Strafe
			targetPosition += currentStrafe;

			Transform t = _calculationRoot.GlobalTransform;
			t.origin = targetPosition;
			_calculationRoot.GlobalTransform = t;
		}
		#endregion

		#region Free Cam
		private float freecamMovespeed = 20;
		private const float MOUSE_SENSITIVITY = .2f;

		private bool freeCamEnabled;
		private bool freeCamRotating;

		private void UpdateFreeCam()
		{
			if (Input.IsKeyPressed((int)KeyList.R))
			{
				freeCamEnabled = freeCamRotating = false;
				_calculationRoot.Visible = false;
				resetFlag = true;
			}

			freeCamRotating = Input.IsMouseButtonPressed((int)ButtonList.Left);
			if (freeCamRotating)
			{
				freeCamEnabled = true;
				_calculationRoot.Visible = true;
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;

			if (!freeCamEnabled) return;

			float targetMoveSpeed = freecamMovespeed;

			if (Input.IsKeyPressed((int)KeyList.Shift))
				targetMoveSpeed *= 2;
			else if (Input.IsKeyPressed((int)KeyList.Control))
				targetMoveSpeed *= .5f;

			if (Input.IsKeyPressed((int)KeyList.E))
				_cameraRoot.GlobalTranslate(_camera.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.Q))
				_cameraRoot.GlobalTranslate(_camera.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.W))
				_cameraRoot.GlobalTranslate(_camera.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.S))
				_cameraRoot.GlobalTranslate(_camera.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.D))
				_cameraRoot.GlobalTranslate(_camera.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.A))
				_cameraRoot.GlobalTranslate(_camera.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
		}

		public override void _Input(InputEvent e)
		{
			if (!freeCamRotating)
			{
				e.Dispose(); //Be sure to dispose events! Otherwise a memory leak may occour.
				return;
			}

			if (e is InputEventMouseMotion)
			{
				_cameraRoot.RotateY(Mathf.Deg2Rad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY);
				_cameraGimbal.RotateX(Mathf.Deg2Rad(-(e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
				_cameraGimbal.RotationDegrees = Vector3.Right * Mathf.Clamp(_cameraGimbal.RotationDegrees.x, -90, 90);
			}
			else if (e is InputEventMouseButton emb)
			{
				if (emb.IsPressed())
				{
					if (emb.ButtonIndex == (int)ButtonList.WheelUp)
					{
						freecamMovespeed += 5;
						GD.Print($"Free cam Speed set to {freecamMovespeed}.");
					}
					if (emb.ButtonIndex == (int)ButtonList.WheelDown)
					{
						freecamMovespeed -= 5;
						if (freecamMovespeed < 0)
							freecamMovespeed = 0;
						GD.Print($"Free cam Speed set to {freecamMovespeed}.");
					}
				}
			}

			e.Dispose();
		}
		#endregion
	}
}