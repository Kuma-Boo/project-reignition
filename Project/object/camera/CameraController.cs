using Godot;
using Project.Core;

namespace Project.Gameplay
{

	/// <summary>
	/// Follows the player based on the settings provided from CameraSettingsResource.cs
	/// </summary>
	//Undergoing complete rewrite...Again.
	public partial class CameraController : Node3D
	{
		public static CameraController instance;

		[Export]
		private Node3D _calculationRoot; //Responsible for pitch rotation
		[Export]
		private Node3D _calculationGimbal; //Responsible for yaw rotation
		[Export]
		private Node3D _cameraRoot;
		[Export]
		private Node3D _cameraGimbal;
		[Export]
		private Camera3D _camera;

		[Export]
		private TextureRect _crossfade;
		[Export]
		private AnimationPlayer _crossfadeAnimator;

		private CharacterController Character => CharacterController.instance;
		private CharacterPathFollower PathFollower => Character.PathFollower;

		public Transform3D CameraTransform => _camera.GlobalTransform;
		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => _camera.UnprojectPosition(worldSpace);
		public bool IsOnScreen(Vector3 worldSpace) => _camera.IsPositionInFrustum(worldSpace);
		public bool IsPositionBehind(Vector3 worldSpace) => _camera.IsPositionBehind(worldSpace);
		public float CurrentYaw { get; private set; }
		public float CurrentPitch { get; private set; }
		/// <summary> Angle to use when transforming from world space to camera space </summary>
		private float xformAngle;
		public float TransformAngle(float angle) => xformAngle + angle;

