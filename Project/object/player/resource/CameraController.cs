using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class CameraController : Spatial
	{
		public static CameraController instance;

		[Export]
		public CameraSettingsResource defaultCameraData;
		private CameraSettingsResource activeData;

		[Export]
		public NodePath camera;
		private Camera _camera;

		[Export]
		public NodePath gimbal;
		private Spatial _gimbal;

		private PathFollow _pathFollower;

		[Export]
		public NodePath player;
		private CharacterController _player;

		// Called when the node enters the scene tree for the first time.
		public override void _Ready()
		{
			instance = this;

			_player = GetNode<CharacterController>(player);
			_pathFollower = _player.PathFollower;
			_camera = GetNode<Camera>(camera);
			_gimbal = GetNode<Spatial>(gimbal);
		}

		public void UpdateCamera()
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

		private float viewOffset;

		private float currentDistance;
		private float currentHeight;

		private const float SCREEN_WIDTH = .4f; //At what point to begin offsetting camera

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

			if (_player.MoveSpeed >= 0)
			{
				float t = Mathf.Clamp(_player.MoveSpeed / _player.runSpeed, 0, 1);
				currentDistance = Mathf.Lerp(idleDistance, runDistance, t);
				currentHeight = Mathf.Lerp(idleHeight, runHeight, t);
			}

			//Calculate target camera position
			if (_player.ActivePath != null)
			{
				//Sideways
				//Rotate camera to face the road / face the player
				Vector3 normalizedVector = _pathFollower.GlobalTransform.Forward().Flatten().Normalized();
				Rotation = Vector3.Up * normalizedVector.RemoveVertical().AngleTo(Vector2.Up);

				/*
				float playerDistance = GetDistanceFromPath(Player.GlobalTransform.origin);
				float delta = playerDistance - viewOffset;
				if (Mathf.Abs(delta) > SCREEN_WIDTH)
				{
					float targetViewOffset = viewOffset + (Mathf.Abs(delta) - SCREEN_WIDTH) * Mathf.Sign(delta);
					targetViewOffset = Mathf.Clamp(targetViewOffset, -roadWidth, roadWidth);
					viewOffset = Mathf.Lerp(viewOffset, targetViewOffset, strafeSmoothing * PhysicsManager.physicsDelta);
				}


				Vector3 targetPosition = Player.ActivePath.Curve.GetClosestPoint(Player.GlobalTransform.origin) + normalizedVector * currentDistance;
				targetPosition += Player.StrafeDirection * viewOffset;
				targetPosition.y = Player.GlobalTransform.origin.y + currentHeight;
				*/

				Transform t = GlobalTransform;
				//t.origin = t.origin.LinearInterpolate(targetPosition, .8f);

				t.origin = _player.GlobalTransform.origin + _pathFollower.Forward() * idleDistance + _player.Up() * idleHeight;
				GlobalTransform = t;
			}
		}

		public void ResetCameraData()
		{
			activeData = defaultCameraData;
		}

		public void SetCameraData(CameraSettingsResource data)
		{
			activeData = data;

			if (activeData.isInstantTransition)
			{
				Image img = _camera.GetViewport().GetTexture().GetData();
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
				GlobalTranslate(_camera.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.Q))
				GlobalTranslate(_camera.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.W))
				GlobalTranslate(_camera.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.S))
				GlobalTranslate(_camera.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.D))
				GlobalTranslate(_camera.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed((int)KeyList.A))
				GlobalTranslate(_camera.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
		}

		public override void _Input(InputEvent e)
		{
			if (!freeCamRotating) return;

			if (e is InputEventMouseMotion)
			{
				_gimbal.RotateY(Mathf.Deg2Rad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY);
				_camera.RotateX(Mathf.Deg2Rad(-(e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
				_camera.RotationDegrees = Vector3.Right * Mathf.Clamp(_camera.RotationDegrees.x, -90, 90);
			}
			else if (e is InputEventMouseButton)
			{
				InputEventMouseButton emb = (InputEventMouseButton)e;
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
		}
		#endregion
	}
}