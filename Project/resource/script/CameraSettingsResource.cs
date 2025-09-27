using Godot;
using Godot.Collections;

namespace Project.Gameplay;

[Tool]

public partial class CameraSettingsResource : Resource
{
	#region Editor
	private const string CopyPositionKey = "Copy Position";
	private const string CopyRotationKey = "Copy Rotation";

	private const string DistanceKey = "Distance/Distance";
	private const string BackstepDistanceKey = "Distance/Backstep Distance";
	private const string DistanceModeKey = "Distance/Distance Calculation Mode";
	private const string SampleOffsetKey = "Distance/Sample Offset";
	private const string HomingAttackDistanceKey = "Distance/Ignore Homing Attack";

	private const string PitchAngleKey = "Rotation/Pitch Angle";
	private const string BackstepPitchKey = "Rotation/Extra Backstep Pitch Angle";
	private const string YawAngleKey = "Rotation/Yaw Angle";
	private const string PitchOverrideKey = "Rotation/Pitch Override Mode";
	private const string YawOverrideKey = "Rotation/Yaw Override Mode";

	private const string TiltModeKey = "Rotation/Tilt Mode";
	private const string TiltAngleKey = "Rotation/Tilt Angle";
	private const string TiltLengthKey = "Rotation/Tilt Length";
	private const string TiltMagnitudeKey = "Rotation/Tilt Magnitude";

	private const string ControlModeKey = "Rotation/Control Mode";
	private const string ControlInfluenceKey = "Rotation/Control Influence";

	private const string ViewOffsetKey = "View/Offset";
	private const string CopyFovKey = "View/Copy Fov";
	private const string FovKey = "View/FOV";

	private const string HorizontalTrackingKey = "Tracking/Horizontal Tracking Mode";
	private const string VerticalTrackingKey = "Tracking/Vertical Tracking Mode";
	private const string HallWidthKey = "Tracking/Hall Width";
	private const string HallRotationKey = "Tracking/Hall Rotation Tracking Strength";

	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = [];

		properties.Add(ExtensionMethods.CreateProperty(CopyPositionKey, Variant.Type.Bool));
		properties.Add(ExtensionMethods.CreateProperty(CopyRotationKey, Variant.Type.Bool));

