using Godot;
using Godot.Collections;

namespace Project.Gameplay;

[Tool]
[GlobalClass]
public partial class CameraSettingsResource : Resource
{
	#region Editor
	private const string STATIC_CAMERA_KEY = "Static Camera Enabled";
	private const string COPY_POSITION_KEY = "Copy Position";
	private const string COPY_ROTATION_KEY = "Copy Rotation";
	private const string POSITION_KEY = "Static Position";

	private const string DISTANCE_KEY = "Distance/Distance";
	private const string BACKSTEP_DISTANCE_KEY = "Distance/Backstep Distance";
	private const string DISTANCE_MODE_KEY = "Distance/Distance Calculation Mode";
	private const string SAMPLE_OFFSET_KEY = "Distance/Sample Offset";
	private const string HOMING_ATTACK_DISTANCE_KEY = "Distance/Ignore Homing Attack";

	private const string PITCH_ANGLE_KEY = "Rotation/Pitch Angle";
	private const string YAW_ANGLE_KEY = "Rotation/Yaw Angle";
	private const string PITCH_OVERRIDE_KEY = "Rotation/Pitch Override Mode";
	private const string YAW_OVERRIDE_KEY = "Rotation/Yaw Override Mode";
	private const string TILT_KEY = "Rotation/Follow Path Tilt";
	private const string CONTROL_INFLUENCE_KEY = "Rotation/Control Influence";

	private const string VIEW_OFFSET_KEY = "View/Offset";
	private const string COPY_FOV_KEY = "View/Copy Fov";
	private const string FOV_KEY = "View/FOV";

	private const string HORIZONTAL_TRACKING_KEY = "Tracking/Horizontal Tracking Mode";
	private const string VERTICAL_TRACKING_KEY = "Tracking/Vertical Tracking Mode";
	private const string HALL_WIDTH_KEY = "Tracking/Hall Width";
	private const string HALL_ROTATION_KEY = "Tracking/Hall Rotation Tracking Strength";

	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = new();

