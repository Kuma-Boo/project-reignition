using Godot;
using Project.Core;
using Project.Gameplay.Triggers;
using System.Collections.Generic;

namespace Project.Gameplay;

public enum CameraTransitionType
{
	Time,
	Crossfade,
}

/// <summary>
/// Follows the player based on the settings provided from CameraSettingsResource.cs
/// </summary>
public partial class PlayerCameraController : Node3D
{
	[Signal]
	public delegate void StartCompletionEventHandler();

	public const float DefaultFov = 70;

	[ExportGroup("Components")]
	[Export] public Camera3D Camera { get; private set; }
	[Export] public Node3D FreeCamRoot { get; private set; }
	[Export] private Node3D cameraRoot;
	[Export] private Node3D debugMesh;
	public Vector2 ConvertToScreenSpace(Vector3 worldSpace) => Camera.UnprojectPosition(worldSpace);
	public bool IsOnScreen(Vector3 worldSpace) => Camera.IsPositionInFrustum(worldSpace);
	public bool IsBehindCamera(Vector3 worldSpace) => Camera.IsPositionBehind(worldSpace);

	/// <summary> Camera's pathfollower. Different than Player.PathFollower. </summary>
	[Export] public PlayerPathController PathFollower { get; private set; }
	[Export] private Node3D sampler;

	private readonly StringName ShaderPlayerScreenPosition = new("player_screen_position");

	private PlayerController Player;
	public void Initialize(PlayerController player)
	{
		if (Engine.IsEditorHint()) return;

		Player = player;
		PathFollower.Initialize(player);
		LimitToPathDistance = !PathFollower.Loop;

		// Apply default settings
		CameraSettingsResource targetSettings = (StageSettings.Instance?.InitialCameraSettings) ?? defaultSettings;

		SnapXform();
		UpdateCameraVisibility();
		UpdateCameraSettings(new()
		{
			SettingsResource = targetSettings,
		});

		motionBlurMaterial.SetShaderParameter(OpacityParameter, 0);

		sampler.GetParent().RemoveChild(sampler);
		StageSettings.Instance.AddChild(sampler);
		if (StageSettings.Instance.Data.CompletionAnimation == LevelDataResource.CompletionAnimationType.ThumbsUp)
			StageSettings.Instance.LevelSuccess += StartThumbsUpCamera;

		Runtime.Instance.EventInputed += ReceiveInput;
	}

	public override void _EnterTree() => DebugManager.Instance.CameraVisibilityToggled += UpdateCameraVisibility;
	public override void _ExitTree() => DebugManager.Instance.CameraVisibilityToggled -= UpdateCameraVisibility;

	public void Respawn()
	{
		SnapXform();

		// Revert camera settings
		CameraSettingsResource resource;
		if (Player.IsDebugRespawn)
			resource = DebugManager.Instance.DebugCheckpoint.CameraSettings;
		else
			resource = StageSettings.Instance.CurrentCheckpoint.CameraSettings;

		UpdateCameraSettings(new()
		{
			SettingsResource = resource,
		});

		SnapFlag = true;
	}

	public override void _PhysicsProcess(double _)
	{
		if (GetTree().Paused)
			return;

		UpdatePathFollower();
		reverseBlendRotationAmount = CalculateReversePathBlendRotation();

		// Don't update the camera when the player is defeated from a DeathTrigger
		if (IsDefeatFreezeActive)
		{
			if (Player.IsDefeated)
				return;

			IsDefeatFreezeActive = false;
		}

		UpdateGameplayCamera();
		UpdateScreenShake();
		UpdateMotionBlur();
	}

	public override void _Process(double _)
	{
		if (OS.IsDebugBuild())
			UpdateFreeCam();
	}

	public float ReversePathInfluence { get; set; }
	public int ReversePathRotationDirection { get; set; }
	private float reverseBlendRotationAmount;
	private float pathBlend = 1.0f;
	private float pathBlendSmoothed = 1.0f;
	private float pathBlendSpeed;
	public void UpdatePathBlendSpeed(float speed)
	{
		if (Mathf.IsZeroApprox(speed))
		{
			pathBlend = pathBlendSmoothed = 1.0f;
			return;
		}

		pathBlend = pathBlendSmoothed = 0.0f;
		pathBlendSpeed = speed;
	}

	private void UpdatePathFollower()
	{
		if (SnapFlag)
			pathBlend = pathBlendSmoothed = 1.0f;

		PathFollower.Resync();
		sampler.GlobalPosition = sampler.GlobalPosition.Lerp(PathFollower.GlobalPosition, pathBlendSmoothed);
		sampler.GlobalBasis = sampler.GlobalBasis.Orthonormalized().Slerp(PathFollower.GlobalBasis.Orthonormalized(), pathBlendSmoothed);

		if (Mathf.IsEqualApprox(pathBlend, 1.0f))
			return;

		pathBlend = Mathf.MoveToward(pathBlend, 1.0f, pathBlendSpeed * PhysicsManager.physicsDelta);
		pathBlendSmoothed = Mathf.SmoothStep(0.0f, 1.0f, pathBlend);
	}

