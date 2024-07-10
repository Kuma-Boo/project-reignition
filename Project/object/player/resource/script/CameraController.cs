using Godot;
using Project.Core;
using Project.Gameplay.Triggers;
using System.Collections.Generic;

namespace Project.Gameplay;

/// <summary>
/// Follows the player based on the settings provided from CameraSettingsResource.cs
/// </summary>
public partial class CameraController : Node3D
{
	public const float DefaultFov = 70;

	[ExportGroup("Components")]
	[Export]
	public Camera3D Camera { get; private set; }
	[Export]
	public Node3D FreeCamRoot { get; private set; }
	[Export]
	private Node3D cameraRoot;
	[Export]
	private Node3D debugMesh;
	public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => Camera.UnprojectPosition(worldSpace);
	public bool IsOnScreen(Vector3 worldSpace) => Camera.IsPositionInFrustum(worldSpace);
	public bool IsBehindCamera(Vector3 worldSpace) => Camera.IsPositionBehind(worldSpace);

	[Export]
	private TextureRect crossfade;
	[Export]
	private AnimationPlayer crossfadeAnimator;
	public bool IsCrossfading => crossfadeAnimator.IsPlaying();

	[Export]
	/// <summary> Camera's pathfollower. Different than Character.PathFollower. </summary>
	public CharacterPathFollower PathFollower { get; private set; }
	private CharacterController Character => CharacterController.instance;

	private readonly StringName SHADER_GLOBAL_PLAYER_SCREEN_POSITION = new("player_screen_position");

	public void Initialize()
	{
		if (Engine.IsEditorHint()) return;

		//Apply default settings
		CameraSettingsResource targetSettings = (StageSettings.instance?.InitialCameraSettings) ?? defaultSettings;

		SnapXform();
		UpdateCameraSettings(new CameraBlendData()
		{
			SettingsResource = targetSettings,
		});

		Runtime.Instance.Connect(Runtime.SignalName.EventInputed, new(this, MethodName.ReceiveInput));
	}

	public void Respawn()
	{
		SnapXform();
		//Revert camera settings
		UpdateCameraSettings(new CameraBlendData()
		{
			SettingsResource = StageSettings.instance.CurrentCheckpoint.CameraSettings,
		});
	}

	public override void _PhysicsProcess(double _)
	{
		PathFollower.Resync();

		// Don't update the camera when the player is defeated from a DeathTrigger
		if (IsDefeatFreezeActive)
		{
			if (Character.IsDefeated)
				return;

			IsDefeatFreezeActive = false;
		}

		UpdateGameplayCamera();
	}

	public override void _Process(double _)
	{
		if (OS.IsDebugBuild())
			UpdateFreeCam();
	}

	/// <summary> Enabled when the camera should freeze due to a DeathTrigger. </summary>
	public bool IsDefeatFreezeActive { get; set; }
	/// <summary> Used to focus onto multi-HP enemies, bosses, etc. Not to be confused with CharacterLockon.Target. </summary>
	public Node3D LockonTarget { get; set; }
	private bool IsLockonCameraActive => LockonTarget != null || Character.Lockon.IsHomingAttacking || Character.Lockon.IsBounceLockoutActive;
	/// <summary> [0 -> 1] ratio of how much to use the lockon camera. </summary>
	private float lockonBlend;
	private float lockonBlendVelocity;
	/// <summary> [0 -> 1] ratio of how much to focus onto LockonTarget. </summary>
	private float lockonTargetBlend;
	private float lockonTargetBlendVelocity;
	/// <summary> Snappier blend when lockon is active to keep things in frame. </summary>
	private const float LockonBlendInSmoothing = 5.0f;
	/// <summary> More smoothing/slower blend when resetting lockonBlend. </summary>
	private const float LockonBlendOutSmoothing = 20.0f;
	/// <summary> How much extra distance to add when performing a homing attack. </summary>
	private const float LockonDistance = 3f;
	private void UpdateLockonTarget()
	{
		if (LockonTarget?.IsInsideTree() == false) // Invalid LockonTarget
			LockonTarget = null;

		float targetBlend = 0;
		float smoothing = LockonBlendOutSmoothing;

		// Lockon is active
		if (IsLockonCameraActive)
		{
			targetBlend = 1;
			smoothing = LockonBlendInSmoothing;
		}

		lockonBlend = ExtensionMethods.SmoothDamp(lockonBlend, targetBlend, ref lockonBlendVelocity, smoothing * PhysicsManager.physicsDelta);
		lockonTargetBlend = ExtensionMethods.SmoothDamp(lockonTargetBlend, LockonTarget == null ? 0 : 1, ref lockonTargetBlendVelocity, LockonBlendOutSmoothing * PhysicsManager.physicsDelta);
	}

