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
			UpdateCameraSettings(defaultSettings, 0f); //Apply default settings
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

		/// <summary> 0 -> Don't use backstep, 1 -> Use backstep. </summary>
		private float backstepBlend;
		private float backstepBlendVelocity;
		/// <summary> Doesn't update when the Character isn't moving. </summary>
		private bool isBackstepActive;
		private readonly float BACKSTEP_TRANSITION_SPEED = .4f;

		/// <summary>
		///Updates whether backstep distance should be used.
		/// </summary>
		private void UpdateBackstepCamera()
		{
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
			UpdateBackstepCamera();

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
			{
				cameraRoot.GlobalTransform = calculationRoot.GlobalTransform;
				cameraGimbal.GlobalTransform = calculationGimbal.GlobalTransform;
			}

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

		private float CalculateXform(CameraSettingsResource settings)
		{
			if (settings == null) return 0;

			if (settings.IsStaticCamera)
				return CalculateYaw(settings);

			return CharacterController.CalculateForwardAngle(cameraRoot.Forward());
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

			//Add Distance
			float distance = settings.distance;
			distance += Mathf.Lerp(0, settings.backstepDistanceAddition, backstepBlend);

			if (settings.yawMode != CameraSettingsResource.OverrideModes.Override)
				targetPosition += PathFollower.Back() * distance;
			else
				targetPosition += Vector3.Forward.Rotated(Vector3.Up, settings.yawAngle) * distance;

			//Add Height
			if (settings.isRollEnabled)
				targetPosition += PathFollower.Up() * settings.height;
			else
				targetPosition += PathFollower.UpAxis * settings.height;

			if (settings.IsFieldCamera) //Horizontal tracking
			{
				if (settings.isRollEnabled)
					targetPosition += PathFollower.Right() * PathFollower.TruePlayerPositionDelta.x;
				else
					targetPosition += PathFollower.Forward().Rotated(Vector3.Up, Mathf.Pi * .5f).RemoveVertical().Normalized() * PathFollower.FlatPlayerPositionDelta.x;
			}

			if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModes.Move) //Vertical tracking
			{
				if (settings.isRollEnabled)
					targetPosition += PathFollower.Up() * PathFollower.TruePlayerPositionDelta.y;
				else
					targetPosition += PathFollower.UpAxis * PathFollower.FlatPlayerPositionDelta.y;
			}

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

			if (isYawOverrideActive) //Yaw override (for Grindrails, needs to be deprecated)
			{
				targetYaw = targetYawOverride;
				isYawOverrideActive = false;
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
			if (settings == null) return 0; //Invalid resource

			if (settings.IsStaticCamera)
				return (Character.GlobalPosition - settings.staticPosition).Flatten().AngleTo(Vector2.Down);

			if (settings.yawMode == CameraSettingsResource.OverrideModes.Override)
				return settings.yawAngle; //Override view direction

			//Forward direction is based on PathFollower's orientation
			return PathFollower.ForwardAngle + settings.yawAngle; //Add
		}

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
			{
				Vector3 staticDelta = settings.staticPosition - Character.GlobalPosition;
				staticDelta = staticDelta.Rotated(Vector3.Up, -CalculateYaw(settings));
				staticDelta.x = 0;
				return Vector3.Forward.SignedAngleTo(staticDelta.Normalized(), Vector3.Right);
			}

			if (settings.pitchMode == CameraSettingsResource.OverrideModes.Override)
				return settings.pitchAngle; //Override view direction

			Vector3 targetLookAtPosition = Character.GlobalPosition;
			if (settings.isRollEnabled)
				targetLookAtPosition += PathFollower.Up() * settings.height;
			else
				targetLookAtPosition += PathFollower.UpAxis * settings.height;

			if (!Character.IsOnGround && settings.verticalTrackingMode == CameraSettingsResource.TrackingModes.Rotate)
			{
				float leadAmount = Character.VerticalSpd * PhysicsManager.physicsDelta;
				if (Character.VerticalSpd > 0)
					leadAmount *= PITCH_RISING_LEAD_RATIO;
				else
					leadAmount *= PITCH_FALLING_LEAD_RATIO;

				leadAmount = Mathf.Clamp(leadAmount, -PITCH_MAX_LEAD_AMOUNT, PITCH_MAX_LEAD_AMOUNT);
				targetLookAtPosition += Character.UpDirection * leadAmount;
			}

			Vector3 delta = CalculatePosition(settings) - targetLookAtPosition;
			delta = delta.Rotated(Vector3.Up, -PathFollower.ForwardAngle);
			delta = delta.Rotated(Vector3.Forward, CalculateTilt(settings));
			delta.x = 0;
			float targetPitch = Vector3.Forward.SignedAngleTo(delta.Normalized(), Vector3.Right);
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