	private float CalculateReversePathBlendRotation()
	{
		float target = PathFollower.IsReversingPath ? Mathf.Pi * ReversePathRotationDirection : 0;
		if (Mathf.IsZeroApprox(ReversePathInfluence))
			return target;

		float starting = PathFollower.IsReversingPath ? 0 : Mathf.Pi * ReversePathRotationDirection;
		ReversePathInfluence = 1f - pathBlendSmoothed;
		return Mathf.LerpAngle(starting, target, pathBlendSmoothed);
	}

	/// <summary> Enabled when the camera should freeze due to a DeathTrigger. </summary>
	public bool IsDefeatFreezeActive { get; set; }
	/// <summary> Used to focus onto multi-HP enemies, bosses, etc. Not to be confused with CharacterLockon.Target. </summary>
	public Node3D LockonTarget { get; private set; }
	private bool IsLockonCameraActive => LockonTarget != null || Player.IsHomingAttacking || Player.IsBouncing;
	/// <summary> [0 -> 1] ratio of how much to use the lockon camera. </summary>
	private float lockonBlend;
	private float lockonBlendVelocity;
	/// <summary> [0 -> 1] ratio of how much to focus onto LockonTarget. </summary>
	private float lockonTargetBlend;
	private float lockonTargetBlendVelocity;
	/// <summary> Amount when blending between different lockon targets. Reset whenever the lockon target changes. </summary>
	private float lockonTargetTransitionBlend;
	private float lockonTargetTransitionBlendVelocity;
	/// <summary> Snappier blend when lockon is active to keep things in frame. </summary>
	private const float LockonBlendInSmoothing = 5.0f;
	/// <summary> More smoothing/slower blend when resetting lockonBlend. </summary>
	private const float LockonBlendOutSmoothing = 20.0f;
	/// <summary> How much extra distance to add when performing a homing attack. </summary>
	private const float LockonDistance = 3f;
	/// <summary> How much extra distance to add when performing a jump dash. </summary>
	private const float JumpDashDistance = 2f;
	public void SetLockonTarget(Node3D lockonTarget)
	{
		if (LockonTarget == lockonTarget) return;

		LockonTarget = lockonTarget;
		lockonTargetTransitionBlend = 0;
	}

	private void UpdateLockonTarget()
	{
		float targetBlend = 0;
		float smoothing = LockonBlendOutSmoothing;

		// Lockon is active
		if (!ActiveSettings.ignoreHomingAttack && IsLockonCameraActive)
		{
			targetBlend = 1;
			smoothing = LockonBlendInSmoothing;
		}

		lockonBlend = ExtensionMethods.SmoothDamp(lockonBlend, targetBlend, ref lockonBlendVelocity, smoothing * PhysicsManager.physicsDelta);
		lockonTargetBlend = ExtensionMethods.SmoothDamp(lockonTargetBlend, LockonTarget == null ? 0f : 1f, ref lockonTargetBlendVelocity, LockonBlendOutSmoothing * PhysicsManager.physicsDelta);

		if (LockonTarget != null)
			lockonTargetTransitionBlend = ExtensionMethods.SmoothDamp(lockonTargetTransitionBlend, 1f, ref lockonTargetTransitionBlendVelocity, LockonBlendInSmoothing * PhysicsManager.physicsDelta);
	}

	#region Gameplay Camera
	/// <summary> Skips smoothing for the current frame. </summary>
	public bool SnapFlag { get; set; }
	/// <summary> Determines whether the camera's distance will be limited by its path. </summary>
	public bool LimitToPathDistance { get; set; }

	/// <summary> Default camera settings to use when nothing is set. </summary>
	[Export] public CameraSettingsResource defaultSettings;
	/// <summary> Reference to active CameraBlendData. </summary>
	public CameraBlendData ActiveBlendData => CameraBlendList[^1];
	/// <summary> Reference to active CameraSettingsResource. </summary>
	public CameraSettingsResource ActiveSettings => ActiveBlendData.SettingsResource;
	/// <summary> A list of all camera settings that are influencing camera. </summary>
	private readonly List<CameraBlendData> CameraBlendList = [];

	/// <summary> Camera setting to use when performing the thumbs-up animation. </summary>
	[Export] public CameraSettingsResource thumbsUpSettings;

	public bool UsingCompletionCamera { get; private set; }
	public void StartCompletionCamera()
	{
		EmitSignal(SignalName.StartCompletion);
		UsingCompletionCamera = true;
	}

	public void StartThumbsUpCamera()
	{
		UpdateCameraSettings(new()
		{
			BlendTime = 1f,
			SettingsResource = thumbsUpSettings,
			TransitionType = CameraTransitionType.Time,
			Trigger = null
		});
	}