	#region Gameplay Camera
	/// <summary> Skips smoothing for the current frame. </summary>
	public bool SnapFlag { get; set; }

	[Export]
	/// <summary> Default camera settings to use when nothing is set. </summary>
	public CameraSettingsResource defaultSettings;
	/// <summary> Reference to active CameraBlendData. </summary>
	public CameraBlendData ActiveBlendData => CameraBlendList[^1];
	/// <summary> Reference to active CameraSettingsResource. </summary>
	public CameraSettingsResource ActiveSettings => ActiveBlendData.SettingsResource;
	/// <summary> A list of all camera settings that are influencing camera. </summary>
	private readonly List<CameraBlendData> CameraBlendList = [];

	public void StartCrossfade(float speed = 1.0f)
	{
		// Already crossfading
		if (IsCrossfading)
			return;

		// Update the crossfade texture
		ImageTexture tex = new();
		tex.SetImage(GetViewport().GetTexture().GetImage());
		crossfade.Texture = tex;
		crossfadeAnimator.Play("activate");// Start crossfade animation
		crossfadeAnimator.SpeedScale = speed;

		if (!StageSettings.instance.IsLevelIngame)
			return;

		// Warp the camera
		SnapFlag = true;
		UpdateGameplayCamera();
	}

	/// <summary> Changes the current camera settings. </summary>
	public void UpdateCameraSettings(CameraBlendData data, bool enableXformBlend = false)
	{
		if (data.SettingsResource == null) return; // Invalid data

		if (Mathf.IsZeroApprox(data.BlendTime)) // Cut transition
		{
			SnapFlag = true;
			if (enableXformBlend)
				StartXformBlend();
		}
		else if (data.IsCrossfadeEnabled) // Crossfade transition
		{
			StartCrossfade(1.0f / data.BlendTime);
			SnapFlag = true;
			if (enableXformBlend)
				StartXformBlend();
		}
		else
		{
			data.CalculateBlendSpeed(); // Cache blend speed so we don't have to do it every frame
		}

		// Add current data to blend list
		if (data.Trigger == null && data.SettingsResource.useStaticPosition) // Fallback to static position value
			data.StaticPosition = data.SettingsResource.staticPosition;

		CameraBlendList.Add(data);
	}

	/// <summary> Update the transition timer. </summary>
	private void UpdateTransitionTimer()
	{
		// Clear all lists (except active blend) when snapping
		if (SnapFlag)
		{
			// Remove all blend data except the latest (active)
			for (int i = CameraBlendList.Count - 2; i >= 0; i--)
			{
				CameraBlendList[i].Free(); // Prevent memory leak
				CameraBlendList.RemoveAt(i);
			}

			// The remaining blend data is active, and its influence is set to 1
			CameraBlendList[0].SetInfluence(1);
			return;
		}

		for (int i = CameraBlendList.Count - 1; i >= 0; i--)
			UpdateCameraBlendInfluence(i);
	}

