using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Activates a <see cref="CameraSettingsResource"/>.
/// </summary>
[Tool] //Needed to draw distance blend endpoint, like drift triggers
public partial class CameraTrigger : StageTriggerModule
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = new()
		{
		ExtensionMethods.CreateProperty("Additional Blend Settings/Blend By Distance", Variant.Type.Bool)
		};

		if (BlendByDistance)
		{
			properties.Add(ExtensionMethods.CreateProperty("Additional Blend Settings/Blend Distance", Variant.Type.Int, PropertyHint.Range, "1, 300"));
			properties.Add(ExtensionMethods.CreateProperty("Additional Blend Settings/Distance Blend Setting", Variant.Type.Object));
		}
		return properties;
	}
	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case "Additional Blend Settings/Blend By Distance":
				return BlendByDistance;
			case "Additional Blend Settings/Blend Distance":
				return (int)blendDistance;
			case "Additional Blend Settings/Distance Blend Setting":
				return (CameraSettingsResource)DistanceBlendSetting;
		}
		return base._Get(property);
	}
	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Additional Blend Settings/Blend By Distance":
				BlendByDistance = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Additional Blend Settings/Blend Distance":
				blendDistance = (int)value;
				break;
			case "Additional Blend Settings/Distance Blend Setting":
				DistanceBlendSetting = (CameraSettingsResource)value;
				break;
			default:
				return false;
		}
		return true;
	}
	#endregion

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
	/// <summary>Does this trigger blend between two camera settings based off distance?</summary>
	private bool BlendByDistance = false;
	/// <summary>How far away would the blend/tranisition complete?</summary>
	private int blendDistance = 100;
	/// <summary>The point where the camera would fully tranisition to setting 2 when blending by distance</summary>
	public Vector3 BlendFinishPoint => GlobalPosition + (this.Back() * blendDistance);

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
	/// <summary> If blending two settings by distance, the second camera setting data that is used for the blend </summary>
	private CameraSettingsResource DistanceBlendSetting;
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
			BlendsOverDistance = false,
			BlendTime = transitionTime,
			SettingsResource = settings,
			IsCrossfadeEnabled = transitionType == TransitionType.Crossfade,
			Trigger = this
		}, enableInputBlending);
   
		if (BlendByDistance) //If this a distance blend Trigger, add the second setting/camera for the first to blend with as well
		{
			Camera.UpdateCameraSettings(new()
			{
				DistanceBlendEndPoint = BlendFinishPoint,
				blendLength = blendDistance,
				BlendsOverDistance = BlendByDistance,
				SettingsResource = DistanceBlendSetting,
				IsCrossfadeEnabled = transitionType == TransitionType.Crossfade,
				Trigger = this
			}, enableInputBlending);
		}

		UpdateStaticData(Camera.ActiveBlendData);
	}

	public override void Deactivate()
	{
		if (previousSettings == null || settings == null)
			return;

		if (Camera.ActiveSettings != settings && Camera.ActiveSettings != DistanceBlendSetting)
			return; // Already overridden by a different trigger

		if (Player.IsTeleporting)
			return;

		Camera.UpdateCameraSettings(new()
		{
			BlendTime = Mathf.IsEqualApprox(deactivationTransitionTime, -1) ? transitionTime : deactivationTransitionTime,
			SettingsResource = previousSettings,
			StaticPosition = previousStaticPosition, // Restore cached static position
			RotationBasis = previousStaticRotation // Restore cached static rotation
		}, enableInputBlending);
	}
}