	/// <summary> Changes the current camera settings. </summary>
	public void UpdateCameraSettings(CameraBlendData data, bool enableXformBlend = false)
	{
		if (UsingCompletionCamera) return;
		if (data.SettingsResource == null) return; // Invalid data

		if (CameraBlendList.Count != 0 && ActiveSettings == data.SettingsResource &&
			!(data.SettingsResource.copyPosition || data.SettingsResource.copyRotation) &&
			data.Trigger?.UseDistanceBlending == false)
		{
			// When the same data is used for multiple different triggers (except for distance triggers)
			ActiveBlendData.Trigger = data.Trigger; // Simply update the current trigger
			return;
		}

		if (Mathf.IsZeroApprox(data.BlendTime)) // Cut transition
		{
			SnapFlag = true;
			if (enableXformBlend)
				StartXformBlend();
		}
		else if (data.TransitionType == CameraTransitionType.Crossfade) // Crossfade transition
		{
			StartCrossfade(1.0f / data.BlendTime);
			SnapFlag = true;
			if (enableXformBlend)
				StartXformBlend();
		}
		else if (data.TransitionType == CameraTransitionType.Time)
		{
			data.CalculateBlendSpeed(); // Cache blend speed so we don't have to do it every frame
		}

		// Add current data to blend list
		CameraBlendList.Add(data);
	}

	/// <summary> Update the transition timer. </summary>
	private void UpdateTransitionTimer()
	{
		// Clear all lists (except active blend) when snapping
		if (SnapFlag)
		{
			// Remove all blend data except the last 2 active ones (for blending purposes)
			int startingIndex = CameraBlendList.Count - 2;
			if (CameraBlendList[^1].UseDistanceBlending)
				startingIndex--;

			for (int i = startingIndex; i >= 0; i--)
			{
				CameraBlendList[i].Free(); // Prevent memory leak
				CameraBlendList.RemoveAt(i);
			}

			CameraBlendList[0].SetInfluence(1);
			if (CameraBlendList[^1].UseDistanceBlending)
				CameraBlendList[^1].SetInfluence(1);
			return;
		}

		for (int i = CameraBlendList.Count - 1; i >= 0; i--)
			UpdateCameraBlendInfluence(i);
	}

	/// <summary> Update the influence of a particular blend. </summary>
	private void UpdateCameraBlendInfluence(int blendIndex)
	{
		// Removes completed normal blends
		if (!CameraBlendList[blendIndex].UseDistanceBlending &&
			blendIndex < CameraBlendList.Count - 1 &&
			Mathf.IsEqualApprox(CameraBlendList[blendIndex + 1].LinearInfluence, 1.0f))
		{
			CameraBlendList[blendIndex].Free();
			CameraBlendList.RemoveAt(blendIndex);
			return;
		}

		// Remove completed distance blends
		if (CameraBlendList[blendIndex].UseDistanceBlending &&
			ActiveBlendData.Trigger != CameraBlendList[blendIndex].Trigger &&
			Mathf.IsEqualApprox(ActiveBlendData.LinearInfluence, 1.0f))
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
		UpdateCameraBlends();
		RenderingServer.GlobalShaderParameterSet(ShaderPlayerScreenPosition, ConvertToScreenSpace(Player.CenterPosition) / Runtime.ScreenSize);

		if (SnapFlag) // Reset flag after camera was updated
			SnapFlag = false;
	}

	private void UpdateCameraBlends()
	{
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

			/*	For distance blends, simulate two cameras at once, then blend between them.
				Note that since distance blend datas come in pairs of two, it's safe to access
				the Trigger even after we increment i.
			*/
			if (CameraBlendList[i].Trigger?.UseDistanceBlending == true)
			{
				i++; // Iterate so we can simulate the next item in the blend list
				CameraPositionData secondaryData = SimulateCamera(i);
				float secondaryInfluence = CameraBlendList[i].Trigger.CalculateInfluence();
				iData.BlendWith(secondaryData, secondaryInfluence);

				float blendedDistance = Mathf.Lerp(iData.blendData.distance, secondaryData.blendData.distance, secondaryInfluence);
				distance = Mathf.Lerp(distance, blendedDistance, CameraBlendList[i].SmoothedInfluence);
				float blendedFov = Mathf.Lerp(iData.blendData.Fov, secondaryData.blendData.Fov, secondaryInfluence);
				fov = Mathf.Lerp(fov, blendedFov, CameraBlendList[i].SmoothedInfluence);

				Vector2 blendedViewportOffset = iData.blendData.SettingsResource.viewportOffset;
				blendedViewportOffset = blendedViewportOffset.Lerp(secondaryData.blendData.SettingsResource.viewportOffset, secondaryInfluence);
				viewportOffset = viewportOffset.Lerp(blendedViewportOffset, CameraBlendList[i].SmoothedInfluence);
			}
			else
			{
				distance = Mathf.Lerp(distance, iData.blendData.distance, CameraBlendList[i].SmoothedInfluence);
				fov = Mathf.Lerp(fov, iData.blendData.Fov, CameraBlendList[i].SmoothedInfluence);

				viewportOffset = viewportOffset.Lerp(CameraBlendList[i].SettingsResource.viewportOffset, CameraBlendList[i].SmoothedInfluence);
			}

			data.offsetBasis = data.offsetBasis.Slerp(iData.offsetBasis, CameraBlendList[i].SmoothedInfluence);

			data.precalculatedPosition = data.precalculatedPosition.Lerp(iData.precalculatedPosition, CameraBlendList[i].SmoothedInfluence);

			data.yawTracking = Mathf.LerpAngle(data.yawTracking, iData.yawTracking, CameraBlendList[i].SmoothedInfluence);
			data.secondaryYawTracking = Mathf.LerpAngle(data.secondaryYawTracking, iData.secondaryYawTracking, CameraBlendList[i].SmoothedInfluence);
			data.pitchTracking = Mathf.Lerp(data.pitchTracking, iData.pitchTracking, CameraBlendList[i].SmoothedInfluence);

			data.horizontalTrackingOffset = Mathf.Lerp(data.horizontalTrackingOffset, iData.horizontalTrackingOffset, CameraBlendList[i].SmoothedInfluence);
			data.verticalTrackingOffset = Mathf.Lerp(data.verticalTrackingOffset, iData.verticalTrackingOffset, CameraBlendList[i].SmoothedInfluence);

			staticBlendRatio = Mathf.Lerp(staticBlendRatio, CameraBlendList[i].SettingsResource.copyPosition ? 1 : 0, CameraBlendList[i].SmoothedInfluence);
		}

