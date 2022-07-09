using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class CameraController : Spatial
	{
		public static CameraController instance;

		[Export]
		public NodePath cameraPathFollower;
		private Path activePath;
		private PathFollow _cameraPathFollower;
		private PathFollow PlayerPathFollower => _player.PathFollower;

		[Export]
		public NodePath gimbal;
		private Spatial _gimbal;
		[Export]
		public NodePath camera;
		private Camera _camera;

		[Export]
		public NodePath player;
		private CharacterController _player;

		[Export]
		public CameraSettingsResource defaultCameraSettings;
		[Export]
		public CameraSettingsResource backstepCameraSettings;
		public CameraSettingsResource overrideCameraSettings; //Override this for more specific camera control

		private CameraSettingsResource activeSettings;

		// Called when the node enters the scene tree for the first time.
		public override void _Ready()
		{
			instance = this;

			_player = GetNode<CharacterController>(player);
			_gimbal = GetNode<Spatial>(gimbal);
			_camera = GetNode<Camera>(camera);
			_cameraPathFollower = GetNode<PathFollow>(cameraPathFollower);
		}

		public override void _PhysicsProcess(float _)
		{
			UpdateGameplayCamera();

			if (!OS.IsDebugBuild())
				return;

			if (Input.IsKeyPressed((int)KeyList.R))
				freeCamEnabled = freeCamRotating = false;

			freeCamRotating = Input.IsMouseButtonPressed((int)ButtonList.Left);
			if (freeCamRotating)
			{
				freeCamEnabled = true;
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;

			UpdateFreeCam();
		}

		#region Gameplay Camera
		[Export]
		public float idleDistance;
		[Export]
		public float idleHeight;

		[Export]
		public float runDistance;
		[Export]
		public float runHeight;
		[Export]
		public float backstepDistance;

		[Export]
		public float strafeSmoothing;

		private const float positionSmoothing = .8f;
		private const float rotationSmoothing = .2f;
		private Vector3 playerLocalPosition;
		private bool resetFlag = true; //Set to true to skip smoothing

		#region Backstep Camera
		private float backstepTimer;
		private bool IsBackStepping => backstepTimer >= BACKSTEP_CAMERA_DELAY;
		private const float BACKSTEP_CAMERA_DELAY = .5f;

		private void UpdateBackstep()
		{
			//Backstep camera
			if (_player.MoveSpeed < 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, BACKSTEP_CAMERA_DELAY, PhysicsManager.physicsDelta);
			else if (backstepTimer < BACKSTEP_CAMERA_DELAY || _player.MoveSpeed > 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, 0f, PhysicsManager.physicsDelta);
		}
		#endregion

		public float GetDistanceFromPath(Vector3 worldPosition)
		{
			Vector2 delta = (worldPosition - _player.ActivePath.Curve.GetClosestPoint(worldPosition)).RemoveVertical();
			float absDst = delta.Length();
			float sign = Mathf.Sign(delta.Dot(_player.StrafeDirection.RemoveVertical()));
			return absDst * sign;
		}

		private void UpdateGameplayCamera()
		{
			UpdateActiveSettings();

			playerLocalPosition = _player.GlobalTransform.origin - PlayerPathFollower.GlobalTransform.origin;
			playerLocalPosition = PlayerPathFollower.GlobalTransform.basis.Inverse().Xform(playerLocalPosition);

			Vector3 targetPosition = _player.CenterPosition;
			Basis targetBasis = GlobalTransform.basis;

			if (!activeSettings.constantOffset.IsEqualApprox(Vector3.Zero))
			{
				Vector3 offset = activeSettings.constantOffset;
				targetPosition += offset;
			}
			else if (activePath != null)
			{
				//Calculate Rotation
				_cameraPathFollower.Offset = PlayerPathFollower.Offset + _player.MoveSpeed * .1f * PhysicsManager.physicsDelta;
				targetBasis = _cameraPathFollower.GlobalTransform.basis;

				//Calculate position
				float offset = _cameraPathFollower.Offset - activeSettings.distance;
				if (offset < 0)
				{
					_cameraPathFollower.VOffset += Mathf.Abs(offset);
					offset = 0;
				}
				_cameraPathFollower.Offset = offset;

				_cameraPathFollower.VOffset = activeSettings.height;
				targetPosition = _cameraPathFollower.GlobalTransform.origin;

				//Track height
				targetPosition += PlayerPathFollower.Up() * playerLocalPosition.y * activeSettings.heightTrackingStrength;
			}

			if (freeCamEnabled) return;

			UpdatePitch();

			Transform t = GlobalTransform;
			if (resetFlag)
			{
				resetFlag = false;
				t.origin = targetPosition;
				t.basis = targetBasis;
			}
			else
			{
				float smoothing = Mathf.Abs(_player.SpeedRatio);
				t.origin = t.origin.LinearInterpolate(targetPosition, smoothing * positionSmoothing);
				t.basis = t.basis.Slerp(targetBasis.Orthonormalized(), smoothing * rotationSmoothing).Orthonormalized();
			}
			GlobalTransform = t;
		}

		private void UpdateActiveSettings()
		{
			//Calculate the active camera settings
			activeSettings = overrideCameraSettings;
			if (activeSettings == null)
			{
				UpdateBackstep();

				if (IsBackStepping)
					activeSettings = backstepCameraSettings;
				else
					activeSettings = defaultCameraSettings;
			}
		}

		private void UpdatePitch()
		{
			if (activeSettings.constantOffset != Vector3.Zero) return;

			float factor = Mathf.Clamp((1 - activeSettings.heightTrackingStrength) * 2f, 0, 1);
			float angle = new Vector2(activeSettings.distance, playerLocalPosition.y).AngleTo(Vector2.Right);
			_gimbal.Rotation = Vector3.Right * angle * factor;
		}

		public void SetActivePath(Path newPath)
		{
			activePath = newPath;

			if (_cameraPathFollower.IsInsideTree())
				_cameraPathFollower.GetParent().RemoveChild(_cameraPathFollower);

			_cameraPathFollower.Loop = activePath.Curve.IsLoopingPath();

			activePath.AddChild(_cameraPathFollower);
			UpdateGameplayCamera();
		}

		public void SetCameraData(CameraSettingsResource data)
		{
			overrideCameraSettings = data;

			if (overrideCameraSettings == null) return; //Camera settings reverted back to default.

			//Crossfade transition
			if (overrideCameraSettings.useCrossfade)
			{
				Image img = _gimbal.GetViewport().GetTexture().GetData();
				var tex = new ImageTexture();
				tex.CreateFromImage(img);
				GameplayInterface.instance.PlayCameraTransition(tex);
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
			if (!freeCamEnabled) return;

			float targetMoveSpeed = freecamMovespeed;

			if (Input.IsKeyPressed((int)KeyList.Shift))
				targetMoveSpeed *= 2;
			else if (Input.IsKeyPressed((int)KeyList.Control))
				targetMoveSpeed *= .5f;

			if (Input.IsKeyPressed((int)KeyList.E))
				GlobalTranslate(_gimbal.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.Q))
				GlobalTranslate(_gimbal.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.W))
				GlobalTranslate(_gimbal.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.S))
				GlobalTranslate(_gimbal.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.D))
				GlobalTranslate(_gimbal.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.A))
				GlobalTranslate(_gimbal.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
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
				RotateY(Mathf.Deg2Rad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY);
				_gimbal.RotateX(Mathf.Deg2Rad((e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
				_gimbal.RotationDegrees = Vector3.Right * Mathf.Clamp(_gimbal.RotationDegrees.x, -90, 90);
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

		public Vector2 ConvertToScreenSpace(Vector3 worldSpace)
		{
			return _camera.UnprojectPosition(worldSpace);
		}
	}
}