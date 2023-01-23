using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay
{
	/// <summary>
	/// Follows the player based on the settings provided from CameraSettingsResource.cs
	/// </summary>
	//Undergoing complete rewrite...Again.
	public partial class CameraController : Node3D
	{
		public static CameraController instance;
		public Node3D ExternalController { get; set; } //Node3D to follow (i.e. in a cutscene)

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

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			instance = this;
			UpdateCameraSettings(defaultSettings, 0f); //Apply default settings
		}

		public override void _PhysicsProcess(double _)
		{
			if (ExternalController != null)
			{
				cameraRoot.GlobalTransform = ExternalController.GlobalTransform;
				return;
			}

			//Don't update the camera when the player is defeated
			if (Character.IsDefeated) return;

			UpdateGameplayCamera();

			if (OS.IsDebugBuild())
				UpdateFreeCam();
		}

		#region Gameplay Camera
		#region Transitions and Settings
		[Export]
		public CameraSettingsResource defaultSettings; //Default settings to use when nothing is set
		public CameraSettingsResource ActiveSettings => BlendSettingsList.Count == 0 ? null : BlendSettingsList[BlendSettingsList.Count - 1]; //Settings to transition to

		/// <summary> Ratio [0 <-> 1] of transition that has been completed. </summary>
		private readonly List<float> LinearBlendRatioList = new List<float>();
		/// <summary> Smoothstep transition time. </summary>
		private readonly List<float> SmoothBlendRatioList = new List<float>();
		/// <summary> Speed of transition (in seconds). </summary>
		private readonly List<float> BlendTimeList = new List<float>();
		private readonly List<CameraSettingsResource> BlendSettingsList = new List<CameraSettingsResource>();

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
			if (data == null) return;

			if (Mathf.IsZeroApprox(blendTime)) //Cut transition
				SnapFlag = true;
			else if (useCrossfade) //Crossfade transition
			{
				StartCrossfade();
				SnapFlag = true;
			}

			//Add current data
			BlendSettingsList.Add(data);
			LinearBlendRatioList.Add(0);
			SmoothBlendRatioList.Add(0);
			BlendTimeList.Add(blendTime);
		}

		/// <summary> Set to true to skip smoothing. </summary>
		public bool SnapFlag { get; set; }
		/// <summary>
		/// Update the transition timer.
		/// </summary>
		private void UpdateTransitionTimer()
		{
			//Clear all lists (except active one) when snapping
			if (SnapFlag)
			{
				for (int i = BlendSettingsList.Count - 2; i >= 0; i--)
				{
					BlendSettingsList.RemoveAt(i);
					LinearBlendRatioList.RemoveAt(i);
					SmoothBlendRatioList.RemoveAt(i);
					BlendTimeList.RemoveAt(i);
				}

				LinearBlendRatioList[0] = SmoothBlendRatioList[0] = 1;
			}
			else
			{
				for (int i = BlendSettingsList.Count - 1; i >= 0; i--)
				{
					//Remove completed blends
					if (i < BlendSettingsList.Count - 2 && Mathf.IsEqualApprox(LinearBlendRatioList[i + 1], 1.0f))
					{
						BlendSettingsList.RemoveAt(i);
						LinearBlendRatioList.RemoveAt(i);
						SmoothBlendRatioList.RemoveAt(i);
						BlendTimeList.RemoveAt(i);
						continue;
					}

					LinearBlendRatioList[i] = Mathf.MoveToward(LinearBlendRatioList[i], 1f, (1f / BlendTimeList[i]) * PhysicsManager.physicsDelta);
					SmoothBlendRatioList[i] = Mathf.SmoothStep(0, 1f, LinearBlendRatioList[i]);
				}
			}
		}

		/// <summary> Is the lockon camera currently active? </summary>
		private bool isLockonActive;
		/// <summary> 0 -> Don't use lockon camera, 1 -> Use lockon camera. </summary>
		private float lockonBlend;
		private float lockonBlendVelocity;
		/// <summary> Value of the lockon object's last known position. Used in case Lockon becomes null before lockon blend finishes. </summary>
		private Vector3 lastLockonPosition;
		private readonly float LOCKON_TRANSITION_SPEED = .2f;
		/// <summary> Add some extra height while homing attacking </summary>
		private readonly float LOCKON_HEIGHT = 2f;
		/// <summary> Add some extra distance while homing attacking </summary>
		private readonly float LOCKON_DISTANCE = 1f;
		/// <summary> Limit how much the lockon camera can rotate </summary>
		private readonly float MAX_LOCKON_PITCH = Mathf.Pi * .3f;

		/// <summary> Is backstep camera currently active? </summary>
		private bool isBackstepActive;
		/// <summary> 0 -> Don't use backstep, 1 -> Use backstep. </summary>
		private float backstepBlend;
		private float backstepBlendVelocity;
		private readonly float BACKSTEP_TRANSITION_SPEED = .4f;

		/// <summary>
		///Updates whether backstep/lockon should be used.
		/// </summary>
		private void UpdateTrackingSettings()
		{
			if (!Character.IsOnGround && Character.Lockon.LockonEnemy != null)
			{
				isLockonActive = true;
				lastLockonPosition = Character.Lockon.LockonEnemy.GlobalPosition;
			}
			else
				isLockonActive = false;

			if (Character.MoveSpeed != 0) // Doesn't update when Character isn't moving.
			{
				if (Character.IsMovingBackward)
					isBackstepActive = true;
				else if (Character.Skills.IsSpeedBreakActive || Character.Lockon.IsHomingAttacking ||
				Character.IsHoldingDirection(PathFollower.ForwardAngle))
					isBackstepActive = false;
			}

			if (SnapFlag)
			{
				lockonBlend = isLockonActive ? 1 : 0;

				backstepBlend = isBackstepActive ? 1 : 0;
				lockonBlendVelocity = backstepBlendVelocity = 0;
			}
			else
			{
				lockonBlend = ExtensionMethods.SmoothDamp(lockonBlend, isLockonActive ? 1 : 0, ref lockonBlendVelocity, LOCKON_TRANSITION_SPEED);
				backstepBlend = ExtensionMethods.SmoothDamp(backstepBlend, isBackstepActive ? 1 : 0, ref backstepBlendVelocity, BACKSTEP_TRANSITION_SPEED);
			}
		}
		#endregion

		private void UpdateGameplayCamera()
		{
			UpdateTransitionTimer();
			UpdateTrackingSettings();

			UpdatePosition();
			UpdateRotation();

			//Update FOV and view offsets
			Camera.Fov = CalculateTargetFOV(BlendSettingsList[0]);
			Vector2 viewOffset = CalculateViewOffset(BlendSettingsList[0]);
			if (BlendSettingsList.Count > 1)
			{
				for (int i = 1; i < BlendSettingsList.Count; i++)
				{
					Camera.Fov = Mathf.Lerp(Camera.Fov, CalculateTargetFOV(BlendSettingsList[i]), SmoothBlendRatioList[i]);
					viewOffset = viewOffset.Lerp(CalculateViewOffset(BlendSettingsList[i]), SmoothBlendRatioList[i]);
				}
			}

			Camera.HOffset = viewOffset.x;
			Camera.VOffset = viewOffset.y;
			if (!freeCamEnabled) //Apply transform
				SyncCameraTransforms();

			//Update input transformation angle
			xformAngle = CalculateXform(BlendSettingsList[0]);
			if (BlendSettingsList.Count > 1)
			{
				for (int i = 1; i < BlendSettingsList.Count; i++)
					xformAngle = Mathf.LerpAngle(xformAngle, CalculateXform(BlendSettingsList[i]), SmoothBlendRatioList[i]);
			}

			if (SnapFlag) //Reset flag
				SnapFlag = false;
		}

		private void SyncCameraTransforms()
		{
			cameraRoot.GlobalTransform = calculationRoot.GlobalTransform;
			cameraGimbal.GlobalTransform = calculationGimbal.GlobalTransform;
		}

		private float CalculateXform(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			if (settings.IsStaticCamera)
				return CalculateYaw(settings);

			return Character.CalculateForwardAngle(calculationRoot.Forward());
		}

		private float CalculateTargetFOV(CameraSettingsResource settings)
		{
			if (settings == null || !settings.useCustomFOV) return Camera.Fov;
			return settings.fov;
		}
		private Vector2 CalculateViewOffset(CameraSettingsResource settings)
		{
			if (settings == null) return Vector2.Zero;
			return new Vector2(settings.hOffset, settings.vOffset);
		}

		private void UpdatePosition()
		{
			Vector3 targetPosition = CalculatePosition(BlendSettingsList[0]);
			if (BlendSettingsList.Count > 1)
			{
				for (int i = 1; i < BlendSettingsList.Count; i++)
					targetPosition = targetPosition.Lerp(CalculatePosition(BlendSettingsList[i]), SmoothBlendRatioList[i]);
			}

			calculationRoot.GlobalPosition = targetPosition;
		}

		/// <summary>
		/// Calculates the (offset) position for a given CameraSettingResource.
		/// </summary>
		private Vector3 CalculatePosition(CameraSettingsResource settings)
		{
			if (settings == null) return Vector3.Zero;
			if (settings.IsStaticCamera) //Static camera
				return settings.staticPosition;

			Vector3 targetPosition = PathFollower.GlobalPosition;
			float height = settings.height;
			float distance = settings.distance;

			//Add tracking modifiers
			distance += Mathf.Lerp(0, settings.backstepDistance, backstepBlend);
			if (settings.isLockonTrackingEnabled)
			{
				height += Mathf.Lerp(0, LOCKON_HEIGHT, lockonBlend);
				distance += Mathf.Lerp(0, LOCKON_DISTANCE, lockonBlend);
			}

			if (settings.yawMode != CameraSettingsResource.OverrideModes.Override)
				targetPosition += PathFollower.Back() * distance;
			else
				targetPosition += Vector3.Forward.Rotated(Vector3.Up, settings.yawAngle) * distance;

			bool trackHorizontally = settings.IsFieldCamera || settings.IsHallCamera;
			if (trackHorizontally) //Horizontal tracking
			{
				float trackingAmount = settings.isRollEnabled ? PathFollower.TruePlayerPositionDelta.x : PathFollower.FlatPlayerPositionDelta.x;
				if (settings.IsHallCamera)
					trackingAmount = Mathf.Clamp(trackingAmount, -settings.hallWidth, settings.hallWidth);

				if (settings.isRollEnabled)
					targetPosition += PathFollower.Right() * trackingAmount;
				else
					targetPosition += PathFollower.Forward().Rotated(Vector3.Up, Mathf.Pi * .5f).RemoveVertical().Normalized() * trackingAmount;
			}

			//Calculate Height
			if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModes.Move) //Vertical tracking
				height += settings.isRollEnabled ? PathFollower.TruePlayerPositionDelta.y : PathFollower.FlatPlayerPositionDelta.y;

			if (settings.isRollEnabled)
				targetPosition += PathFollower.Up() * height;
			else
				targetPosition += PathFollower.UpAxis * height;

			return targetPosition;
		}

		public float CurrentPitch { get; private set; }
		public float CurrentYaw { get; private set; }
		public float CurrentTilt { get; private set; }
		private void UpdateRotation()
		{
			//Calculate target angles
			float targetTilt = CalculateTilt(BlendSettingsList[0]);
			float targetYaw = CalculateYaw(BlendSettingsList[0]);
			if (BlendSettingsList.Count > 1)
			{
				for (int i = 1; i < BlendSettingsList.Count; i++)
				{
					targetTilt = Mathf.Lerp(targetTilt, CalculateTilt(BlendSettingsList[i]), SmoothBlendRatioList[i]);
					targetYaw = Mathf.LerpAngle(targetYaw, CalculateYaw(BlendSettingsList[i]), SmoothBlendRatioList[i]);
				}
			}

			CurrentTilt = targetTilt;
			CurrentYaw = targetYaw;

			calculationRoot.GlobalRotation = new Vector3(0, CurrentYaw, CurrentTilt);

			//Pitch has to be calculated after yaw and tilt rotation was applied
			float targetPitch = CalculatePitch(BlendSettingsList[0]);
			if (BlendSettingsList.Count > 1)
			{
				for (int i = 1; i < BlendSettingsList.Count; i++)
					targetPitch = Mathf.Lerp(targetPitch, CalculatePitch(BlendSettingsList[i]), SmoothBlendRatioList[i]);
			}

			CurrentPitch = targetPitch;
			calculationGimbal.Rotation = Vector3.Right * CurrentPitch;
		}

		private float CalculateTilt(CameraSettingsResource settings)
		{
			if (settings == null || !settings.isRollEnabled || settings.IsStaticCamera) return 0;

			//Rotate the z axis along PathFollower's forward, by angle of the ground direction
			Vector3 angle = PathFollower.Up().Rotated(Vector3.Up, -PathFollower.ForwardAngle);
			angle.z = 0;

			return angle.Normalized().SignedAngleTo(Vector3.Up, Vector3.Forward);
		}

		/// <summary>
		/// Calculates yaw (y-axis rotation) of a given CameraSettingsResource.
		/// </summary>
		private float CalculateYaw(CameraSettingsResource settings)
		{
			if (settings == null) return 0; //Invalid resource

			if (settings.IsStaticCamera)
				return (Character.GlobalPosition - settings.staticPosition).Flatten().AngleTo(Vector2.Down);

			if (settings.yawMode == CameraSettingsResource.OverrideModes.Override)
				return settings.yawAngle; //Override view direction

			//Forward direction is based on PathFollower's orientation
			return PathFollower.ForwardAngle + settings.yawAngle; //Add
		}

		private readonly float PITCH_DEADZONE = .6f; //Don't bother rotating when the player's screen ratio is less than this
		private readonly float PITCH_RISING_LEAD_RATIO = .2f;
		private readonly float PITCH_FALLING_LEAD_RATIO = .4f;
		private readonly float PITCH_MAX_LEAD_AMOUNT = 5f;
		/// <summary>
		/// Calculates pitch (x-axis rotation) of a given CameraSettingsResource.
		/// </summary>
		private float CalculatePitch(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			if (settings.IsStaticCamera)
				return CalculatePitchAngle(Character.GlobalPosition, settings);

			if (settings.pitchMode == CameraSettingsResource.OverrideModes.Override)
				return settings.pitchAngle; //Override view direction

			Vector3 upAxis = settings.isRollEnabled ? PathFollower.Up() : PathFollower.UpAxis;
			Vector3 targetLookAtPosition = Character.GlobalPosition + upAxis * settings.height;
			float playerPitch = CalculatePitchAngle(targetLookAtPosition, settings);

			//Tracking
			if (settings.isLockonTrackingEnabled)
			{
				float enemyPitch = CalculatePitchAngle(lastLockonPosition + PathFollower.Forward() * LOCKON_DISTANCE, settings) - settings.pitchAngle;
				if (Mathf.Abs(enemyPitch) > MAX_LOCKON_PITCH) //Incase the player decides to just ignore the enemy and keep moving forward
					enemyPitch = Mathf.Sign(enemyPitch) * MAX_LOCKON_PITCH;

				return Mathf.Lerp(playerPitch, enemyPitch, lockonBlend);
			}

			if (!Character.IsOnGround && settings.verticalTrackingMode == CameraSettingsResource.TrackingModes.Rotate)
			{
				float leadAmount = Character.VerticalSpd * PhysicsManager.physicsDelta;
				leadAmount *= Character.VerticalSpd > 0 ? PITCH_RISING_LEAD_RATIO : PITCH_FALLING_LEAD_RATIO;
				leadAmount = Mathf.Clamp(leadAmount, -PITCH_MAX_LEAD_AMOUNT, PITCH_MAX_LEAD_AMOUNT);
				targetLookAtPosition += Character.UpDirection * leadAmount;

				//For better framing
				float maxFOV = Mathf.DegToRad(CalculateTargetFOV(settings) * .5f);
				float pathPitch = CalculatePitchAngle(PathFollower.GlobalPosition + upAxis * settings.height, settings);
				float characterPitch = CalculatePitchAngle(targetLookAtPosition, settings) - pathPitch;
				float factor = characterPitch / maxFOV;
				factor = Mathf.Abs(factor) <= PITCH_DEADZONE ? 0 : (factor - Mathf.Sign(factor) * PITCH_DEADZONE);

				return pathPitch + maxFOV * factor;
			}

			return playerPitch;
		}

		private float CalculatePitchAngle(Vector3 lookAt, CameraSettingsResource settings)
		{
			Vector3 delta = CalculatePosition(settings) - lookAt;
			delta = delta.Rotated(Vector3.Up, -CalculateYaw(settings));
			delta = delta.Rotated(Vector3.Forward, CalculateTilt(settings));
			delta.x = 0;

			float targetPitch = Vector3.Forward.SignedAngleTo(delta.Normalized(), Vector3.Right);
			if (!settings.IsStaticCamera)
				targetPitch += settings.pitchAngle; //Add

			return targetPitch;
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