		// Recalculate non-static camera positions for better transition rotations.
		Vector3 position = data.offsetBasis.Z.Normalized() * distance;
		position += Player.CenterPosition;

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

		if (!isFreeCamActive || !isFreeCamLocked) // Only update camera transform when free cam isn't locked
			cameraRoot.GlobalTransform = cameraTransform; // Update transform

		Camera.Fov = fov; // Update fov
	}

	/// <summary> Previous xform angle used right before the last camera change. </summary>
	private float cachedXFormAngle;
	private float xformBlend = 1;
	private readonly float XformSmoothing = 1.5f;

	/// <summary> Starts blending the xform angle. </summary>
	private void StartXformBlend()
	{
		xformBlend = 0;
		cachedXFormAngle = Player.Controller.XformAngle;
	}

	/// <summary> Blends xform angles for smoother inputs between camera cuts. </summary>
	private void UpdateInputXForm(Basis cameraBasis)
	{
		float targetXformAngle = ExtensionMethods.CalculateForwardAngle(cameraBasis.Z, cameraBasis.Y);

		// Snap xform blend when no input is held
		if (Mathf.IsZeroApprox(Player.Controller.GetInputStrength()) ||
			(Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe &&
			Mathf.IsZeroApprox(Player.Controller.InputHorizontal)))
		{
			SnapXform();
		}

		xformBlend = Mathf.MoveToward(xformBlend, 1, XformSmoothing * PhysicsManager.physicsDelta);
		Player.Controller.XformAngle = Mathf.LerpAngle(cachedXFormAngle, targetXformAngle, xformBlend);
	}

	public void SnapXform() => xformBlend = 1;

	private Vector3 AddTrackingOffset(Vector3 position, CameraPositionData data)
	{
		position += sampler.Right() * data.horizontalTrackingOffset;
		position += sampler.Up() * data.verticalTrackingOffset;
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

		if (settings.copyPosition)
			data = SimulateStaticCamera(settings, ref data);
		else
			data = SimulateDynamicCamera(settings, ref data);

		data.blendData.Fov = DefaultFov;
		if (!Mathf.IsZeroApprox(settings.targetFOV))
			data.blendData.Fov = settings.targetFOV;

		if (!data.blendData.WasInitialized)
			data.blendData.WasInitialized = true;

		return data;
	}

	private CameraPositionData SimulateStaticCamera(CameraSettingsResource settings, ref CameraPositionData data)
	{
		float targetPitchAngle = settings.pitchAngle;
		float targetYawAngle = settings.yawAngle;

		data.precalculatedPosition = data.blendData.Position;

		if (settings.copyRotation)
		{
			data.offsetBasis = data.blendData.RotationBasis.Orthonormalized();
			return data;
		}

		Vector3 delta = Player.CenterPosition - data.precalculatedPosition;
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

		return data;
	}

	private CameraPositionData SimulateDynamicCamera(CameraSettingsResource settings, ref CameraPositionData data)
	{
		// Calculate distance
		float targetDistance = CalculateDistance(settings);
		data.blendData.DistanceSmoothDamp(targetDistance, Player.IsMovingBackward, SnapFlag);

		CalculateRotation(settings, ref data);

		// Update Tracking
		// Calculate position for tracking calculations
		data.CalculateBasis();
		data.CalculatePosition(sampler.GlobalPosition);

		Vector3 globalDelta = Player.CenterPosition - data.precalculatedPosition;
		Vector3 localDelta = data.offsetBasis.Inverse() * globalDelta;

		if (settings.horizontalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
		{
			data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X;

			if (settings.horizontalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate)
				data.secondaryYawTracking = -localDelta.Normalized().Flatten().AngleTo(Vector2.Down);
		}
		else if (!Mathf.IsZeroApprox(settings.hallWidth) || !Mathf.IsZeroApprox(settings.hallRotationStrength)) // Process hall width
		{
			float positionTracking = Mathf.Clamp(PathFollower.GlobalPlayerPositionDelta.X, -settings.hallWidth, settings.hallWidth);
			data.blendData.HallSmoothDamp(positionTracking, SnapFlag);

			data.horizontalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.X; // Recenter
			data.horizontalTrackingOffset += data.blendData.hallPosition; // Add clamped position tracking

			if (!Mathf.IsZeroApprox(settings.hallRotationStrength) && Mathf.Abs(localDelta.X) > settings.hallWidth)
			{
				localDelta.X -= Mathf.Sign(localDelta.X) * settings.hallWidth;
				data.secondaryYawTracking = -localDelta.Flatten().AngleTo(Vector2.Down) * settings.hallRotationStrength;
			}
		}

		float targetPitchTracking = 0.0f;
		if (settings.verticalTrackingMode != CameraSettingsResource.TrackingModeEnum.Move)
		{
			// Stay on the floor
			data.verticalTrackingOffset = -PathFollower.GlobalPlayerPositionDelta.Y;

			if (settings.verticalTrackingMode == CameraSettingsResource.TrackingModeEnum.Rotate) // Rotational tracking
			{
				localDelta.X = 0; // Ignore x axis for pitch tracking
				localDelta.Y -= settings.viewportOffset.Y;
				data.pitchTracking = localDelta.Normalized().AngleTo(localDelta.RemoveVertical().Normalized()) * Mathf.Sign(localDelta.Y);
				targetPitchTracking = data.pitchTracking;
			}
		}

		data.pitchTracking = targetPitchTracking;

		// Recalculate position after applying rotational tracking
		data.CalculatePosition(Player.CenterPosition);
		data.precalculatedPosition = AddTrackingOffset(data.precalculatedPosition, data);

		if (!settings.ignoreHomingAttack && IsLockonCameraActive && LockonTarget != null)
		{
			globalDelta = LockonTarget.GlobalPosition.Lerp(Player.CenterPosition, .5f) - data.precalculatedPosition;
			localDelta = data.offsetBasis.Inverse() * globalDelta;
			localDelta.X = 0; // Ignore x axis for pitch tracking
			float targetLockonPitchTracking = localDelta.Normalized().AngleTo(localDelta.RemoveVertical().Normalized()) * Mathf.Sign(localDelta.Y);
			data.blendData.lockonPitchTracking = Mathf.Lerp(data.blendData.lockonPitchTracking, targetLockonPitchTracking, lockonTargetTransitionBlend);
		}
		data.pitchTracking += data.blendData.lockonPitchTracking * lockonTargetBlend;

		return data;
	}

	private float CalculateDistance(CameraSettingsResource settings)
	{
		float targetDistance = settings.distance;
		if (Player.IsMovingBackward)
			targetDistance += settings.backstepDistance;

		if (!settings.ignoreHomingAttack)
		{
			if (IsLockonCameraActive)
				targetDistance += LockonDistance;
			else if (Player.IsJumpDashing)
				targetDistance += JumpDashDistance;
		}

		if (PathFollower.Progress < targetDistance &&
			!PathFollower.Loop &&
			LimitToPathDistance)
		{
			targetDistance = PathFollower.Progress;
		}

		return targetDistance;
	}

	private void CalculateRotation(CameraSettingsResource settings, ref CameraPositionData data)
	{
		float targetYawAngle = settings.yawAngle;
		float targetPitchAngle = settings.pitchAngle;
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
		{
			sampledTargetYawAngle += ExtensionMethods.CalculateForwardAngle(sampledForward);
			targetYawAngle += ExtensionMethods.CalculateForwardAngle(sampler.Forward(), sampler.Up());
		}

		if (settings.pitchOverrideMode == CameraSettingsResource.OverrideModeEnum.Add)
		{
			sampledTargetPitchAngle += sampledForward.AngleTo(sampledForward.RemoveVertical().Normalized()) * Mathf.Sign(sampledForward.Y);
			Vector3 forwardDirection = sampler.Forward();
			if (Mathf.Abs(forwardDirection.Dot(Vector3.Up)) > 0.9f)
				forwardDirection = sampler.Up() * Mathf.Sign(-forwardDirection.Y);

			targetPitchAngle += sampler.Forward().AngleTo(forwardDirection.RemoveVertical().Normalized()) * Mathf.Sign(sampler.Forward().Y);
		}

		// Calculate slope rotation blending
		switch (settings.distanceCalculationMode)
		{
			case CameraSettingsResource.DistanceModeEnum.Auto: // Fixes slope changes
				data.blendData.SampleBlend = Mathf.Lerp(data.blendData.SampleBlend, sampledForward.Y < sampler.Forward().Y ? 1.0f : 0.0f, .1f);
				break;
			case CameraSettingsResource.DistanceModeEnum.Sample:
				data.blendData.SampleBlend = 1.0f;
				break;
			case CameraSettingsResource.DistanceModeEnum.Offset:
				data.blendData.SampleBlend = 0.0f;
				break;
		}

		// Fix rotated sampling cameras
		data.blendData.yawAngle = sampledTargetYawAngle;
		data.blendData.pitchAngle = sampledTargetPitchAngle;
		data.CalculateBasis();
		int yawSamplingFix = Mathf.Sign(sampledForward.Dot(-data.offsetBasis.Z));
		sampledTargetPitchAngle *= yawSamplingFix;

		// Interpolate angles
		data.blendData.yawAngle = Mathf.LerpAngle(targetYawAngle, sampledTargetYawAngle, data.blendData.SampleBlend) + reverseBlendRotationAmount;
		data.blendData.pitchAngle = Mathf.Lerp(targetPitchAngle, sampledTargetPitchAngle, data.blendData.SampleBlend);
		PathFollower.TiltEnabled = settings.followPathTilt;
		if (settings.followPathTilt) // Calculate tilt
			data.blendData.tiltAngle = sampler.Right().SignedAngleTo(-PathFollower.SideAxis, sampler.Forward()) * yawSamplingFix;
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

		public void BlendWith(CameraPositionData data, float influence)
		{
			offsetBasis = offsetBasis.Slerp(data.offsetBasis, influence);
			precalculatedPosition = precalculatedPosition.Lerp(data.precalculatedPosition, influence);

			yawTracking = Mathf.LerpAngle(yawTracking, data.yawTracking, influence);
			secondaryYawTracking = Mathf.LerpAngle(secondaryYawTracking, data.secondaryYawTracking, influence);
			pitchTracking = Mathf.Lerp(pitchTracking, data.pitchTracking, influence);

			horizontalTrackingOffset = Mathf.Lerp(horizontalTrackingOffset, data.horizontalTrackingOffset, influence);
			verticalTrackingOffset = Mathf.Lerp(verticalTrackingOffset, data.verticalTrackingOffset, influence);
		}
	}
	#endregion

	#region Effects
	[ExportGroup("Effects")]
	[Export]
	private ShaderMaterial motionBlurMaterial;
	private Vector3 previousCameraPosition;
	private Quaternion previousCameraRotation;

	public int motionBlurRequests;

	private readonly float TimeBreakMotionBlurStrength = 2.0f;
	private readonly float MotionBlurStrength = .5f;
	private readonly string OpacityParameter = "opacity";
	private readonly string LinearVelocityParameter = "linear_velocity";
	private readonly string AngularVelocityParameter = "angular_velocity";

	private void UpdateMotionBlur()
	{
		if (motionBlurMaterial == null || !SaveManager.Config.useMotionBlur)
			return;

		float opacity = (float)motionBlurMaterial.GetShaderParameter(OpacityParameter);
		opacity = Mathf.MoveToward(opacity, motionBlurRequests == 0 ? 0 : 1, 5.0f * PhysicsManager.physicsDelta);
		motionBlurMaterial.SetShaderParameter(OpacityParameter, opacity);

		motionBlurMaterial.SetShaderParameter(LinearVelocityParameter, CalculateLinearVelocity());
		motionBlurMaterial.SetShaderParameter(AngularVelocityParameter, CalculateAngularVelocity());

		previousCameraPosition = Camera.GlobalPosition;
		previousCameraRotation = Camera.GlobalBasis.GetRotationQuaternion();
	}

	private Vector3 CalculateLinearVelocity()
	{
		Vector3 velocity = Camera.GlobalPosition - previousCameraPosition;
		if (!Mathf.IsZeroApprox(Engine.TimeScale))
			velocity /= (float)Engine.TimeScale;

		if (Player.Skills.IsTimeBreakActive)
			return velocity * TimeBreakMotionBlurStrength;

		return velocity * MotionBlurStrength;
	}

	private Vector3 CalculateAngularVelocity()
	{
		Quaternion rotation = Camera.GlobalBasis.GetRotationQuaternion();
		Quaternion rotationDifference = rotation - previousCameraRotation;
		Quaternion rotationConjugate = new(-rotation.X, -rotation.Y, -rotation.Z, rotation.W);
		Quaternion angularRotation = rotationDifference * 2.0f * rotationConjugate;
		return new Vector3(angularRotation.X, angularRotation.Y, angularRotation.Z) * MotionBlurStrength;
	}

	public void RequestMotionBlur() => motionBlurRequests++;
	public void UnrequestMotionBlur() => motionBlurRequests = Mathf.Max(motionBlurRequests - 1, 0);

	[Export]
	private TextureRect crossfade;
	[Export]
	private AnimationPlayer crossfadeAnimator;
	public bool IsCrossfading => crossfadeAnimator.IsPlaying();
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

		if (!StageSettings.Instance.IsLevelIngame)
			return;

		// Warp the camera
		SnapFlag = true;
		UpdateGameplayCamera();
	}

	private readonly List<CameraShakeSettings> shakeSettings = [];

	public void StartCameraShake(CameraShakeSettings settings)
	{
		settings.Initialize();
		shakeSettings.Add(settings);
	}

	public void StopRespawnShakes()
	{
		for (int i = shakeSettings.Count - 1; i >= 0; i--)
		{
			if (shakeSettings[i].persistBetweenRespawns)
				continue;

			shakeSettings[i].Dispose();
			shakeSettings.RemoveAt(i);
		}
	}

	/// <summary> Starts a medium camera shake. </summary>
	public void StartMediumCameraShake(float length = .2f)
	{
		StartCameraShake(new()
		{
			magnitude = Vector3.One.RemoveDepth() * .4f,
			intensity = Vector3.One * 50.0f,
			duration = length,
		});
	}

	private void UpdateScreenShake()
	{
		if (!SaveManager.Config.useScreenShake)
			return;

		float screenShakeRatio = PhysicsManager.physicsDelta * SaveManager.Config.screenShake * 0.01f;
		for (int i = shakeSettings.Count - 1; i >= 0; i--)
		{
			if (shakeSettings[i].IsFinished)
			{
				shakeSettings[i].Dispose();
				shakeSettings.RemoveAt(i);
				continue;
			}

			Vector3 rotationAmount = shakeSettings[i].SimulateShake(PhysicsManager.physicsDelta, cameraRoot.GlobalPosition);
			cameraRoot.Rotation += rotationAmount * screenShakeRatio;
		}
	}

	public partial class CameraShakeSettings : GodotObject
	{
		/// <summary> How much the camera rotates. </summary>
		public Vector3 magnitude = Vector3.One;
		/// <summary> How quickly the camera shifts between rotations. </summary>
		public Vector3 intensity = Vector3.One * 50.0f;
		/// <summary> How random phases should increase from each other. </summary>
		public float randomness = 1f;
		/// <summary> Actual value used to sample the sin curve. </summary>
		public Vector3 phaseOffset;
		/// <summary> The origin of the camera's shake. </summary>
		public Vector3 origin;
		/// <summary> Maximum distance before camera shake is ignored. 0 means always shake. </summary>
		public float maximumDistance;

		/// <summary> Should this camera shake continue even after being respawned? </summary>
		public bool persistBetweenRespawns;

		/// <summary> Camera's current time. </summary>
		public float currentTime;
		/// <summary> Camera shake fade in time. </summary>
		public float fadeIn = 0.05f;
		/// <summary> How long camera shake lasts (excluding fade times). </summary>
		public float duration = 0.2f;
		/// <summary> Camera shake fade out time. </summary>
		public float fadeOut = 0.5f;
		/// <summary> Total camera shake time. </summary>
		public float TotalTime { get; private set; }
		public bool IsFinished => currentTime >= TotalTime;

		public void Initialize()
		{
			TotalTime = fadeIn + duration + fadeOut;

			// Randomize phase offset
			phaseOffset = new(
				Runtime.randomNumberGenerator.Randf() * Mathf.Tau,
				Runtime.randomNumberGenerator.Randf() * Mathf.Tau,
				Runtime.randomNumberGenerator.Randf() * Mathf.Tau);
		}

		public Vector3 SimulateShake(float deltaTime, Vector3 cameraPosition)
		{
			// Update times and phase offsets
			currentTime += deltaTime;
			phaseOffset.X += (deltaTime * intensity.X) + (deltaTime * Runtime.randomNumberGenerator.Randf() * randomness);
			phaseOffset.Y += (deltaTime * intensity.Y) + (deltaTime * Runtime.randomNumberGenerator.Randf() * randomness);
			phaseOffset.Z += (deltaTime * intensity.Z) + (deltaTime * Runtime.randomNumberGenerator.Randf() * randomness);

			// Sample sin wave
			Vector3 shake = new(Mathf.Sin(phaseOffset.X), Mathf.Sin(phaseOffset.Y), Mathf.Sin(phaseOffset.Z));
			shake *= magnitude;
			return shake * CalculateRatio() * CalculateDistanceRatio(cameraPosition);
		}

		private float CalculateRatio()
		{
			float ratio = 1.0f;
			if (currentTime < fadeIn) // Fading in
				ratio = currentTime / fadeIn;
			else if (currentTime >= fadeIn + duration) // Fading out
				ratio = 1.0f - ((currentTime - (fadeIn + duration)) / fadeOut);
			ratio = Mathf.Clamp(ratio, 0f, 1f);
			ratio = Mathf.Pow(ratio, 2.0f);
			return ratio;
		}

		private float CalculateDistanceRatio(Vector3 cameraPosition)
		{
			if (Mathf.IsZeroApprox(maximumDistance))
				return 1f;

			float distance = cameraPosition.DistanceTo(origin);
			return 1f - Mathf.Clamp(distance / maximumDistance, 0f, 1f);
		}
	}
	#endregion

	#region Free Cam
	private float freecamMovespeed = 20;
	private Vector3 freecamMovementVector;
	private Vector3 freecamVelocity;

	private bool isFreeCamActive;
	private bool isFreeCamRotating;
	private bool isFreeCamTilting;

	private bool isFreeCamLocked;
	private Vector3 freeCamPosition;
	private Vector3 freeCamRotation;

	private void UpdateFreeCam()
	{
		UpdateFreeCamState();
		UpdateFreeCamMovement();
		UpdateFreeCamRotation();

		Camera.RotationDegrees = Camera.RotationDegrees.RemoveVertical();
		freeCamRotation = new(Camera.RotationDegrees.X, FreeCamRoot.GlobalRotationDegrees.Y, Camera.RotationDegrees.Z);

		DebugManager.Instance.RedrawCamData(FreeCamRoot.GlobalPosition, freeCamRotation);
		if (isFreeCamLocked)
			UpdateFreeCamData(freeCamPosition, freeCamRotation);
		else
			UpdateMotionBlur();
	}

	private void UpdateCameraVisibility()
	{
		debugMesh.Visible = DebugManager.Instance.DrawDebugCam;
		PathFollower.Visible = DebugManager.Instance.DrawDebugCam;
		Player.PathFollower.Visible = DebugManager.Instance.DrawDebugCam;
	}

	private void ToggleFreeCam()
	{
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
		}
	}

	private void UpdateFreeCamState()
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
			freeCamPosition = FreeCamRoot.GlobalPosition;
			GD.Print($"Free cam lock set to {isFreeCamLocked}.");
		}
	}

	private const float FreeCamPositionSmoothing = .3f;
	private void UpdateFreeCamMovement()
	{
		if (!isFreeCamActive)
			return;

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

		freecamMovementVector = freecamMovementVector.SmoothDamp(targetDirection * targetMoveSpeed, ref freecamVelocity, FreeCamPositionSmoothing);
		FreeCamRoot.GlobalTranslate(freecamMovementVector * PhysicsManager.normalDelta);
		if (isFreeCamLocked) // Update position instantly
			freeCamPosition = FreeCamRoot.GlobalPosition;
	}

	private Vector2 currentMouseMotion;
	private Vector2 receivedMouseMotion;
	private Vector2 mouseMotionVelocity;
	private const float FreeCamMouseSensitivity = .1f;
	private const float FreeCamRotationSmoothing = .3f;
	private void UpdateFreeCamRotation()
	{
		if (currentMouseMotion.IsZeroApprox() && receivedMouseMotion.IsZeroApprox())
			return;

		currentMouseMotion = currentMouseMotion.SmoothDamp(receivedMouseMotion, ref mouseMotionVelocity, FreeCamRotationSmoothing);
		receivedMouseMotion = Vector2.Zero; // Reset Mouse Motion
		if (isFreeCamRotating)
		{
			FreeCamRoot.RotateObjectLocal(Vector3.Up, Mathf.DegToRad(-currentMouseMotion.X) * FreeCamMouseSensitivity);
			Camera.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(-currentMouseMotion.Y) * FreeCamMouseSensitivity);
		}
		else if (isFreeCamTilting)
		{
			Camera.RotateObjectLocal(Vector3.Forward, Mathf.DegToRad(currentMouseMotion.X) * FreeCamMouseSensitivity);
		}

		if (isFreeCamLocked) // Update position instantly
			freeCamRotation = new(Camera.RotationDegrees.X, FreeCamRoot.GlobalRotationDegrees.Y, Camera.RotationDegrees.Z);
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
			receivedMouseMotion = (e as InputEventMouseMotion).Relative;
			return;
		}

		if (e is InputEventMouseButton emb && emb.IsPressed())
		{
			if (emb.ButtonIndex == MouseButton.WheelUp)
			{
				freecamMovespeed += 2;
				GD.Print($"Free cam Speed set to {freecamMovespeed}.");
			}
			if (emb.ButtonIndex == MouseButton.WheelDown)
			{
				freecamMovespeed -= 2;
				if (freecamMovespeed < 0)
					freecamMovespeed = 0;
				GD.Print($"Free cam Speed set to {freecamMovespeed}.");
			}
		}
	}
	#endregion
}

