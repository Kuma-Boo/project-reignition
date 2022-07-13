using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class CameraController : Spatial
	{
		public static CameraController instance;
		[Export]
		public NodePath cameraPathFollower;
		private PathFollow CameraPathFollower;
		private PathFollow PlayerPathFollower => _character.PathFollower;
		private Path activePath;
		public void SetActivePath(Path newPath)
		{
			if (activePath == null)
				resetFlag = true;

			activePath = newPath;

			if (CameraPathFollower.IsInsideTree())
				CameraPathFollower.GetParent().RemoveChild(CameraPathFollower);

			CameraPathFollower.Loop = activePath.Curve.IsLoopingPath();
			activePath.AddChild(CameraPathFollower);
		}

		[Export]
		public NodePath gimbal;
		private Spatial _gimbal; //Responsible for pitch rotation
		[Export]
		public NodePath camera;
		private Camera _camera; //Responsible for yaw rotation

		[Export]
		public NodePath character;
		private CharacterController _character;

		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => _camera.UnprojectPosition(worldSpace);

		public override void _Ready()
		{
			instance = this;

			_character = GetNode<CharacterController>(character);
			_gimbal = GetNode<Spatial>(gimbal);
			_camera = GetNode<Camera>(camera);
			CameraPathFollower = GetNode<PathFollow>(cameraPathFollower);
		}

		public void UpdateCamera()
		{
			if (activePath == null)
				return;

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
		private CameraSettingsResource activeSettings;

		public void SetCameraData(CameraSettingsResource data, bool useCrossfade)
		{
			CameraSettingsResource previousData = overrideCameraSettings;
			overrideCameraSettings = data;

			if (overrideCameraSettings != null) //Apply
			{
				if (overrideCameraSettings.IsOverridingPosition)
					positionOffset = CameraPathFollower.GlobalTransform.origin - PlayerPathFollower.GlobalTransform.origin;
			}

			if (previousData != null) //Revert
			{
				if (previousData.IsOverridingPosition)
					positionOffset += PlayerPathFollower.GlobalTransform.origin - CameraPathFollower.GlobalTransform.origin;
			}

			if (useCrossfade) //Crossfade transition
			{
				Image img = _gimbal.GetViewport().GetTexture().GetData();
				var tex = new ImageTexture();
				tex.CreateFromImage(img);
				GameplayInterface.instance.PlayCameraTransition(tex);
			}
		}

		private void UpdateActiveSettings() //Calculate the active camera settings
		{
			activeSettings = overrideCameraSettings;
			if (activeSettings == null)
			{
				//UpdateBackstep();

				if (IsBackStepping)
					activeSettings = backstepCameraSettings;
				else
					activeSettings = defaultCameraSettings;
			}
		}
		#endregion

		#region Backstep Camera
		private float backstepTimer;
		private bool IsBackStepping => backstepTimer >= BACKSTEP_CAMERA_DELAY;
		private const float BACKSTEP_CAMERA_DELAY = .5f;

		private void UpdateBackstep()
		{
			//Backstep camera
			if (_character.MoveSpeed < 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, BACKSTEP_CAMERA_DELAY, PhysicsManager.physicsDelta);
			else if (backstepTimer < BACKSTEP_CAMERA_DELAY || _character.MoveSpeed > 0)
				backstepTimer = Mathf.MoveToward(backstepTimer, 0f, PhysicsManager.physicsDelta);
		}
		#endregion

		#region Gameplay Camera
		private Vector3 localPlayerPosition; //Player's local position, relative to it's path follower
		private void CalculateLocalPlayerPosition()
		{
			localPlayerPosition = _character.GlobalTransform.origin - PlayerPathFollower.GlobalTransform.origin;
			localPlayerPosition = PlayerPathFollower.GlobalTransform.basis.XformInv(localPlayerPosition);
		}

		private bool resetFlag; //Set to true to skip smoothing
		private const float POSITION_SMOOTHING = .6f;
		private const float ROTATION_SMOOTHING = .2f;
		private const float OFFSET_SMOOTHING = .25f;

		private void UpdateGameplayCamera()
		{
			if (freeCamEnabled) return;

			CalculateLocalPlayerPosition();
			UpdateActiveSettings();
			ResyncPathFollower();
			UpdateBasePosition();
			UpdateStrafe();
			UpdateHeight();
			UpdatePitch();

			if (resetFlag) //Reset flag
				resetFlag = false;
		}

		private Vector3 positionOffset;
		private Vector3 positionVelocity;
		private float currentDistance;
		private void UpdateBasePosition() //Align view to path at floor level
		{
			Transform t = GlobalTransform;
			Vector3 targetPosition = CameraPathFollower.GlobalTransform.origin;
			Basis targetBasis = GlobalTransform.basis;

			if (activeSettings.positionOffset.IsEqualApprox(Vector3.Zero))
			{
				targetBasis = CameraPathFollower.GlobalTransform.basis;
				CameraPathFollower.VOffset = activeSettings.height;
				currentDistance = Mathf.Lerp(currentDistance, activeSettings.distance, POSITION_SMOOTHING);

				positionOffset = positionOffset.SmoothDamp(Vector3.Zero, ref positionVelocity, OFFSET_SMOOTHING); //Reset position offset
			}
			else
			{
				targetPosition = PlayerPathFollower.GlobalTransform.origin;
				positionOffset = positionOffset.SmoothDamp(activeSettings.positionOffset, ref positionVelocity, OFFSET_SMOOTHING);
			}

			targetPosition += positionOffset;

			if (resetFlag) //Snap values
			{
				t.origin = targetPosition;
				t.basis = targetBasis.Orthonormalized();
			}
			else
			{
				t.origin = t.origin.LinearInterpolate(targetPosition, POSITION_SMOOTHING);
				t.basis = t.basis.Slerp(targetBasis.Orthonormalized(), ROTATION_SMOOTHING).Orthonormalized();
			}
			GlobalTransform = t;
		}

		private void ResyncPathFollower()
		{
			float offset = PlayerPathFollower.Offset + _character.MoveSpeed * .1f * PhysicsManager.physicsDelta - currentDistance;
			if (offset < 0)
			{
				CameraPathFollower.VOffset += Mathf.Abs(offset);
				offset = 0;
			}
			CameraPathFollower.Offset = offset;
		}

		public bool IsRecenteringStrafe { get; set; }
		private float currentStrafe;
		private float strafeVelocity;
		private const float STRAFE_SMOOTHING = .2f;
		private const float STRAFE_RESET_SPEED = 14f;
		private const float STRAFE_SMOOTHING_POINT = 1.2f;
		private const float STRAFE_SNAP_DISTANCE = 1.5f;
		private const float STRAFE_LEAD_AMOUNT = 1.5f;
		private void UpdateStrafe()
		{
			Transform t = _gimbal.Transform;

			float delta = localPlayerPosition.x - currentStrafe;
			float sign = Mathf.Sign(delta);
			delta = Mathf.Abs(delta);

			if (_character.IsGrindStepping)
			{
				//Make switching rails easier
				currentStrafe = ExtensionMethods.SmoothDamp(currentStrafe, currentStrafe + delta * sign, ref strafeVelocity, STRAFE_SMOOTHING);
			}
			else
			{
				if (activeSettings.IsOverridingPosition || IsRecenteringStrafe)
					currentStrafe = Mathf.MoveToward(currentStrafe, 0, STRAFE_RESET_SPEED * _character.SpeedRatio * PhysicsManager.physicsDelta);

				if (delta > STRAFE_SMOOTHING_POINT)
				{
					if (delta > STRAFE_SNAP_DISTANCE)
						currentStrafe = ExtensionMethods.SmoothDamp(currentStrafe, currentStrafe + (delta - STRAFE_SNAP_DISTANCE) * sign, ref strafeVelocity, 0f);
					else
						currentStrafe = ExtensionMethods.SmoothDamp(currentStrafe, currentStrafe - STRAFE_LEAD_AMOUNT * _character.strafeSettings.GetSpeedRatio(_character.StrafeSpeed), ref strafeVelocity, STRAFE_SMOOTHING);
				}
			}

			t.origin.x = currentStrafe;
			_gimbal.Transform = t;
		}

		private void UpdateHeight()
		{
			//Track height
			Transform t = _gimbal.Transform;
			t.origin.y = localPlayerPosition.y * activeSettings.heightTrackingStrength;
			_gimbal.Transform = t;
		}

		private float pitchVelocity;
		private const float PITCH_SMOOTHING = .05f;
		private void UpdatePitch()
		{
			if (activeSettings.positionOffset != Vector3.Zero) return;

			float factor = Mathf.Clamp((1 - activeSettings.heightTrackingStrength) * 2f, 0, 1);
			float targetAngle = new Vector2(activeSettings.distance, localPlayerPosition.y + _character.VerticalSpeed * PhysicsManager.physicsDelta).AngleTo(Vector2.Right);
			float angle = ExtensionMethods.SmoothDamp(_gimbal.Rotation.x, targetAngle, ref pitchVelocity, PITCH_SMOOTHING);
			_gimbal.Rotation = Vector3.Right * angle * factor;
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
				resetFlag = true;
			}

			freeCamRotating = Input.IsMouseButtonPressed((int)ButtonList.Left);
			if (freeCamRotating)
			{
				freeCamEnabled = true;
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
	}
}