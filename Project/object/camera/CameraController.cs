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
		private Node3D calculationRoot; //Responsible for pitch rotation
		[Export]
		private Node3D calculationGimbal; //Responsible for yaw rotation
		[Export]
		private Node3D cameraRoot;
		[Export]
		private Node3D cameraGimbal;
		[Export]
		private Camera3D camera;
		[Export]
		private Camera3D bloomCamera;

		[Export]
		private TextureRect _crossfade;
		[Export]
		private AnimationPlayer _crossfadeAnimator;

		private CharacterController Character => CharacterController.instance;
		private CharacterPathFollower PathFollower => Character.PathFollower;

		public Transform3D CameraTransform => camera.GlobalTransform;
		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => camera.UnprojectPosition(worldSpace);
		public bool IsOnScreen(Vector3 worldSpace) => camera.IsPositionInFrustum(worldSpace);
		public bool IsPositionBehind(Vector3 worldSpace) => camera.IsPositionBehind(worldSpace);
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

			if (OS.IsDebugBuild())
				UpdateFreeCam();

			bloomCamera.GlobalTransform = camera.GlobalTransform;
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
				cameraRoot.GlobalTransform = calculationRoot.GlobalTransform;
				cameraGimbal.GlobalTransform = calculationGimbal.GlobalTransform;
			}

			if (ResetFlag) //Reset flag
				ResetFlag = false;
		}

		private void UpdateBasePosition()
		{
			UpdateRotation();

			//TODO Check for floors, walls, etc.
			calculationRoot.GlobalPosition = GetTargetPosition();
		}

		public float CurrentPitch { get; private set; }
		public float CurrentYaw { get; private set; }
		private float currentYawVelocity;
		private readonly float BASE_YAW_SMOOTHING = .1f;
		private void UpdateRotation()
		{
			UpdateYaw();
			UpdatePitch();
			UpdateTilt();

			calculationRoot.GlobalRotation = Vector3.Up * CurrentYaw;
			calculationGimbal.Rotation = Vector3.Right * CurrentPitch;
			xformAngle = Vector2.Down.Rotated(-CurrentYaw).AngleTo(Vector2.Down);
		}

		private bool isYawOverrideActive;
		private float targetYawOverride; //In Radians
		/// <summary>
		/// Use this for stage objects that want to override yaw without modifying the other camera settings.
		/// Must be called every frame.
		/// </summary>
		public void OverrideYaw(float angle, float smoothing = 0)
		{
			isYawOverrideActive = true;

			if (Mathf.IsZeroApprox(smoothing))
				targetYawOverride = angle;
			else
				targetYawOverride = Mathf.LerpAngle(CurrentYaw, angle, smoothing);
		}

		private void UpdateYaw() //Calculate horizontal rotation (yaw)
		{
			float targetYaw;
			if (isYawOverrideActive)
			{
				targetYaw = targetYawOverride;
				isYawOverrideActive = false;
			}
			else if (targetSettings.yawMode == CameraSettingsResource.OverrideMode.Override)
				targetYaw = Mathf.DegToRad(targetSettings.viewAngle.y); //Override view direction
			else
			{
				//Forward direction is based on PathFollower's orientation
				targetYaw = PathFollower.ForwardAngle;
				targetYaw += Mathf.DegToRad(targetSettings.viewAngle.y); //Add
			}

			if (ResetFlag)
			{
				CurrentYaw = targetYaw;
				currentYawVelocity = 0f;
			}
			else
				CurrentYaw = ExtensionMethods.SmoothDampAngle(CurrentYaw, targetYaw, ref currentYawVelocity, BASE_YAW_SMOOTHING);
		}

		private void UpdatePitch()
		{
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
			targetPosition += calculationRoot.Back() * targetSettings.distance;
			targetPosition += calculationRoot.Up() * targetSettings.height;
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
				calculationRoot.Visible = false;
				ResetFlag = true;
			}

			freeCamRotating = Input.IsMouseButtonPressed(MouseButton.Left);
			if (freeCamRotating)
			{
				freeCamEnabled = true;
				calculationRoot.Visible = true;
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
				cameraRoot.GlobalTranslate(camera.Up() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.Q))
				cameraRoot.GlobalTranslate(camera.Down() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.W))
				cameraRoot.GlobalTranslate(camera.Back() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.S))
				cameraRoot.GlobalTranslate(camera.Forward() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.D))
				cameraRoot.GlobalTranslate(camera.Right() * targetMoveSpeed * PhysicsManager.physicsDelta);
			if (Input.IsKeyPressed(Key.A))
				cameraRoot.GlobalTranslate(camera.Left() * targetMoveSpeed * PhysicsManager.physicsDelta);
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
				cameraRoot.RotateY(Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY);
				cameraGimbal.RotateX(Mathf.DegToRad((e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY);
				cameraGimbal.Rotation = Vector3.Right * Mathf.Clamp(cameraGimbal.Rotation.x, -Mathf.Pi * .5f, Mathf.Pi * .5f);
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