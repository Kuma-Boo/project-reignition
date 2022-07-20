/*
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
		public CameraSettingsResource defaultCameraSettings;
		[Export]
		public CameraSettingsResource backstepCameraSettings;
		[Export]
		public CameraSettingsResource overrideCameraSettings;
		private CameraSettingsResource interpolationSettings; //Settings to start a lerp from
		private readonly CameraSettingsResource activeSettings = new CameraSettingsResource();
		public void SetCameraData(CameraSettingsResource data, bool useCrossfade)
		{
			interpolationSettings = activeSettings.Duplicate() as CameraSettingsResource;

			transitionTime = 0f;
			if ((data != null && data.IsOverridingViewAngle) ||
				(overrideCameraSettings != null && overrideCameraSettings.IsOverridingViewAngle)) //Reset transition timer
				interpolationSettings.viewAngle = new Vector2(_calculationGimbal.Rotation.x, _calculationRoot.Rotation.y);
			
			overrideCameraSettings = data;

			if (useCrossfade) //Crossfade transition
			{
				Image img = _calculationGimbal.GetViewport().GetTexture().GetData();
				var tex = new ImageTexture();
				tex.CreateFromImage(img);
				GameplayInterface.instance.PlayCameraTransition(tex);
			}
		}

		private float transitionTime;
		private void UpdateActiveSettings() //Calculate the active camera settings
		{
			if(overrideCameraSettings == null)
				SetCameraData(defaultCameraSettings, false);

			if(resetFlag || Mathf.IsZeroApprox(overrideCameraSettings.transitionSpeed))
				transitionTime = 1f;
			else
				transitionTime = Mathf.MoveToward(transitionTime, 1f, (1 / overrideCameraSettings.transitionSpeed) * PhysicsManager.physicsDelta);

			activeSettings.distance = Mathf.Lerp(interpolationSettings.distance, overrideCameraSettings.distance, transitionTime);
			activeSettings.height = Mathf.Lerp(interpolationSettings.height, overrideCameraSettings.height, transitionTime);

			activeSettings.heightTrackingStrength = Mathf.Lerp(interpolationSettings.heightTrackingStrength, overrideCameraSettings.heightTrackingStrength, transitionTime);

			activeSettings.viewAngle.x = Mathf.LerpAngle(interpolationSettings.viewAngle.x, Mathf.Deg2Rad(overrideCameraSettings.viewAngle.x), transitionTime);
			activeSettings.viewAngle.y = Mathf.LerpAngle(interpolationSettings.viewAngle.y, Mathf.Deg2Rad(overrideCameraSettings.viewAngle.y), transitionTime);
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

		private bool resetFlag; //Set to true to skip smoothing

		private void UpdateGameplayCamera()
		{
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
		private const float POSITION_SMOOTHING = .6f;
		private const float ROTATION_SMOOTHING = .06f;
		private void UpdateBasePosition()
		{
			UpdateOffsets(); //Height, distance & strafe

			//Update Rotation
			Vector2 targetRotation = activeSettings.viewAngle;
			if (!activeSettings.IsOverridingViewAngle)
			{
				Vector3 forwardDirection = PlayerPathFollower.Forward().Flatten().Normalized();
				if (Mathf.Abs(PlayerPathFollower.Forward().y) > .8f) //Case of running up a wall
					forwardDirection = Mathf.Sign(PlayerPathFollower.Forward().y) * PlayerPathFollower.Down().Flatten().Normalized();
				targetRotation.y = forwardDirection.SignedAngleTo(Vector3.Forward, Vector3.Down) + Mathf.Pi;

				float cachedRotation = _calculationRoot.Rotation.y;
				_calculationRoot.Rotation = Vector3.Up * targetRotation.y; //Temporarily apply the rotation so pitch calculation can be correct
				Vector3 rightDirection = forwardDirection.Cross(Vector3.Down);
				targetRotation.x = _calculationRoot.Forward().SignedAngleTo(PlayerPathFollower.Forward(), rightDirection);

				//Reset rotation
				_calculationRoot.Rotation = Vector3.Up * cachedRotation;
			}

			float steppedTime = Mathf.SmoothStep(0, 1, transitionTime);
			targetRotation = new Vector2(Mathf.LerpAngle(interpolationSettings.viewAngle.x, targetRotation.x, steppedTime),
				Mathf.LerpAngle(interpolationSettings.viewAngle.y, targetRotation.y, steppedTime));

			if(resetFlag)
				currentRotation = targetRotation;
			else
			{
				currentRotation = new Vector2(ExtensionMethods.SmoothDampAngle(currentRotation.x, targetRotation.x, ref rotationVelocity.x, ROTATION_SMOOTHING),
					ExtensionMethods.SmoothDampAngle(currentRotation.y, targetRotation.y, ref rotationVelocity.y, ROTATION_SMOOTHING));
			}

			//Track height
			float heightTracking = localPlayerPosition.y * activeSettings.heightTrackingStrength;

			//Calculate positions
			Vector3 targetPosition = PlayerPathFollower.GlobalTransform.origin;
			if (activeSettings.IsOverridingViewAngle)
				targetPosition = Player.GlobalTransform.origin - Vector3.Up * localPlayerPosition.y;

			Vector3 offsetDirection = _calculationRoot.Back().Rotated(_calculationRoot.Right(), currentRotation.x);
			targetPosition += offsetDirection * currentDistance;
			targetPosition += offsetDirection.Rotated(_calculationRoot.Right(), Mathf.Pi * .5f) * (currentHeight + heightTracking);

			//Update Pitch
			float pitchTracking = 0; // new Vector2(currentDistance, localPlayerPosition.y).AngleTo(Vector2.Right) * (1 - activeSettings.heightTrackingStrength);
			
			//Apply changes
			_calculationRoot.Rotation = Vector3.Up * currentRotation.y;
			_calculationGimbal.Rotation = Vector3.Right * (currentRotation.x + pitchTracking);
			Transform t = _calculationRoot.GlobalTransform;
			t.origin = targetPosition;
			_calculationRoot.GlobalTransform = t;

		}

		private void UpdateOffsets()
		{
			if (resetFlag)
			{
				currentHeight = activeSettings.height;
				currentDistance = activeSettings.distance;
				heightVelocity = distanceVelocity = 0f;
			}
			else
			{
				currentHeight = ExtensionMethods.SmoothDamp(currentHeight, activeSettings.height, ref heightVelocity, POSITION_SMOOTHING);
				currentDistance = ExtensionMethods.SmoothDamp(currentDistance, activeSettings.distance, ref distanceVelocity, POSITION_SMOOTHING);
			}
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
				_cameraRoot.GlobalTranslate(_cameraGimbal.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.Q))
				_cameraRoot.GlobalTranslate(_cameraGimbal.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.W))
				_cameraRoot.GlobalTranslate(_cameraGimbal.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.S))
				_cameraRoot.GlobalTranslate(_cameraGimbal.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.D))
				_cameraRoot.GlobalTranslate(_cameraGimbal.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.A))
				_cameraRoot.GlobalTranslate(_cameraGimbal.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
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
				_cameraGimbal.RotateX(Mathf.Deg2Rad((e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
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
*/