using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Activates a <see cref="CameraSettingsResource"/>.
/// </summary>
public partial class CameraTrigger : StageTriggerModule
{
	/// <summary> How long the transition is (in seconds). Use a transition time of 0 to perform an instant cut. </summary>
	[Export(PropertyHint.Range, "0,2,0.1")]
	public float transitionTime;
	/// <summary> Override to have a different blend time during deactivation. </summary>
	[Export(PropertyHint.Range, "-1,2,0.1")]
	public float deactivationTransitionTime = -1;
	[Export]
	public TransitionType transitionType;
	public enum TransitionType
	{
		Blend, // Interpolate between states
		Crossfade, // Crossfade states
	}

	/// <summary> Update static position/rotations every frame? </summary>
	[Export]
	public bool UpdateEveryFrame { get; private set; }

	[Export]
	public bool enableInputBlending;

	/// <summary> Must be assigned to something. </summary>
	[Export]
	public CameraSettingsResource settings;
	/// <summary> Reference to the camera data that was being used when this trigger was entered. </summary>
	[Export]
	private CameraSettingsResource previousSettings;
	[Export]
	private Camera3D referenceCamera;

	private Vector3 previousStaticPosition;
	private Basis previousStaticRotation;
	private PlayerCameraController Camera => Player.Camera;

	public void UpdateStaticData(CameraBlendData data)
	{
		if (data.SettingsResource != settings) return;

		if (data.SettingsResource.useStaticPosition)
		{
			if (data.SettingsResource.copyPosition)
				data.StaticPosition = GlobalPosition;
			else
				data.StaticPosition = data.SettingsResource.staticPosition;
		}

		if (data.SettingsResource.copyRotation)
			data.RotationBasis = GlobalBasis;

		if (data.SettingsResource.copyFov && referenceCamera != null)
			data.Fov = referenceCamera.Fov;
	}

	public override void Activate()
	{
		if (settings == null)
		{
			GD.PrintErr($"{Name} doesn't have a CameraSettingResource attached!");
			return;
		}

		if (previousSettings == null)
		{
			previousSettings = Camera.ActiveSettings;
			previousStaticPosition = Camera.ActiveBlendData.StaticPosition; // Cache static position
			previousStaticRotation = Camera.ActiveBlendData.RotationBasis; // Cache static rotation
		}

		if (Camera.ActiveSettings == settings &&
			!(settings.copyPosition || settings.copyRotation))
		{
			return;
		}

		Camera.UpdateCameraSettings(new()
		{
			BlendTime = transitionTime,
			SettingsResource = settings,
			IsCrossfadeEnabled = transitionType == TransitionType.Crossfade,
			Trigger = this
		}, enableInputBlending);

		UpdateStaticData(Camera.ActiveBlendData);
	}

	public override void Deactivate()
	{
		if (previousSettings == null || settings == null) return;
		if (Camera.ActiveSettings != settings) return; // Already overridden by a different trigger

		// REFACTOR TODO if (Player.ActionState == PlayerController.ActionStates.Teleport) return;

		Camera.UpdateCameraSettings(new()
		{
			BlendTime = Mathf.IsEqualApprox(deactivationTransitionTime, -1) ? transitionTime : deactivationTransitionTime,
			SettingsResource = previousSettings,
			StaticPosition = previousStaticPosition, // Restore cached static position
			RotationBasis = previousStaticRotation // Restore cached static rotation
		}, enableInputBlending);
	}
}