		properties.Add(ExtensionMethods.CreateProperty(STATIC_CAMERA_KEY, Variant.Type.Bool));
		if (!useStaticPosition)
		{
			properties.Add(ExtensionMethods.CreateProperty(DISTANCE_KEY, Variant.Type.Float, PropertyHint.Range, "0,30,.1"));
			properties.Add(ExtensionMethods.CreateProperty(BACKSTEP_DISTANCE_KEY, Variant.Type.Float, PropertyHint.Range, "0,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty(HOMING_ATTACK_DISTANCE_KEY, Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty(DISTANCE_MODE_KEY, Variant.Type.Int, PropertyHint.Enum, distanceCalculationMode.EnumToString()));

			if (distanceCalculationMode != DistanceModeEnum.Offset)
				properties.Add(ExtensionMethods.CreateProperty(SAMPLE_OFFSET_KEY, Variant.Type.Float));

			properties.Add(ExtensionMethods.CreateProperty(HORIZONTAL_TRACKING_KEY, Variant.Type.Int, PropertyHint.Enum, horizontalTrackingMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(VERTICAL_TRACKING_KEY, Variant.Type.Int, PropertyHint.Enum, verticalTrackingMode.EnumToString()));

			if (horizontalTrackingMode == TrackingModeEnum.Move)
			{
				properties.Add(ExtensionMethods.CreateProperty(HALL_WIDTH_KEY, Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty(HALL_ROTATION_KEY, Variant.Type.Float, PropertyHint.Range, "0,1,.1"));
			}
		}
		else
		{
			properties.Add(ExtensionMethods.CreateProperty(COPY_POSITION_KEY, Variant.Type.Bool));
			if (!copyPosition)
				properties.Add(ExtensionMethods.CreateProperty(POSITION_KEY, Variant.Type.Vector3));

			properties.Add(ExtensionMethods.CreateProperty(COPY_ROTATION_KEY, Variant.Type.Bool));
		}


		if (!useStaticPosition || !copyRotation)
		{
			properties.Add(ExtensionMethods.CreateProperty(PITCH_ANGLE_KEY, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
			properties.Add(ExtensionMethods.CreateProperty(YAW_ANGLE_KEY, Variant.Type.Float, PropertyHint.Range, "-180,180,1"));
			properties.Add(ExtensionMethods.CreateProperty(PITCH_OVERRIDE_KEY, Variant.Type.Int, PropertyHint.Enum, pitchOverrideMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(YAW_OVERRIDE_KEY, Variant.Type.Int, PropertyHint.Enum, yawOverrideMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty(TILT_KEY, Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty(CONTROL_INFLUENCE_KEY, Variant.Type.Float, PropertyHint.Range, "0,3,.01"));
		}

		properties.Add(ExtensionMethods.CreateProperty(VIEW_OFFSET_KEY, Variant.Type.Vector2));

		properties.Add(ExtensionMethods.CreateProperty(COPY_FOV_KEY, Variant.Type.Bool));
		if (!copyFov)
			properties.Add(ExtensionMethods.CreateProperty(FOV_KEY, Variant.Type.Float, PropertyHint.Range, "0, 179, .1"));

		return properties;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case STATIC_CAMERA_KEY:
				return useStaticPosition;
			case COPY_POSITION_KEY:
				return copyPosition;
			case COPY_ROTATION_KEY:
				return copyRotation;
			case COPY_FOV_KEY:
				return copyFov;
			case POSITION_KEY:
				return staticPosition;

			case DISTANCE_KEY:
				return distance;
			case BACKSTEP_DISTANCE_KEY:
				return backstepDistance;
			case HOMING_ATTACK_DISTANCE_KEY:
				return ignoreHomingAttackDistance;
			case DISTANCE_MODE_KEY:
				return (int)distanceCalculationMode;
			case SAMPLE_OFFSET_KEY:
				return (float)sampleOffset;

			case PITCH_ANGLE_KEY:
				return Mathf.RadToDeg(pitchAngle);
			case YAW_ANGLE_KEY:
				return Mathf.RadToDeg(yawAngle);
			case PITCH_OVERRIDE_KEY:
				return (int)pitchOverrideMode;
			case YAW_OVERRIDE_KEY:
				return (int)yawOverrideMode;
			case TILT_KEY:
				return followPathTilt;
			case CONTROL_INFLUENCE_KEY:
				return pathControlInfluence;

			case VIEW_OFFSET_KEY:
				return viewportOffset;
			case FOV_KEY:
				return targetFOV;

			case HORIZONTAL_TRACKING_KEY:
				return (int)horizontalTrackingMode;
			case VERTICAL_TRACKING_KEY:
				return (int)verticalTrackingMode;
			case HALL_WIDTH_KEY:
				return hallWidth;
			case HALL_ROTATION_KEY:
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
			case STATIC_CAMERA_KEY:
				useStaticPosition = (bool)value;
				NotifyPropertyListChanged();
				break;
			case COPY_POSITION_KEY:
				copyPosition = (bool)value;
				NotifyPropertyListChanged();
				break;
			case COPY_ROTATION_KEY:
				copyRotation = (bool)value;
				NotifyPropertyListChanged();
				break;
			case COPY_FOV_KEY:
				copyFov = (bool)value;
				NotifyPropertyListChanged();
				break;
			case POSITION_KEY:
				staticPosition = (Vector3)value;
				break;

			case DISTANCE_KEY:
				distance = (float)value;
				break;
			case BACKSTEP_DISTANCE_KEY:
				backstepDistance = (float)value;
				break;
			case HOMING_ATTACK_DISTANCE_KEY:
				ignoreHomingAttackDistance = (bool)value;
				break;
			case DISTANCE_MODE_KEY:
				distanceCalculationMode = (DistanceModeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case SAMPLE_OFFSET_KEY:
				sampleOffset = Mathf.Round((float)value * 10.0f) * .1f;
				break;


			case PITCH_ANGLE_KEY:
				pitchAngle = Mathf.DegToRad((float)value);
				break;
			case YAW_ANGLE_KEY:
				yawAngle = Mathf.DegToRad((float)value);
				break;
			case PITCH_OVERRIDE_KEY:
				pitchOverrideMode = (OverrideModeEnum)(int)value;
				break;
			case YAW_OVERRIDE_KEY:
				yawOverrideMode = (OverrideModeEnum)(int)value;
				break;
			case TILT_KEY:
				followPathTilt = (bool)value;
				break;
			case CONTROL_INFLUENCE_KEY:
				pathControlInfluence = (float)value;
				break;

			case VIEW_OFFSET_KEY:
				viewportOffset = (Vector2)value;
				break;
			case FOV_KEY:
				targetFOV = (float)value;
				break;

			case HORIZONTAL_TRACKING_KEY:
				horizontalTrackingMode = (TrackingModeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case VERTICAL_TRACKING_KEY:
				verticalTrackingMode = (TrackingModeEnum)(int)value;
				break;
			case HALL_WIDTH_KEY:
				hallWidth = (float)value;
				if (hallWidth < 0) //Can't have a negative hall width!
					hallWidth = 0;
				break;
			case HALL_ROTATION_KEY:
				hallRotationStrength = (float)value;
				break;

			default:
				return false;
		}

		return true;
	}
	#endregion

	/// <summary> Keep the camera's position at a specific point? </summary>
	public bool useStaticPosition;
	/// <summary> Copy camera's position from the cameraTrigger node? </summary>
	public bool copyPosition = true;
	/// <summary> Copy camera's rotation from the cameraTrigger node? </summary>
	public bool copyRotation;
	/// <summary> Copy camera's rotation from the cameraTrigger node (requires a reference camera)? </summary>
	public bool copyFov;
	/// <summary> Static position to use when copyPosition is false. </summary>
	public Vector3 staticPosition;

	/// <summary> Angle (in radians) of pitch (X rotation). </summary>
	public float pitchAngle;
	/// <summary> Angle (in radians) of yaw (Y rotation). </summary>
	public float yawAngle;
	/// <summary> Should the camera tilt (Z rotation) with the path? </summary>
	public bool followPathTilt;
	/// <summary> How closely to follow the path. </summary>
	public float pathControlInfluence;
	/// <summary> How should pitch be applied? </summary>
	public OverrideModeEnum pitchOverrideMode;
	/// <summary> How should yaw be applied? </summary>
	public OverrideModeEnum yawOverrideMode;
	public enum OverrideModeEnum
	{
		Add, //Add the value
		Replace //Replace the value
	}

	/// <summary> How far to stay from the player. </summary>
	public float distance;
	/// <summary> Extra distance to add when backstepping. </summary>
	public float backstepDistance;
	/// <summary> Don't add more distance when performing a homing attack? </summary>
	public bool ignoreHomingAttackDistance;
	/// <summary> How to calculate distance. </summary>
	public DistanceModeEnum distanceCalculationMode;
	public enum DistanceModeEnum
	{
		Auto, //Use offset when moving forward, and sample when moving backwards
		Offset, //Add distance * PathFollower.Back() (Better for sharp corners)
		Sample, //Physically sample to move pathfollower's progress by distance (Better for slopes)
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