	/// <summary> Update the influence of a particular blend. </summary>
	private void UpdateCameraBlendInfluence(int blendIndex)
	{
		// Removes completed blends (Excluding active blend data)
		if (blendIndex < CameraBlendList.Count - 1 && Mathf.IsEqualApprox(CameraBlendList[blendIndex + 1].LinearInfluence, 1.0f))
		{
			CameraBlendList[blendIndex].Free();
			CameraBlendList.RemoveAt(blendIndex);
			return;
		}

		float influence = Mathf.MoveToward(CameraBlendList[blendIndex].LinearInfluence, 1f,
			CameraBlendList[blendIndex].BlendSpeed * PhysicsManager.physicsDelta);
		CameraBlendList[blendIndex].SetInfluence(influence);
	}

	private void UpdateGameplayCamera()
	{
		UpdateTransitionTimer();
		UpdateLockonTarget();

		CameraPositionData data = new()
		{
			offsetBasis = Basis.Identity,
			blendData = ActiveBlendData
		};

		float distance = 0;
		float staticBlendRatio = 0; // Blend value of whether to use static camera positions or not
		Vector2 viewportOffset = Vector2.Zero;
		float fov = DefaultFov;

		for (int i = 0; i < CameraBlendList.Count; i++) // Simulate each blend data separately
		{
			CameraPositionData iData = SimulateCamera(i);
			data.offsetBasis = data.offsetBasis.Slerp(iData.offsetBasis, CameraBlendList[i].SmoothedInfluence);
			distance = Mathf.Lerp(distance, iData.blendData.distance, CameraBlendList[i].SmoothedInfluence);

			data.precalculatedPosition = data.precalculatedPosition.Lerp(iData.precalculatedPosition, CameraBlendList[i].SmoothedInfluence);

			data.yawTracking = Mathf.LerpAngle(data.yawTracking, iData.yawTracking, CameraBlendList[i].SmoothedInfluence);
			data.secondaryYawTracking = Mathf.LerpAngle(data.secondaryYawTracking, iData.secondaryYawTracking, CameraBlendList[i].SmoothedInfluence);
			data.pitchTracking = Mathf.Lerp(data.pitchTracking, iData.pitchTracking, CameraBlendList[i].SmoothedInfluence);

			data.horizontalTrackingOffset = Mathf.Lerp(data.horizontalTrackingOffset, iData.horizontalTrackingOffset, CameraBlendList[i].SmoothedInfluence);
			data.verticalTrackingOffset = Mathf.Lerp(data.verticalTrackingOffset, iData.verticalTrackingOffset, CameraBlendList[i].SmoothedInfluence);

			staticBlendRatio = Mathf.Lerp(staticBlendRatio, CameraBlendList[i].SettingsResource.useStaticPosition ? 1 : 0, CameraBlendList[i].SmoothedInfluence);
			viewportOffset = viewportOffset.Lerp(CameraBlendList[i].SettingsResource.viewportOffset, CameraBlendList[i].SmoothedInfluence);

			fov = Mathf.Lerp(fov, iData.blendData.Fov, CameraBlendList[i].SmoothedInfluence);
		}

		// Recalculate non-static camera positions for better transition rotations.
		Vector3 position = data.offsetBasis.Z.Normalized() * distance;
		position += Character.CenterPosition;

		Transform3D cameraTransform = new(data.offsetBasis, position.Lerp(data.precalculatedPosition, staticBlendRatio));
		cameraTransform = cameraTransform.RotatedLocal(Vector3.Up, data.yawTracking);

		// Calculate xform angle before applying pitch tracking
		UpdateInputXForm(cameraTransform.Basis);

		// Apply pitch tracking
		cameraTransform = cameraTransform.RotatedLocal(Vector3.Right, data.pitchTracking);

		// Apply secondary yaw tracking
		cameraTransform = cameraTransform.RotatedLocal(Vector3.Up, data.secondaryYawTracking);

		// Update cameraTransform origin
		viewportOffset.Y = Mathf.Lerp(viewportOffset.Y, 0, lockonBlend);
		cameraTransform.Origin = AddTrackingOffset(cameraTransform.Origin, data);
		cameraTransform.Origin += cameraTransform.Basis.X * viewportOffset.X;
		cameraTransform.Origin += cameraTransform.Basis.Y * viewportOffset.Y;

		cameraRoot.GlobalTransform = cameraTransform; // Update transform

		Camera.Fov = fov; // Update fov
		RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_PLAYER_SCREEN_POSITION, ConvertToScreenSpace(Character.CenterPosition) / Runtime.SCREEN_SIZE);

