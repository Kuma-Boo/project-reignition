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

			properties.Add(ExtensionMethods.CreateProperty("Custom FOV", Variant.Type.Bool));
			if (useCustomFOV)
				properties.Add(ExtensionMethods.CreateProperty("FOV", Variant.Type.Float, PropertyHint.Range, "1,179,0.1"));

			properties.Add(ExtensionMethods.CreateProperty("Camera Mode", Variant.Type.Int, PropertyHint.Enum, "Hall,Field,Static"));

			if (cameraMode == CameraModes.Static)
			{
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Autoset Position", Variant.Type.Bool));

				if (!autosetStaticPosition)
					properties.Add(ExtensionMethods.CreateProperty("Position Settings/Static Position", Variant.Type.Vector3));
			}
			else
			{
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Distance", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Backstep Distance Addition", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Height", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));

				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Pitch Mode", Variant.Type.Int, PropertyHint.Enum, pitchMode.EnumToString()));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Yaw Mode", Variant.Type.Int, PropertyHint.Enum, yawMode.EnumToString()));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Pitch Angle", Variant.Type.Float, PropertyHint.Range, "-360,360,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Yaw Angle", Variant.Type.Float, PropertyHint.Range, "-360,360,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Allow Rolling", Variant.Type.Bool));

				properties.Add(ExtensionMethods.CreateProperty("Tracking Settings/Vertical Tracking Mode", Variant.Type.Int, PropertyHint.Enum, verticalTrackingMode.EnumToString()));
			}

			properties.Add(ExtensionMethods.CreateProperty("Position Settings/H_Offset", Variant.Type.Float));
			properties.Add(ExtensionMethods.CreateProperty("Position Settings/V_Offset", Variant.Type.Float));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Custom FOV":
					return useCustomFOV;
				case "FOV":
					return fov;

				case "Camera Mode":
					return (int)cameraMode;
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

				case "Tracking Settings/Vertical Tracking Mode":
					return (int)verticalTrackingMode;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Custom FOV":
					useCustomFOV = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "FOV":
					fov = (float)value;
					break;

				case "Camera Mode":
					cameraMode = (CameraModes)(int)value;
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
					pitchMode = (OverrideModes)(int)value;
					break;
				case "Rotation Settings/Yaw Mode":
					yawMode = (OverrideModes)(int)value;
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

				case "Tracking Settings/Vertical Tracking Mode":
					verticalTrackingMode = (TrackingModes)(int)value;
					break;

				default:
					return false;
			}

			return true;
		}
		#endregion

		public bool useCustomFOV;
		/// <summary> Camera's FOV. </summary>
		public float fov;

		public CameraModes cameraMode;
		public enum CameraModes
		{
			Hall,
			Field,
			Static,
		}

		public bool IsHallCamera => cameraMode == CameraSettingsResource.CameraModes.Hall;
		public bool IsFieldCamera => cameraMode == CameraSettingsResource.CameraModes.Field;
		public bool IsStaticCamera => cameraMode == CameraSettingsResource.CameraModes.Static;

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

		/// <summary> How should the camera track the player vertical position? </summary>
		public TrackingModes verticalTrackingMode;
		public enum TrackingModes
		{
			Move, //Move the camera
			Rotate, //Rotate the camera
		}

		//Rotation settings
		public enum OverrideModes
		{
			Add,
			Override,
		}
		/// <summary> Override mode for pitch (x-axis rotation). </summary>
		public OverrideModes pitchMode;
		/// <summary> Override mode for yaw (y-axis rotation). </summary>
		public OverrideModes yawMode;
		/// <summary> Pitch (x-axis rotation) angle, in radians. </summary>
		public float pitchAngle;
		/// <summary> Yaw (y-axis rotation) angle, in radians. </summary>
		public float yawAngle;
		/// <summary> Should the camera roll (z-axis rotation) to match the ground's angle? </summary>
		public bool isRollEnabled;
	}
}