		public override void _Ready()
		{
			instance = this;
			ResetFlag = true; //Default to snapping view when spawning
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
		public CameraSettingsResource targetSettings;
		public void SetCameraData(CameraSettingsResource data, float blendTime = .2f, bool useCrossfade = false)
		{
			targetSettings = data;

			if (useCrossfade) //Crossfade transition
			{
				StartCrossfade();
				ResetFlag = true;
			}
		}

		public void StartCrossfade()
		{
			ImageTexture tex = new ImageTexture(); //Render the viewport
			tex.SetImage(GetViewport().GetTexture().GetImage());
			_crossfade.Texture = tex;
			_crossfadeAnimator.Play("activate"); //Play crossfade animation
		}

		private float transitionTime; //Ratio (from 0 -> 1) of transition that has been completed
		private float transitionTimeStepped; //Smoothstepped transition time
		private float transitionSpeed; //Speed of transition
		private void UpdateActiveSettings() //Calculate the active camera settings
		{
			if (targetSettings == null) return; //ERROR! No data set.

			if (Mathf.IsZeroApprox(transitionSpeed))
				ResetFlag = true;

			if (ResetFlag)
				transitionTime = 1f;
			else
				transitionTime = Mathf.MoveToward(transitionTime, 1f, (1f / transitionSpeed) * PhysicsManager.physicsDelta);
			transitionTimeStepped = Mathf.SmoothStep(0, 1f, transitionTime);
		}
		#endregion

		#region Gameplay Camera
		public bool ResetFlag { get; set; } //Set to true to skip smoothing

		private void UpdateGameplayCamera()
		{
			UpdateActiveSettings();
			UpdateBasePosition();

			if (!freeCamEnabled) //Apply transform
			{
				_cameraRoot.GlobalTransform = _calculationRoot.GlobalTransform;
				_cameraGimbal.GlobalTransform = _calculationGimbal.GlobalTransform;
			}

			if (ResetFlag) //Reset flag
				ResetFlag = false;
		}

		private void UpdateBasePosition()
		{
			UpdateRotation();

			//TODO Check for floors, walls, etc.
			_calculationRoot.GlobalPosition = GetTargetPosition();
		}

		private void UpdateRotation()
		{
			UpdateYaw();
			UpdateTilt();

			_calculationRoot.GlobalRotation = Vector3.Up * CurrentYaw;
			_calculationGimbal.Rotation = Vector3.Right * CurrentPitch;
			xformAngle = Vector2.Down.Rotated(-CurrentYaw).AngleTo(Vector2.Down);
		}

		private void UpdateYaw() //Calculate horizontal rotation (yaw)
		{
			if (targetSettings.yawMode == CameraSettingsResource.OverrideMode.Override)
				CurrentYaw = Mathf.DegToRad(targetSettings.viewAngle.y); //Override view direction
			else
			{
				if (Mathf.Abs(PathFollower.Back().Dot(Vector3.Up)) > .9f) //Moving vertically, can't use PathFollower.Forward
				{
					if (Character.IsOnGround) //Use ground direction as "forward"
						CurrentYaw = Character.UpDirection.SignedAngleTo(Vector3.Forward, Vector3.Up);
					else //NEEDS TESTING - Maintain the current view angle?
						return;
				}
				else //Forward direction is based on PathFollower's orientation
				{
					//Using a flattened vector because 3d vectors cause issues when traveling down slopes
					CurrentYaw = PathFollower.Forward().Flatten().Normalized().AngleTo(Vector2.Down);
				}

				CurrentYaw += Mathf.DegToRad(targetSettings.viewAngle.y); //Add
			}


			if (targetSettings.pitchMode == CameraSettingsResource.OverrideMode.Override)
				CurrentPitch = Mathf.DegToRad(targetSettings.viewAngle.x); //Override view direction
			else
			{
				CurrentPitch = 0; //TODO Calculate pitch
				CurrentPitch += Mathf.DegToRad(targetSettings.viewAngle.x); //Add
			}
		}

		private void UpdateTilt()
		{
			if (targetSettings.enableZTilting) //Rotate the z axis along PathFollower's forward, by angle of worldDirection to up
			{
				float targetTiltAmount = Character.UpDirection.SignedAngleTo(Vector3.Up, PathFollower.Back());
			}
		}

		private Vector3 GetTargetPosition()
		{
			if (targetSettings.isStaticCamera) //Static camera, move to view position
				return targetSettings.staticPosition;

			Vector3 targetPosition = Character.CenterPosition;
			targetPosition += _calculationRoot.Back() * targetSettings.distance;
			targetPosition += _calculationRoot.Up() * targetSettings.height;
			return targetPosition;
		}
		#endregion

		#region Free Cam
		private float freecamMovespeed = 20;
		private const float MOUSE_SENSITIVITY = .2f;

		private bool freeCamEnabled;
		private bool freeCamRotating;

		private void UpdateFreeCam()
		{
			if (Input.IsKeyPressed(Key.R))
			{
				freeCamEnabled = freeCamRotating = false;
				_calculationRoot.Visible = false;
				ResetFlag = true;
			}

			freeCamRotating = Input.IsMouseButtonPressed(MouseButton.Left);
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

			if (Input.IsKeyPressed(Key.Shift))
				targetMoveSpeed *= 2;
			else if (Input.IsKeyPressed(Key.Ctrl))
				targetMoveSpeed *= .5f;

			if (Input.IsKeyPressed(Key.E))
				_cameraRoot.GlobalTranslate(_camera.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.Q))
				_cameraRoot.GlobalTranslate(_camera.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.W))
				_cameraRoot.GlobalTranslate(_camera.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.S))
				_cameraRoot.GlobalTranslate(_camera.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.D))
				_cameraRoot.GlobalTranslate(_camera.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.A))
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
				_cameraRoot.RotateY(Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY);
				_cameraGimbal.RotateX(Mathf.DegToRad((e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
				_cameraGimbal.Rotation = Vector3.Right * Mathf.Clamp(_cameraGimbal.Rotation.x, -Mathf.Pi * .5f, Mathf.Pi * .5f);
			}
			else if (e is InputEventMouseButton emb)
			{
				if (emb.IsPressed())
				{
					if (emb.ButtonIndex == MouseButton.WheelUp)
					{
						freecamMovespeed += 5;
						GD.Print($"Free cam Speed set to {freecamMovespeed}.");
					}
					if (emb.ButtonIndex == MouseButton.WheelDown)
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