		if (SnapFlag) // Reset flag after camera was updated
			SnapFlag = false;
	}

	/// <summary> Angle to use when transforming from world space to camera space. </summary>
	private float xformAngle;
	/// <summary> Previous xform angle used right before the last camera change. </summary>
	private float previousXformAngle;
	private float xformBlend = 1;
	private readonly float XformSmoothing = 1.5f;
	public float TransformAngle(float angle) => xformAngle + angle;

	/// <summary> Starts blending the xform angle. </summary>
	private void StartXformBlend()
	{
		xformBlend = 0;
		previousXformAngle = xformAngle;
	}

	/// <summary> Blends xform angles for smoother inputs between camera cuts. </summary>
	private void UpdateInputXForm(Basis cameraBasis)
	{
		float targetXformAngle = ExtensionMethods.CalculateForwardAngle(-cameraBasis.Z, cameraBasis.Y);

		// Snap xform blend when no input is held
		if (Character.InputVector.IsZeroApprox() ||
			(Character.IsLockoutActive &&
			Character.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe &&
			Mathf.IsZeroApprox(Character.InputHorizontal)))
		{
			SnapXform();
		}

		xformBlend = Mathf.MoveToward(xformBlend, 1, XformSmoothing * PhysicsManager.physicsDelta);
		xformAngle = Mathf.LerpAngle(previousXformAngle, targetXformAngle, xformBlend);
	}

	public void SnapXform() => xformBlend = 1;

	private Vector3 AddTrackingOffset(Vector3 position, CameraPositionData data)
	{
		position += PathFollower.Right() * data.horizontalTrackingOffset;
		position += PathFollower.Up() * data.verticalTrackingOffset; // Use Pathfollower's up axis for vertical offset
		return position;
	}

	/// <summary> Simulates a camera setting and returns the Transform of where it would end up. </summary>
	private CameraPositionData SimulateCamera(int index)
	{
		CameraSettingsResource settings = CameraBlendList[index].SettingsResource;
		CameraPositionData data = new()
		{
			blendData = CameraBlendList[index],
		};

		// Update static data before simulating camera
		if (CameraBlendList[index].Trigger?.UpdateEveryFrame == true)
			CameraBlendList[index].Trigger.UpdateStaticData(CameraBlendList[index]);

		if (!settings.copyFov)
			data.blendData.Fov = Mathf.IsZeroApprox(settings.targetFOV) ? DefaultFov : settings.targetFOV;

		float targetYawAngle = settings.yawAngle;
		float targetPitchAngle = settings.pitchAngle;

		if (settings.useStaticPosition)
		{
			data.precalculatedPosition = data.blendData.StaticPosition;

			if (settings.copyRotation) // Override rotation w/ inherited basis
			{
				data.offsetBasis = data.blendData.RotationBasis.Orthonormalized();
			}
			else
			{
				Vector3 delta = Character.CenterPosition - data.precalculatedPosition;
				data.blendData.distance = delta.Length();
				delta = delta.Normalized();

				if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetYawAngle += delta.Flatten().AngleTo(Vector2.Up);
				targetYawAngle += Mathf.Pi;

				if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
					targetPitchAngle += delta.AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);

				data.blendData.yawAngle = targetYawAngle;
				data.blendData.pitchAngle = targetPitchAngle;
				data.CalculateBasis();
			}
		}
		else
		{
			// Calculate distance
			float targetDistance = settings.distance;
			if (Character.IsMovingBackward)
				targetDistance += settings.backstepDistance;

			if (!settings.ignoreHomingAttackDistance && IsLockonCameraActive)
				targetDistance += LockonDistance;

			if (PathFollower.Progress < targetDistance && !PathFollower.Loop)
				targetDistance = PathFollower.Progress;

			data.blendData.DistanceSmoothDamp(targetDistance, Character.IsMovingBackward, SnapFlag);

			// Calculate targetAngles when DistanceMode is set to Sample.
			float sampledTargetYawAngle = targetYawAngle;
			float sampledTargetPitchAngle = targetPitchAngle;

			float currentProgress = PathFollower.Progress; // Cache progress
			PathFollower.Progress -= data.blendData.distance;
			PathFollower.Progress += data.blendData.SettingsResource.sampleOffset;
			Vector3 sampledPosition = PathFollower.GlobalPosition;
			PathFollower.Progress = currentProgress + data.blendData.SettingsResource.sampleOffset; // Sample current
			Vector3 sampledForward = (PathFollower.GlobalPosition - sampledPosition).Normalized();
			PathFollower.Progress = currentProgress; // Revert progress

			if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
				sampledTargetYawAngle += ExtensionMethods.CalculateForwardAngle(sampledForward);
			if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
				sampledTargetPitchAngle += sampledForward.AngleTo(sampledForward.RemoveVertical().Normalized()) * Mathf.Sign(sampledForward.Y);

			// Calculate target angles when DistanceMode is set to Offset
			if (settings.yawOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
				targetYawAngle += PathFollower.ForwardAngle;
			if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
				targetPitchAngle += PathFollower.Forward().AngleTo(PathFollower.Forward().RemoveVertical().Normalized()) * Mathf.Sign(PathFollower.Forward().Y);

			if (settings.distanceCalculationMode == CameraSettingsResource.DistanceModeEnum.Auto) // Fixes slope changes
			{
				// Negative number -> Concave, Positive number -> Convex.
				float slopeDifference = sampledForward.Y - PathFollower.Forward().Y;
				data.blendData.SampleBlend = Mathf.Lerp(data.blendData.SampleBlend, slopeDifference < 0 ? 1.0f : 0.0f, .1f);
			}
			else if (settings.distanceCalculationMode == CameraSettingsResource.DistanceModeEnum.Sample)
			{
				data.blendData.SampleBlend = 1.0f;
			}
			else
			{
				data.blendData.SampleBlend = 0.0f;
			}

			// Fix rotated sampling cameras
			data.blendData.yawAngle = sampledTargetYawAngle;
			data.blendData.pitchAngle = sampledTargetPitchAngle;
			data.CalculateBasis();
			int yawSamplingFix = Mathf.Sign(sampledForward.Dot(-data.offsetBasis.Z));
			sampledTargetPitchAngle *= yawSamplingFix;

			// Interpolate angles
			data.blendData.yawAngle = Mathf.LerpAngle(targetYawAngle, sampledTargetYawAngle, data.blendData.SampleBlend);
			data.blendData.pitchAngle = Mathf.Lerp(targetPitchAngle, sampledTargetPitchAngle, data.blendData.SampleBlend);
			if (settings.followPathTilt) // Calculate tilt
				data.blendData.tiltAngle = PathFollower.Right().SignedAngleTo(-PathFollower.SideAxis, PathFollower.Forward()) * yawSamplingFix;

			// Update Tracking
			// Calculate position for tracking calculations
			data.CalculateBasis();
			data.CalculatePosition(PathFollower.GlobalPosition);

			Vector3 globalDelta = Character.CenterPosition - data.precalculatedPosition;
			Vector3 delta = data.offsetBasis.Inverse() * globalDelta;

			if (settings.horizontalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
			{
				data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X;

				if (settings.horizontalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate)
					data.secondaryYawTracking = -delta.Normalized().Flatten().AngleTo(Vector2.Down);
			}
			else if (!Mathf.IsZeroApprox(settings.hallWidth) || !Mathf.IsZeroApprox(settings.hallRotationStrength)) // Process hall width
			{
				float positionTracking = Mathf.Clamp(PathFollower.GlobalPlayerPositionDelta.X, -settings.hallWidth, settings.hallWidth);
				data.blendData.HallSmoothDamp(positionTracking, SnapFlag);

				data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X; // Recenter
				data.horizontalTrackingOffset += data.blendData.hallPosition; // Add clamped position tracking

				if (!Mathf.IsZeroApprox(settings.hallRotationStrength) && Mathf.Abs(delta.X) > settings.hallWidth)
				{
					delta.X -= Mathf.Sign(delta.X) * settings.hallWidth;
					data.secondaryYawTracking = -delta.Flatten().AngleTo(Vector2.Down) * settings.hallRotationStrength;
				}
			}

			float targetPitchTracking = 0.0f;
			if (settings.verticalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
			{
				// Stay on the floor
				data.verticalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.Y;

				if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate) // Rotational tracking
				{
					delta.X = 0; // Ignore x axis for pitch tracking
					data.pitchTracking = delta.Normalized().AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);
					targetPitchTracking = data.pitchTracking;
				}
			}

			data.pitchTracking = targetPitchTracking;

			// Recalculate position after applying rotational tracking
			data.CalculatePosition(Character.CenterPosition);
			data.precalculatedPosition = AddTrackingOffset(data.precalculatedPosition, data);

			if (IsLockonCameraActive && LockonTarget != null)
			{
				globalDelta = LockonTarget.GlobalPosition.Lerp(Character.CenterPosition, .5f) - data.precalculatedPosition;
				delta = data.offsetBasis.Inverse() * globalDelta;
				delta.X = 0; // Ignore x axis for pitch tracking
				data.blendData.lockonPitchTracking = delta.Normalized().AngleTo(delta.RemoveVertical().Normalized()) * Mathf.Sign(delta.Y);
			}
			data.pitchTracking += data.blendData.lockonPitchTracking * lockonTargetBlend;
		}

		if (!data.blendData.WasInitialized)
			data.blendData.WasInitialized = true;

		return data;
	}

	private struct CameraPositionData
	{
		/// <summary> Rotation data used for offset calculation. </summary>
		public Basis offsetBasis;
		public void CalculateBasis()
		{
			offsetBasis = Basis.Identity.Rotated(Vector3.Up, Mathf.Pi);
			offsetBasis = offsetBasis.Rotated(Vector3.Up, blendData.yawAngle);
			offsetBasis = offsetBasis.Rotated(offsetBasis.X.Normalized(), blendData.pitchAngle);
			offsetBasis = offsetBasis.Rotated(offsetBasis.Z.Normalized(), blendData.tiltAngle);
		}

		/// <summary> Yaw rotation data used for tracking. </summary>
		public float yawTracking;
		/// <summary> Secondary yaw rotation data that doesn't influence controls. </summary>
		public float secondaryYawTracking;
		/// <summary> Pitch rotation data used for tracking. </summary>
		public float pitchTracking;

		/// <summary> How much to move camera for horizontal tracking. </summary>
		public float horizontalTrackingOffset;
		/// <summary> How much to move camera for vertical tracking. </summary>
		public float verticalTrackingOffset;

		/// <summary> Only used when blending with a static camera. </summary>
		public Vector3 precalculatedPosition;
		public void CalculatePosition(Vector3 referencePosition)
		{
			precalculatedPosition = -offsetBasis.Z.Normalized() * blendData.distance;
			precalculatedPosition += referencePosition;
		}

		/// <summary> Reference to the CameraBlendData being used. </summary>
		public CameraBlendData blendData;
	}
	#endregion

	#region Free Cam
	private float freecamMovespeed = 20;
	private Vector3 freecamMovementVector;
	private Vector3 freecamVelocity;
	private const float MOUSE_SENSITIVITY = .1f;
	private const float FREE_CAM_POSITION_SMOOTHING = .1f;

	private bool isFreeCamActive;
	private bool isFreeCamRotating;
	private bool isFreeCamTilting;

	private bool isFreeCamLocked;
	private Vector3 freeCamLockedPosition;
	private Vector3 freeCamLockedRotation;

	private void UpdateFreeCam()
	{
		bool wasFreeCamActive = isFreeCamActive;

		if (Input.IsActionJustPressed("debug_free_cam_reset"))
			isFreeCamActive = false;

		isFreeCamRotating = Input.IsMouseButtonPressed(MouseButton.Right);
		isFreeCamTilting = Input.IsMouseButtonPressed(MouseButton.Middle);
		if (isFreeCamRotating || isFreeCamTilting)
		{
			isFreeCamActive = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if (isFreeCamActive != wasFreeCamActive)
			ToggleFreeCam();

		if (!isFreeCamActive) return;

		if (Input.IsActionJustPressed("debug_free_cam_lock"))
		{
			isFreeCamLocked = !isFreeCamLocked;
			freeCamLockedPosition = FreeCamRoot.GlobalPosition;
			freeCamLockedRotation = new(Camera.RotationDegrees.X, FreeCamRoot.GlobalRotationDegrees.Y, Camera.RotationDegrees.Z);
			GD.Print($"Free cam lock set to {isFreeCamLocked}.");
		}

		float targetMoveSpeed = freecamMovespeed;

		if (Input.IsKeyPressed(Key.Shift))
			targetMoveSpeed *= 2;
		else if (Input.IsKeyPressed(Key.Ctrl))
			targetMoveSpeed *= .5f;

		Vector3 targetDirection = new();

		if (Input.IsKeyPressed(Key.E))
			targetDirection += Camera.Up();
		if (Input.IsKeyPressed(Key.Q))
			targetDirection += Camera.Down();
		if (Input.IsKeyPressed(Key.W))
			targetDirection += Camera.Back();
		if (Input.IsKeyPressed(Key.S))
			targetDirection += Camera.Forward();
		if (Input.IsKeyPressed(Key.D))
			targetDirection += Camera.Right();
		if (Input.IsKeyPressed(Key.A))
			targetDirection += Camera.Left();

		freecamMovementVector = freecamMovementVector.SmoothDamp(targetDirection * targetMoveSpeed, ref freecamVelocity, FREE_CAM_POSITION_SMOOTHING);
		FreeCamRoot.GlobalTranslate(freecamMovementVector * PhysicsManager.normalDelta);

		if (isFreeCamLocked)
			UpdateFreeCamData(freeCamLockedPosition, freeCamLockedRotation);
	}

	private void ToggleFreeCam()
	{
		// Update visibility
		bool showCamera = isFreeCamActive && DebugManager.Instance.DrawDebugCam;
		debugMesh.Visible = showCamera;
		PathFollower.Visible = showCamera;
		Character.PathFollower.Visible = showCamera;

		if (isFreeCamActive)
		{
			Camera.Rotation = FreeCamRoot.GlobalRotation.RemoveVertical();
			FreeCamRoot.GlobalRotation = new(0, FreeCamRoot.GlobalRotation.Y, 0);
		}
		else
		{
			isFreeCamLocked = false;
			isFreeCamActive = isFreeCamRotating = false;
			Camera.Transform = Transform3D.Identity;
			FreeCamRoot.Transform = Transform3D.Identity;
			GD.Print("Free cam disabled.");
		}
	}

	public void UpdateFreeCamData(Vector3 position, Vector3 rotation)
	{
		if (!isFreeCamActive) return;

		FreeCamRoot.GlobalPosition = position;
		FreeCamRoot.GlobalRotationDegrees = Vector3.Up * rotation.Y;
		Camera.RotationDegrees = rotation.RemoveVertical();
	}

	private void ReceiveInput(InputEvent e)
	{
		if (!isFreeCamActive) return;

		if (e is InputEventMouseMotion)
		{
			if (isFreeCamRotating)
			{
				FreeCamRoot.RotateObjectLocal(Vector3.Up, Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.X) * MOUSE_SENSITIVITY);
				Camera.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(-(e as InputEventMouseMotion).Relative.Y) * MOUSE_SENSITIVITY);
			}
			else if (isFreeCamTilting)
			{
				Camera.RotateObjectLocal(Vector3.Forward, Mathf.DegToRad((e as InputEventMouseMotion).Relative.X) * MOUSE_SENSITIVITY);
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
	}
	#endregion
}

