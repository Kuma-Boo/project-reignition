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

		[ExportSubgroup("Gameplay Camera")]
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
		public Camera3D Camera => camera;
		[Export]
		private RayCast3D backstepCheck;

		[Export]
		private TextureRect _crossfade;
		[Export]
		private AnimationPlayer _crossfadeAnimator;

		private CharacterController Character => CharacterController.instance;
		private CharacterPathFollower PathFollower => Character.PathFollower;

		public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => camera.UnprojectPosition(worldSpace);
		public bool IsOnScreen(Vector3 worldSpace) => camera.IsPositionInFrustum(worldSpace);
		public bool IsBehindCamera(Vector3 worldSpace) => camera.IsPositionBehind(worldSpace);
		/// <summary> Angle to use when transforming from world space to camera space </summary>
		private float xformAngle;
		public float TransformAngle(float angle) => xformAngle + angle;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			instance = this;
			SnapFlag = true; //Default to snapping view when spawning
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateGameplayCamera();

			if (OS.IsDebugBuild())
				UpdateFreeCam();
		}

		#region Gameplay Camera
		#region Transitions and Settings
		[Export]
		public CameraSettingsResource defaultSettings; //Default settings to use when nothing is set
		public CameraSettingsResource BlendToSettings { get; private set; } //Settings to transition to
		public CameraSettingsResource BlendFromSettings { get; private set; } //Settings to transition from

		public void StartCrossfade()
		{
			ImageTexture tex = new ImageTexture(); //Render the viewport
			tex.SetImage(GetViewport().GetTexture().GetImage());
			_crossfade.Texture = tex;
			_crossfadeAnimator.Play("activate"); //Play crossfade animation
		}

		/// <summary>
		/// Change the current camera settings.
		/// </summary>
		public void UpdateCameraSettings(CameraSettingsResource data, float blendTime = .2f, bool useCrossfade = false)
		{
			BlendFromSettings = BlendToSettings;
			BlendToSettings = data;
			transitionSpeed = blendTime;
			transitionLinearRatio = transitionSmoothedRatio = 0f;

			if (Mathf.IsZeroApprox(transitionSpeed)) //Cut transition
				SnapFlag = true;
			else if (useCrossfade) //Crossfade transition
			{
				StartCrossfade();
				SnapFlag = true;
			}
		}

		/// <summary> Set to true to skip smoothing. </summary>
		public bool SnapFlag { get; set; }
		/// <summary> Ratio [0 <-> 1] of transition that has been completed. </summary>
		private float transitionLinearRatio;
		/// <summary> Smoothstep transition time. </summary>
		private float transitionSmoothedRatio;
		/// <summary> Speed of transition (in seconds). </summary>
		private float transitionSpeed;
		/// <summary>
		/// Update the transition timer.
		/// </summary>
		private void UpdateTransitionTimer()
		{
			if (BlendToSettings == null) //No data set, cut to default camera settings
				UpdateCameraSettings(defaultSettings, 0f);

			if (!Mathf.IsEqualApprox(transitionLinearRatio, 1.0f)) //Update transition blend
			{
				if (SnapFlag)
					transitionLinearRatio = 1f;
				else
					transitionLinearRatio = Mathf.MoveToward(transitionLinearRatio, 1f, (1f / transitionSpeed) * PhysicsManager.physicsDelta);

				transitionSmoothedRatio = Mathf.SmoothStep(0, 1f, transitionLinearRatio);
			}
		}

		/// <summary> Is the player behind the camera? </summary>
		private bool isPlayerBehindCamera;
		/// <summary> Player's position on screen, normalized by screen size. </summary>
		private Vector2 playerPosition;
		/// <summary> 0 -> Don't use backstep, 1 -> Use backstep. </summary>
		private float backstepBlend;
		private float backstepBlendVelocity;
		/// <summary> Doesn't update when the Character isn't moving. </summary>
		private bool isBackstepActive;
		private readonly float BACKSTEP_TRANSITION_SPEED = .4f;
		private void CalculatePlayerPosition()
		{
			isPlayerBehindCamera = IsBehindCamera(Character.GlobalPosition);

			//Calculate player's screen position
			playerPosition = ConvertToScreenSpace(Character.GlobalPosition);
			playerPosition /= RuntimeConstants.SCREEN_SIZE;
			playerPosition = (playerPosition - Vector2.One * .5f) * 2f;

			//Update backstep
			if (Character.MoveSpeed != 0)
			{
				if (Character.IsMovingBackward)
					isBackstepActive = true;
				else if (Character.IsHoldingDirection(PathFollower.ForwardAngle))
					isBackstepActive = false;
			}

			if (SnapFlag)
			{
				backstepBlend = isBackstepActive ? 1 : 0;
				backstepBlendVelocity = 0;
			}
			else
				backstepBlend = ExtensionMethods.SmoothDamp(backstepBlend, isBackstepActive ? 1 : 0, ref backstepBlendVelocity, BACKSTEP_TRANSITION_SPEED);
		}
		#endregion

		private void UpdateGameplayCamera()
		{
			UpdateTransitionTimer();
			CalculatePlayerPosition();

			UpdatePosition();
			UpdateRotation();
			UpdateTrackingPosition();
			UpdateTrackingRotation();

			//Update FOV and view offsets
			Camera.Fov = Mathf.Lerp(CalculateTargetFOV(BlendFromSettings), CalculateTargetFOV(BlendFromSettings), transitionSmoothedRatio);
			Vector2 viewOffset = CalculateViewOffset(BlendFromSettings).Lerp(CalculateViewOffset(BlendToSettings), transitionSmoothedRatio);
			Camera.HOffset = viewOffset.x;
			Camera.VOffset = viewOffset.y;

			if (!freeCamEnabled) //Apply transform
			{
				cameraRoot.GlobalTransform = calculationRoot.GlobalTransform;
				cameraGimbal.GlobalTransform = calculationGimbal.GlobalTransform;
			}

			//Update input transformation angle
			xformAngle = Mathf.Lerp(CalculateXform(BlendFromSettings), CalculateXform(BlendToSettings), transitionSmoothedRatio);

			if (SnapFlag) //Reset flag
				SnapFlag = false;
		}

		private float CalculateXform(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			if (settings.isStaticCamera)
				return Vector2.Down.Rotated(-calculationRoot.Rotation.y).AngleTo(Vector2.Down);

			return Vector2.Down.Rotated(-CurrentYaw).AngleTo(Vector2.Down);
		}

		private float CalculateTargetFOV(CameraSettingsResource settings)
		{
			if (settings == null || !settings.modifyFOV) return Camera.Fov;
			return settings.fov;
		}
		private Vector2 CalculateViewOffset(CameraSettingsResource settings)
		{
			if (settings == null) return Vector2.Zero;
			return new Vector2(settings.hOffset, settings.vOffset);
		}

		private void UpdatePosition()
		{
			Vector3 targetPosition = CalculatePosition(BlendFromSettings).Lerp(CalculatePosition(BlendToSettings), transitionSmoothedRatio);
			calculationRoot.GlobalPosition = targetPosition;
		}

		/// <summary>
		/// Calculates the position for a given CameraSettingResource.
		/// </summary>
		private Vector3 CalculatePosition(CameraSettingsResource settings)
		{
			if (settings == null) return Vector3.Zero;
			if (settings.isStaticCamera) //Static camera
				return settings.staticPosition;

			Vector3 targetPosition = PathFollower.GlobalPosition;

			//Add Distance
			float distance = settings.distance;
			distance += Mathf.Lerp(0, settings.backstepDistanceAddition, backstepBlend);

			if (settings.yawMode != CameraSettingsResource.OverrideMode.Override)
				targetPosition += PathFollower.Back() * distance;
			else
				targetPosition += Vector3.Forward.Rotated(Vector3.Up, settings.yawAngle) * distance;

			//Add Height
			if (settings.isRollEnabled)
				targetPosition += PathFollower.Up() * settings.height;
			else
				targetPosition += PathFollower.UpAxis * settings.height;

			return targetPosition;
		}

		public float CurrentPitch { get; private set; }
		public float CurrentYaw { get; private set; }
		public float CurrentTilt { get; private set; }
		private float currentPitchVelocity;
		private float currentYawVelocity;
		private float currentTiltVelocity;
		private readonly float BASE_ROTATION_SMOOTHING = .1f;
		private void UpdateRotation()
		{
			//Calculate target angles
			float targetTilt = Mathf.Lerp(CalculateTilt(BlendFromSettings), CalculateTilt(BlendToSettings), transitionSmoothedRatio);
			float targetYaw = Mathf.LerpAngle(CalculateYaw(BlendFromSettings), CalculateYaw(BlendToSettings), transitionSmoothedRatio);
			if (isYawOverrideActive) //Yaw override (for Grindrails, needs to be deprecated)
			{
				targetYaw = targetYawOverride;
				isYawOverrideActive = false;
			}

			if (SnapFlag)
			{
				CurrentTilt = targetTilt;
				CurrentYaw = targetYaw;
				currentYawVelocity = currentTiltVelocity = 0f;
			}
			else
			{
				CurrentTilt = ExtensionMethods.SmoothDampAngle(CurrentTilt, targetTilt, ref currentTiltVelocity, BASE_ROTATION_SMOOTHING);
				CurrentYaw = ExtensionMethods.SmoothDampAngle(CurrentYaw, targetYaw, ref currentYawVelocity, BASE_ROTATION_SMOOTHING);
			}

			calculationRoot.GlobalRotation = new Vector3(0, CurrentYaw, CurrentTilt);

			//Pitch has to be calculated after yaw and tilt rotation was applied
			float targetPitch = Mathf.Lerp(CalculatePitch(BlendFromSettings), CalculatePitch(BlendToSettings), transitionSmoothedRatio);
			CurrentPitch = targetPitch;
			/*
			if (SnapFlag)
			{
				currentPitchVelocity = 0;
			}
			else
				CurrentPitch = ExtensionMethods.SmoothDamp(CurrentPitch, targetPitch, ref currentPitchVelocity, BASE_ROTATION_SMOOTHING);
			*/

			calculationGimbal.Rotation = Vector3.Right * CurrentPitch;
		}

		private float CalculateTilt(CameraSettingsResource settings)
		{
			if (settings == null || !settings.isRollEnabled || settings.isStaticCamera) return 0;

			//Rotate the z axis along PathFollower's forward, by angle of the ground direction
			Vector3 angle = PathFollower.Up().Rotated(Vector3.Up, -PathFollower.ForwardAngle);
			angle.z = 0;

			return angle.Normalized().SignedAngleTo(Vector3.Up, Vector3.Forward);
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

		/// <summary>
		/// Calculates yaw (y-axis rotation) of a given CameraSettingsResource.
		/// </summary>
		private float CalculateYaw(CameraSettingsResource settings)
		{
			if (settings == null || settings.isStaticCamera) return 0; //Invalid resource

			float targetYaw;
			if (settings.yawMode == CameraSettingsResource.OverrideMode.Override)
				targetYaw = settings.yawAngle; //Override view direction
			else
			{
				//Forward direction is based on PathFollower's orientation
				targetYaw = PathFollower.ForwardAngle;
				targetYaw += settings.yawAngle; //Add
			}

			return targetYaw;
		}

		/// <summary>
		/// Calculates pitch (x-axis rotation) of a given CameraSettingsResource.
		/// </summary>
		private float CalculatePitch(CameraSettingsResource settings)
		{
			if (settings == null || settings.isStaticCamera) return 0;

			if (settings.pitchMode == CameraSettingsResource.OverrideMode.Override)
				return settings.pitchAngle; //Override view direction

			Vector3 targetLookAtPosition = PathFollower.GlobalPosition;

			if (settings.isRollEnabled)
				targetLookAtPosition += PathFollower.Up() * settings.height;
			else
				targetLookAtPosition += PathFollower.UpAxis * settings.height;

			Vector3 delta = calculationRoot.GlobalPosition - targetLookAtPosition;
			delta = calculationRoot.Basis.Inverse() * delta;
			delta.x = 0;
			float targetPitch = Vector3.Forward.SignedAngleTo(delta.Normalized(), Vector3.Right);
			targetPitch += settings.pitchAngle; //Add
			return targetPitch;
		}

		private float CurrentYawTracking { get; set; }
		private float CurrentPitchTracking { get; set; }
		private float yawTrackingVelocity;
		private float pitchTrackingVelocity;
		private const float ROTATION_TRACKING_SMOOTHING = .1f;
		private void UpdateTrackingRotation()
		{
			float yawTracking = Mathf.Lerp(CalculateTrackingYaw(BlendFromSettings), CalculateTrackingYaw(BlendToSettings), transitionSmoothedRatio);
			if (SnapFlag)
			{
				CurrentYawTracking = yawTracking;
				yawTrackingVelocity = 0;
			}
			else
				CurrentYawTracking = ExtensionMethods.SmoothDampAngle(CurrentYawTracking, yawTracking, ref yawTrackingVelocity, ROTATION_TRACKING_SMOOTHING);
			calculationRoot.GlobalRotate(calculationGimbal.Up(), CurrentYawTracking);

			float pitchTracking = Mathf.Lerp(CalculateTrackingPitch(BlendFromSettings), CalculateTrackingPitch(BlendToSettings), transitionSmoothedRatio);
			if (SnapFlag)
			{
				CurrentPitchTracking = pitchTracking;
				pitchTrackingVelocity = 0;
			}
			else
				CurrentPitchTracking = ExtensionMethods.SmoothDampAngle(CurrentPitchTracking, pitchTracking, ref pitchTrackingVelocity, ROTATION_TRACKING_SMOOTHING);

			calculationGimbal.Rotation += Vector3.Right * CurrentPitchTracking;
		}

		private readonly float PITCH_RISING_LEAD_RATIO = 1f;
		private readonly float PITCH_FALLING_LEAD_RATIO = 2f;
		private readonly float PITCH_MAX_LEAD_AMOUNT = 5f;
		private float CalculateTrackingPitch(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			Vector3 targetLookAtPosition = Character.GlobalPosition;
			if (settings.isRollEnabled)
				targetLookAtPosition += PathFollower.Up() * settings.height;
			else
				targetLookAtPosition += PathFollower.UpAxis * settings.height;
			if (!Character.IsOnGround)
			{
				float leadAmount = Character.VerticalSpd * PhysicsManager.physicsDelta;
				if (Character.VerticalSpd > 0)
					leadAmount *= PITCH_RISING_LEAD_RATIO;
				else
					leadAmount *= PITCH_FALLING_LEAD_RATIO;

				leadAmount = Mathf.Clamp(leadAmount, -PITCH_MAX_LEAD_AMOUNT, PITCH_MAX_LEAD_AMOUNT);
				targetLookAtPosition += Character.UpDirection * leadAmount;
			}

			Vector3 delta = calculationRoot.GlobalPosition - targetLookAtPosition;
			delta = calculationRoot.Basis.Inverse() * delta;
			delta.x = 0;
			float targetPitch = Vector3.Forward.SignedAngleTo(delta.Normalized(), Vector3.Right);
			targetPitch -= calculationGimbal.Rotation.x;
			return targetPitch * settings.pitchTrackingStrength;
		}

		private float CalculateTrackingYaw(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			Vector3 referencePos = PathFollower.Basis.Inverse() * (calculationRoot.GlobalPosition - PathFollower.GlobalPosition);
			Vector2 delta = (PathFollower.TruePlayerPositionDelta - referencePos).Flatten().Normalized();
			float targetYaw = -Vector2.Down.AngleTo(delta);
			return targetYaw * settings.yawTrackingStrength;
		}

		private void UpdateTrackingPosition()
		{
			Vector3 targetPosition = Vector3.Zero;
			targetPosition += CalculateVTracking(BlendFromSettings).Lerp(CalculateVTracking(BlendToSettings), transitionSmoothedRatio);
			targetPosition -= CalculateHTracking(BlendFromSettings).Lerp(CalculateHTracking(BlendToSettings), transitionSmoothedRatio);
			calculationRoot.GlobalPosition += targetPosition;
		}

		private Vector3 CalculateHTracking(CameraSettingsResource settings)
		{
			if (settings == null || !settings.hTrackingEnabled || settings.isStaticCamera) return Vector3.Zero;

			if (settings.isRollEnabled)
				return PathFollower.Right() * PathFollower.TruePlayerPositionDelta.x;
			else
				return PathFollower.RightAxis * PathFollower.FlatPlayerPositionDelta.x;
		}

		private Vector3 CalculateVTracking(CameraSettingsResource settings)
		{
			if (settings == null || !settings.vTrackingEnabled || settings.isStaticCamera) return Vector3.Zero;

			if (settings.isRollEnabled)
				return PathFollower.Up() * PathFollower.TruePlayerPositionDelta.y;
			else
				return PathFollower.UpAxis * PathFollower.FlatPlayerPositionDelta.y;
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
				SnapFlag = true;
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
			if (e is InputEventMouseMotion)
			{
				if (freeCamRotating)
				{
					cameraRoot.Rotation += Vector3.Up * Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.x) * MOUSE_SENSITIVITY;
					cameraGimbal.Rotation += Vector3.Right * Mathf.DegToRad((e as InputEventMouseMotion).Relative.y) * MOUSE_SENSITIVITY;
				}
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