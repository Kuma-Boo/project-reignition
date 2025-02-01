using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Activates a <see cref="CameraSettingsResource"/>.
/// </summary>
public partial class CameraTrigger : StageTriggerModule
{
	/// <summary> How long the transition is (in seconds). Use a transition time of 0 to perform an instant cut. </summary>
	[Export(PropertyHint.Range, "0,5,0.1,or_greater")]
	public float transitionTime = 0.5f;
	/// <summary> Override to have a different blend time during deactivation. </summary>
	[Export(PropertyHint.Range, "-1,2,0.1")]
	public float deactivationTransitionTime = -1f;

	/// <summary> Set this to a non-zero value to use distance blending. </summary>
	[Export(PropertyHint.Range, "0,50,1")]
	public int distanceBlending;
	public bool UseDistanceBlending => distanceBlending != 0;

	[Export] public CameraTransitionType transitionType;
	[Export] public bool enableInputBlending;

	/// <summary> Must be assigned to something. </summary>
	[Export] public CameraSettingsResource settings;
	/// <summary> Reference to the camera data that was being used when this trigger was entered. </summary>
	[Export] public CameraSettingsResource previousSettings;

	[ExportGroup("Transform Overrides")]
	/// <summary> Update positions and rotations every frame? </summary>
	[Export] public bool UpdateEveryFrame { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath followObject;
	private Node3D _followObject;
	private Camera3D _referenceCamera;
	private bool IsOverridingCameraTransform => settings.copyPosition || settings.copyRotation || settings.copyRotation;

	private bool cachedPreviousSettings;
	private Vector3 previousStaticPosition;
	private Basis previousStaticRotation;
	private PlayerCameraController Camera => Player.Camera;

	public override void _Ready()
	{
		if (!IsOverridingCameraTransform)
			return;

		_followObject = followObject?.IsEmpty == false ? GetNode<Node3D>(followObject) : this;

		if (_followObject is Camera3D)
			_referenceCamera = _followObject as Camera3D;
	}

	public void UpdateStaticData(CameraBlendData data)
	{
		if (data.SettingsResource != settings || !IsOverridingCameraTransform) return;

		if (data.SettingsResource.copyPosition)
			data.Position = GlobalPosition;

		if (data.SettingsResource.copyRotation)
			data.RotationBasis = GlobalBasis;

		if (data.SettingsResource.copyFov && _referenceCamera != null)
			data.Fov = _referenceCamera.Fov;
	}

	public override void Activate()
	{
		if (settings == null)
		{
			GD.PrintErr($"{Name} doesn't have a CameraSettingResource attached!");
			return;
		}

		if (!cachedPreviousSettings)
		{
			previousSettings ??= Camera.ActiveSettings;
			previousStaticPosition = Camera.ActiveBlendData.Position; // Cache static position
			previousStaticRotation = Camera.ActiveBlendData.RotationBasis; // Cache static rotation
			cachedPreviousSettings = true;
		}

		if (UseDistanceBlending)
		{
			// Ensure we add the previous camera settings so we have something to blend with
			Camera.UpdateCameraSettings(new()
			{
				BlendTime = transitionTime,
				SettingsResource = previousSettings,
				TransitionType = transitionType,
				Trigger = this
			}, enableInputBlending);
		}

		Camera.UpdateCameraSettings(new()
		{
			BlendTime = transitionTime,
			SettingsResource = settings,
			TransitionType = transitionType,
			Trigger = this
		}, enableInputBlending);

		UpdateStaticData(Camera.ActiveBlendData);
	}

	public override void Deactivate()
	{
		if (previousSettings == null || settings == null)
			return;

		if (Camera.ActiveSettings != settings)
			return; // Already overridden by a different trigger

		if (Player.IsTeleporting)
			return;

		Camera.UpdateCameraSettings(new()
		{
			BlendTime = Mathf.IsEqualApprox(deactivationTransitionTime, -1) ? transitionTime : deactivationTransitionTime,
			SettingsResource = previousSettings,
			Position = previousStaticPosition, // Restore cached static position
			RotationBasis = previousStaticRotation // Restore cached static rotation
		}, enableInputBlending);
	}

	public float CalculateInfluence()
	{
		if (Mathf.IsZeroApprox(distanceBlending))
		{
			GD.PushWarning("Warning: ActiveCameraSettings distance amount is 0.");
			return 1f;
		}

		float playerProgress = Player.PathFollower.Progress;
		float triggerProgress = Player.PathFollower.GetProgress(GlobalPosition);
		return Mathf.SmoothStep(0f, 1f, Mathf.Clamp((playerProgress - triggerProgress) / distanceBlending, 0f, 1f));
	}
}