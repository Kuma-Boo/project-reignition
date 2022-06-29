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
				Input.SetMouseMode(Input.MouseMode.Captured);
			}
			else
				Input.SetMouseMode(Input.MouseMode.Visible);

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
			if (freeCamEnabled) return;

			//Calculate the active camera settings
			CameraSettingsResource activeSettings = overrideCameraSettings;
			if (activeSettings == null)
			{
				UpdateBackstep();

				if (IsBackStepping)
					activeSettings = backstepCameraSettings;
				else
					activeSettings = defaultCameraSettings;
			}

			Vector3 playerLocalPosition = _player.GlobalTransform.origin - PlayerPathFollower.GlobalTransform.origin;
			playerLocalPosition = playerLocalPosition.AlignVectorToTransform(PlayerPathFollower.GlobalTransform);

			Vector3 targetPosition = _player.CenterPosition;
			Basis targetBasis = GlobalTransform.basis;

			if (!activeSettings.constantOffset.IsEqualApprox(Vector3.Zero))
			{
				Vector3 offset = activeSettings.constantOffset;
				targetPosition += offset;
			}
			else if(activePath != null)
			{
				//Resync position
				float offset = activePath.Curve.GetClosestOffset(PlayerPathFollower.GlobalTransform.origin - activePath.GlobalTransform.origin) - activeSettings.distance;
				_cameraPathFollower.VOffset = activeSettings.height;

				if (offset < 0)
				{
					_cameraPathFollower.VOffset += Mathf.Abs(offset);
					offset = 0;
				}
				_cameraPathFollower.Offset = offset;

				targetPosition = _cameraPathFollower.GlobalTransform.origin;
				targetBasis = _cameraPathFollower.GlobalTransform.basis;

				//Track height
				targetPosition += PlayerPathFollower.Up() * playerLocalPosition.y * activeSettings.heightTrackingStrength;
			}

			Transform t = GlobalTransform;
			t.origin = targetPosition;
			t.basis = targetBasis;
			GlobalTransform = t;

			/*
			//TODO Look at player. Currently broken.
			Vector3 playerToCameraLocalPosition = _player.CenterPosition - GlobalTransform.origin;
			playerToCameraLocalPosition = playerToCameraLocalPosition.AlignVectorToTransform(_cameraPathFollower.GlobalTransform);

			Vector2 playerDirection = new Vector2(playerToCameraLocalPosition.y, playerToCameraLocalPosition.z).Normalized();
			_gimbal.Rotation = Vector3.Right * playerDirection.AngleTo(Vector2.Up);

			if(_camera.GlobalTransform.origin != _player.CenterPosition)
				_camera.LookAt(_player.CenterPosition, Vector3.Up);
			*/
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