public partial class CameraBlendData : GodotObject
{
	/// <summary> Use crossfading? </summary>
	public bool IsCrossfadeEnabled { get; set; }

	/// <summary> Ratio [0 <-> 1] of how much influence this blend has. </summary>
	public float LinearInfluence { get; private set; }
	/// <summary> Influence, smoothed with Mathf.Smoothstep. </summary>
	public float SmoothedInfluence { get; private set; }
	/// <summary> Actual amount to blend each frame. </summary>
	public float BlendSpeed { get; private set; }

	/// <summary> Hall tracking position. </summary>
	public float hallPosition;
	/// <summary> Hall tracking velocity. </summary>
	private float hallVelocity;
	public const float HALL_SMOOTHING = 10.0f;
	public void HallSmoothDamp(float target, bool snap)
	{
		if (snap || !WasInitialized)
		{
			hallPosition = target;
			hallVelocity = 0;
			return;
		}

		hallPosition = ExtensionMethods.SmoothDamp(hallPosition, target, ref hallVelocity, HALL_SMOOTHING * PhysicsManager.physicsDelta);
	}

	/// <summary> [0 -> 1] Blend between offset and sample. </summary>
	public float SampleBlend { get; set; }

	/// <summary> How long blending takes in seconds. </summary>
	public float BlendTime { get; set; }
	/// <summary> Camera's static position. Only used when CameraSettingsResource.useStaticPosition is true. </summary>
	public Vector3 StaticPosition { get; set; }