		if (!copyPosition)
		{
			properties.Add(ExtensionMethods.CreateProperty(DistanceKey, Variant.Type.Float, PropertyHint.Range, "0,30,.1"));
			properties.Add(ExtensionMethods.CreateProperty(BackstepDistanceKey, Variant.Type.Float, PropertyHint.Range, "0,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty(HomingAttackDistanceKey, Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty(DistanceModeKey, Variant.Type.Int, PropertyHint.Enum, distanceCalculationMode.EnumToString()));

			if (distanceCalculationMode != DistanceModeEnum.Offset)
				properties.Add(ExtensionMethods.CreateProperty(SampleOffsetKey, Variant.Type.Float));

			properties.Add(ExtensionMethods.CreateProperty(HorizontalTrackingKey, Variant.Type.Int, PropertyHint.Enum, horizontalTrackingMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(VerticalTrackingKey, Variant.Type.Int, PropertyHint.Enum, verticalTrackingMode.EnumToString()));

			if (horizontalTrackingMode == TrackingModeEnum.Move)
			{
				properties.Add(ExtensionMethods.CreateProperty(HallWidthKey, Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty(HallRotationKey, Variant.Type.Float, PropertyHint.Range, "0,1,.1"));
			}
		}

		if (!copyRotation)
		{
			properties.Add(ExtensionMethods.CreateProperty(PitchAngleKey, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
			properties.Add(ExtensionMethods.CreateProperty(BackstepPitchKey, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
			properties.Add(ExtensionMethods.CreateProperty(YawAngleKey, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
			properties.Add(ExtensionMethods.CreateProperty(PitchOverrideKey, Variant.Type.Int, PropertyHint.Enum, pitchOverrideMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(YawOverrideKey, Variant.Type.Int, PropertyHint.Enum, yawOverrideMode.EnumToString()));

			properties.Add(ExtensionMethods.CreateProperty(TiltModeKey, Variant.Type.Int, PropertyHint.Enum, tiltMode.EnumToString()));
			if (tiltMode == TiltModeEnum.Override)
			{
				properties.Add(ExtensionMethods.CreateProperty(TiltAngleKey, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
				properties.Add(ExtensionMethods.CreateProperty(TiltLengthKey, Variant.Type.Float, PropertyHint.Range, "0,10,0.1"));
				properties.Add(ExtensionMethods.CreateProperty(TiltMagnitudeKey, Variant.Type.Float, PropertyHint.Range, "0,180,1"));
			}

			properties.Add(ExtensionMethods.CreateProperty(ControlModeKey, Variant.Type.Int, PropertyHint.Enum, controlMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(ControlInfluenceKey, Variant.Type.Float, PropertyHint.Range, "0,3,.01"));
		}

		properties.Add(ExtensionMethods.CreateProperty(ViewOffsetKey, Variant.Type.Vector2));

		properties.Add(ExtensionMethods.CreateProperty(CopyFovKey, Variant.Type.Bool));
		if (!copyFov)
			properties.Add(ExtensionMethods.CreateProperty(FovKey, Variant.Type.Float, PropertyHint.Range, "0, 179, .1"));

		return properties;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case CopyPositionKey:
				return copyPosition;
			case CopyRotationKey:
				return copyRotation;
			case CopyFovKey:
				return copyFov;

			case DistanceKey:
				return distance;
			case BackstepDistanceKey:
				return backstepDistance;
			case HomingAttackDistanceKey:
				return ignoreHomingAttack;
			case DistanceModeKey:
				return (int)distanceCalculationMode;
			case SampleOffsetKey:
				return sampleOffset;

			case PitchAngleKey:
				return Mathf.RadToDeg(pitchAngle);
			case BackstepPitchKey:
				return Mathf.RadToDeg(extraBackstepPitchAngle);
			case YawAngleKey:
				return Mathf.RadToDeg(yawAngle);
			case PitchOverrideKey:
				return (int)pitchOverrideMode;
			case YawOverrideKey:
				return (int)yawOverrideMode;

			case TiltModeKey:
				return (int)tiltMode;
			case TiltAngleKey:
				return Mathf.RadToDeg(tiltAngle);
			case TiltMagnitudeKey:
				return Mathf.RadToDeg(tiltMagnitude);
			case TiltLengthKey:
				return tiltLength;

			case ControlModeKey:
				return (int)controlMode;
			case ControlInfluenceKey:
				return pathControlInfluence;

			case ViewOffsetKey:
				return viewportOffset;
			case FovKey:
				return targetFOV;

			case HorizontalTrackingKey:
				return (int)horizontalTrackingMode;
			case VerticalTrackingKey:
				return (int)verticalTrackingMode;
			case HallWidthKey:
				return hallWidth;
			case HallRotationKey:
				return hallRotationStrength;
			default:
				break;
		}

		return base._Get(property);
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case CopyPositionKey:
				copyPosition = (bool)value;
				NotifyPropertyListChanged();
				break;
			case CopyRotationKey:
				copyRotation = (bool)value;
				NotifyPropertyListChanged();
				break;
			case CopyFovKey:
				copyFov = (bool)value;
				NotifyPropertyListChanged();
				break;

			case DistanceKey:
				distance = (float)value;
				break;
			case BackstepDistanceKey:
				backstepDistance = (float)value;
				break;
			case HomingAttackDistanceKey:
				ignoreHomingAttack = (bool)value;
				break;
			case DistanceModeKey:
				distanceCalculationMode = (DistanceModeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case SampleOffsetKey:
				sampleOffset = Mathf.Round((float)value * 10.0f) * .1f;
				break;


			case PitchAngleKey:
				pitchAngle = Mathf.DegToRad((float)value);
				break;
			case BackstepPitchKey:
				extraBackstepPitchAngle = Mathf.DegToRad((float)value);
				break;
			case YawAngleKey:
				yawAngle = Mathf.DegToRad((float)value);
				break;
			case PitchOverrideKey:
				pitchOverrideMode = (OverrideModeEnum)(int)value;
				break;
			case YawOverrideKey:
				yawOverrideMode = (OverrideModeEnum)(int)value;
				break;

			case TiltModeKey:
				tiltMode = (TiltModeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case TiltAngleKey:
				tiltAngle = Mathf.DegToRad((float)value);
				break;
			case TiltMagnitudeKey:
				tiltMagnitude = Mathf.DegToRad((float)value);
				break;
			case TiltLengthKey:
				tiltLength = (float)value;
				break;

			case ControlModeKey:
				controlMode = (ControlModeEnum)(int)value;
				break;
			case ControlInfluenceKey:
				pathControlInfluence = (float)value;
				break;

			case ViewOffsetKey:
				viewportOffset = (Vector2)value;
				break;
			case FovKey:
				targetFOV = (float)value;
				break;

			case HorizontalTrackingKey:
				horizontalTrackingMode = (TrackingModeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case VerticalTrackingKey:
				verticalTrackingMode = (TrackingModeEnum)(int)value;
				break;
			case HallWidthKey:
				hallWidth = (float)value;
				if (hallWidth < 0) // Can't have a negative hall width!
					hallWidth = 0;
				break;
			case HallRotationKey:
				hallRotationStrength = (float)value;
				break;

			default:
				return false;
		}

		return true;
	}
	#endregion

	/// <summary> Copy camera's position from the cameraTrigger node? </summary>
	public bool copyPosition;
	/// <summary> Copy camera's rotation from the cameraTrigger node? </summary>
	public bool copyRotation;
	/// <summary> Copy camera's FOV from the cameraTrigger node? Requires a reference camera. </summary>
	public bool copyFov;

	/// <summary> Angle (in radians) of pitch (X rotation). </summary>
	public float pitchAngle;
	/// <summary> Extra pitch angle to add when moving backwards. </summary>
	public float extraBackstepPitchAngle;
	/// <summary> Angle (in radians) of yaw (Y rotation). </summary>
	public float yawAngle;
	/// <summary> Determines how the camera's tilt should behave. </summary>
	public TiltModeEnum tiltMode;
	public enum TiltModeEnum
	{
		Disabled, // Don't tilt
		FollowPath, // Use the path's tilt
		Override, // Use a specific value
	}
	/// <summary> Angle (in radians) of tilt (Z rotation). </summary>
	public float tiltAngle;
	/// <summary> Magnitude of the sway. </summary>
	public float tiltMagnitude;
	/// <summary> How long (in seconds) the sway cycle will take. </summary>
	public float tiltLength;

	/// <summary> Determines how inputs should be read (mostly for Autorun/Legacy skills). </summary>
	public ControlModeEnum controlMode;
	public enum ControlModeEnum
	{
		Normal, // Forward is forward and backwards is backwards
		Reverse, // Invert forward and backwards
		Sidescrolling, // Alter inputs to be faced on IsFacingRight
		Auto, // Automatically invert controls based on the direction to the camera
	}

	/// <summary> How closely to follow the path. </summary>
	public float pathControlInfluence = 1.0f;
	/// <summary> How should pitch be applied? </summary>
	public OverrideModeEnum pitchOverrideMode;
	/// <summary> How should yaw be applied? </summary>
	public OverrideModeEnum yawOverrideMode;
	public enum OverrideModeEnum
	{
		Add, // Add the value
		Replace // Replace the value
	}

	/// <summary> How far to stay from the player. </summary>
	public float distance;
	/// <summary> Extra distance to add when backstepping. </summary>
	public float backstepDistance;
	/// <summary> Don't apply the homing attack camera? </summary>
	public bool ignoreHomingAttack;
	/// <summary> How to calculate distance. </summary>
	public DistanceModeEnum distanceCalculationMode;
	public enum DistanceModeEnum
	{
		Auto, // Use offset when moving forward, and sample when moving backwards
		Offset, // Add distance * PathFollower.Back() (Better for sharp corners)
		Sample, // Physically sample to move pathfollower's progress by distance (Better for slopes)
	}
	/// <summary> How much to offset sampling. Can be used for cameras that "look ahead." </summary>
	public float sampleOffset;

	/// <summary> Viewport offset. Use this offset height or lead the player (sidescrolling) </summary>
	public Vector2 viewportOffset;
	/// <summary> FOV. Set to 0 to reset to default fov. </summary>
	public float targetFOV;

	/// <summary> Is horizontal tracking enabled? </summary>
	public TrackingModeEnum horizontalTrackingMode;
	/// <summary> Is vertical tracking enabled? </summary>
	public TrackingModeEnum verticalTrackingMode;

	/// <summary> Limit horizontal tracking to this value. </summary>
	public float hallWidth;
	/// <summary> Rotationally track the player when they go beyond the hall width. </summary>
	public float hallRotationStrength;

	public enum TrackingModeEnum
	{
		Move,
		Rotate,
		Disable
	}
}