public partial class CameraBlendData : GodotObject
{
	/// <summary> Use crossfading? </summary>
	public CameraTransitionType TransitionType { get; set; }

	/// <summary> Ratio [0 <-> 1] of how much influence this blend has. </summary>
	public float LinearInfluence { get; private set; }
	/// <summary> Influence, smoothed with Mathf.Smoothstep. </summary>
	public float SmoothedInfluence { get; private set; }
	/// <summary> Actual amount to blend each frame. </summary>
	public float BlendSpeed { get; private set; }
	public bool UseDistanceBlending => Trigger?.UseDistanceBlending == true;

	/// <summary> Hall tracking position. </summary>
	public float hallPosition;
	/// <summary> Hall tracking velocity. </summary>
	private float hallVelocity;
	public const float HallSmoothing = 10.0f;
	public void HallSmoothDamp(float target, bool snap)
	{
		if (snap || !WasInitialized)
		{
			hallPosition = target;
			hallVelocity = 0;
			return;
		}

		hallPosition = ExtensionMethods.SmoothDamp(hallPosition, target, ref hallVelocity, HallSmoothing * PhysicsManager.physicsDelta);
	}

	/// <summary> [0 -> 1] Blend between offset and sample. </summary>
	public float SampleBlend { get; set; }

	/// <summary> How long blending takes in seconds. </summary>
	public float BlendTime { get; set; }
	/// <summary> Camera's static position. Only used when CameraSettingsResource.copyPosition is true. </summary>
	public Vector3 Position { get; set; }

	/// <summary> Camera's static rotation. Only used when CameraSettingsResource.copyRotation is true. </summary>
	public Basis RotationBasis { get; set; }

	/// <summary> Current fov. </summary>
	public float Fov;

	/// <summary> Current pitch angle. </summary>
	public float pitchAngle;
	/// <summary> Current yaw angle. </summary>
	public float yawAngle;
	/// <summary> Current tilt angle. </summary>
	public float tiltAngle;

	/// <summary> Last frame's lockon pitch tracking. </summary>
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
		SmoothedInfluence = Mathf.SmoothStep(0f, 1f, rawInfluence);
	}

	public void CalculateBlendSpeed() => BlendSpeed = 1f / BlendTime;

	public CameraBlendData() { }
	public CameraBlendData(CameraSettingsResource resource) => SettingsResource = resource;
}