	/// <summary> Camera's static rotation. Only used when CameraSettingsResource.useStaticRotation is true. </summary>
	public Basis RotationBasis { get; set; }

	/// <summary> Current fov. </summary>
	public float Fov;

	/// <summary> Current pitch angle. </summary>
	public float pitchAngle;
	/// <summary> Current yaw angle. </summary>
	public float yawAngle;
	/// <summary> Current tilt angle. </summary>
	public float tiltAngle;

	/// <summary> Last frame's lockon pitch tracking </summary>
	public float lockonPitchTracking;

	/// <summary> How far the camera should be. </summary>
	public float distance;
	/// <summary> Distance smoothdamp velocity. </summary>
	private float distanceVelocity;
	public const float DistanceSmoothing = 10.0f;

	public void DistanceSmoothDamp(float target, bool movingBackwards, bool snap)
	{
		if (snap || !WasInitialized || (movingBackwards && target < distance))
		{
			distance = target;
			distanceVelocity = 0;
			return;
		}

		distance = ExtensionMethods.SmoothDamp(distance, target, ref distanceVelocity, DistanceSmoothing * PhysicsManager.physicsDelta);
	}

	/// <summary> Has this blend data been processed before? </summary>
	public bool WasInitialized { get; set; }

	/// <summary> CameraSettingsResource for this camera setting. </summary>
	public CameraSettingsResource SettingsResource { get; set; }
	/// <summary> Reference to the cameraTrigger, if it exists. </summary>
	public CameraTrigger Trigger { get; set; }

	public void SetInfluence(float rawInfluence)
	{
		LinearInfluence = rawInfluence;
		SmoothedInfluence = Mathf.SmoothStep(0.0f, 1.0f, rawInfluence);
	}

	public void CalculateBlendSpeed() => BlendSpeed = 1f / BlendTime;
}