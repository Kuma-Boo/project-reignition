using Godot;
using Godot.Collections;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	[Tool]
	public partial class CameraSettingsResource : Resource
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();
			properties.Add(ExtensionMethods.CreateProperty("Static Camera", Variant.Type.Bool));

			properties.Add(ExtensionMethods.CreateProperty("Modify FOV", Variant.Type.Bool));
			if (modifyFOV)
				properties.Add(ExtensionMethods.CreateProperty("FOV", Variant.Type.Float, PropertyHint.Range, "1,179,0.1"));

			properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Deadzone X", Variant.Type.Float, PropertyHint.Range, "0,1,0.1"));
			properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Deadzone Y", Variant.Type.Float, PropertyHint.Range, "0,1,0.1"));

			properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Pitch Strength", Variant.Type.Float, PropertyHint.Range, "0,1,0.1"));
			properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Yaw Strength", Variant.Type.Float, PropertyHint.Range, "0,1,0.1"));

			if (!isStaticCamera)
			{
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Distance", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Backstep Distance Addition", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Height", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/H_Offset", Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/V_Offset", Variant.Type.Float));

				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Allow Rolling", Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Pitch Mode", Variant.Type.Int, PropertyHint.Enum, "Add, Override"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Yaw Mode", Variant.Type.Int, PropertyHint.Enum, "Add, Override"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Pitch Angle", Variant.Type.Float, PropertyHint.Range, "-360,360,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Yaw Angle", Variant.Type.Float, PropertyHint.Range, "-360,360,0.1"));

				properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Horizontal Tracking", Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Vertical Tracking", Variant.Type.Bool));
			}
			else
			{
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Autoset Position", Variant.Type.Bool));

				if (!autosetStaticPosition)
					properties.Add(ExtensionMethods.CreateProperty("Position Settings/Static Position", Variant.Type.Vector3));
			}

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Modify FOV":
					return modifyFOV;
				case "FOV":
					return fov;

				case "Static Camera":
					return isStaticCamera;
				case "Position Settings/Autoset Position":
					return autosetStaticPosition;
				case "Position Settings/Static Position":
					return staticPosition;

				case "Position Settings/Distance":
					return distance;
				case "Position Settings/Backstep Distance Addition":
					return backstepDistanceAddition;
				case "Position Settings/Height":
					return height;
				case "Position Settings/H_Offset":
					return hOffset;
				case "Position Settings/V_Offset":
					return vOffset;

				case "Rotation Settings/Pitch Mode":
					return (int)pitchMode;
				case "Rotation Settings/Yaw Mode":
					return (int)yawMode;
				case "Rotation Settings/Pitch Angle":
					return Mathf.RadToDeg(pitchAngle);
				case "Rotation Settings/Yaw Angle":
					return Mathf.RadToDeg(yawAngle);
				case "Rotation Settings/Allow Rolling":
					return isRollEnabled;

				case "Tracking Settings/Deadzone X":
					return trackingDeadzone.x;
				case "Tracking Settings/Deadzone Y":
					return trackingDeadzone.y;
				case "Tracking Settings/Horizontal Tracking":
					return hTrackingEnabled;
				case "Tracking Settings/Vertical Tracking":
					return vTrackingEnabled;
				case "Tracking Settings/Pitch Strength":
					return pitchTrackingStrength;
				case "Tracking Settings/Yaw Strength":
					return yawTrackingStrength;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Modify FOV":
					modifyFOV = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "FOV":
					fov = (float)value;
					break;

				case "Static Camera":
					isStaticCamera = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Position Settings/Autoset Position":
					autosetStaticPosition = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Position Settings/Static Position":
					staticPosition = (Vector3)value;
					break;

				case "Position Settings/Distance":
					distance = (float)value;
					break;
				case "Position Settings/Backstep Distance Addition":
					backstepDistanceAddition = (float)value;
					break;
				case "Position Settings/Height":
					height = (float)value;
					break;
				case "Position Settings/H_Offset":
					hOffset = (float)value;
					break;
				case "Position Settings/V_Offset":
					vOffset = (float)value;
					break;

				case "Rotation Settings/Pitch Mode":
					pitchMode = (OverrideMode)(int)value;
					break;
				case "Rotation Settings/Yaw Mode":
					yawMode = (OverrideMode)(int)value;
					break;
				case "Rotation Settings/Pitch Angle":
					pitchAngle = Mathf.DegToRad((float)value);
					break;
				case "Rotation Settings/Yaw Angle":
					yawAngle = Mathf.DegToRad((float)value);
					break;
				case "Rotation Settings/Allow Rolling":
					isRollEnabled = (bool)value;
					break;


				case "Tracking Settings/Deadzone X":
					trackingDeadzone.x = (float)value;
					break;
				case "Tracking Settings/Deadzone Y":
					trackingDeadzone.y = (float)value;
					break;
				case "Tracking Settings/Horizontal Tracking":
					hTrackingEnabled = (bool)value;
					break;
				case "Tracking Settings/Vertical Tracking":
					vTrackingEnabled = (bool)value;
					break;
				case "Tracking Settings/Pitch Strength":
					pitchTrackingStrength = (float)value;
					break;
				case "Tracking Settings/Yaw Strength":
					yawTrackingStrength = (float)value;
					break;

				default:
					return false;
			}

			return true;
		}
		#endregion

		public bool modifyFOV;
		/// <summary> Camera's FOV. </summary>
		public float fov;

		/// <summary> Track the player from a static position? </summary>
		public bool isStaticCamera;
		/// <summary> Is staticPosition automatically set from a CameraTrigger.cs? </summary>
		public bool autosetStaticPosition;
		/// <summary> Position to view from. Only valid when isStaticCamera is true. </summary>
		public Vector3 staticPosition;

		//Dynamic camera settings
		/// <summary> Distance from the player. </summary>
		public float distance = 1.5f;
		/// <summary> Distance to add when backstepping. </summary>
		public float backstepDistanceAddition;
		/// <summary> Position offset. </summary>
		public float height;
		/// <summary> Horizontal view offset. Translation based on camera's orientation. </summary>
		public float hOffset;
		/// <summary> Vertical view offset. Translation based on camera's orientation. </summary>
		public float vOffset;

		//Rotation settings
		public enum OverrideMode
		{
			Add,
			Override,
		}
		/// <summary> Override mode for pitch (x-axis rotation). </summary>
		public OverrideMode pitchMode;
		/// <summary> Override mode for yaw (y-axis rotation). </summary>
		public OverrideMode yawMode;
		/// <summary> Pitch (x-axis rotation) angle, in radians. </summary>
		public float pitchAngle;
		/// <summary> Yaw (y-axis rotation) angle, in radians. </summary>
		public float yawAngle;


		//Tracking settings
		/// <summary> Screen Ratio deadzones. </summary>
		public Vector2 trackingDeadzone = new Vector2(0.5f, 0.0f);
		/// <summary> Should the camera track the player's horizontal position? </summary>
		public bool hTrackingEnabled;
		/// <summary> Should the camera track the player vertical position? </summary>
		public bool vTrackingEnabled = true;
		/// <summary> Strength of pitch tracking </summary>
		public float pitchTrackingStrength = 1f;
		/// <summary> Strength of yaw tracking </summary>
		public float yawTrackingStrength = 1f;

		/// <summary> Should the camera roll (z-axis rotation) to match the ground's angle? </summary>
		public bool isRollEnabled;
	}
}
