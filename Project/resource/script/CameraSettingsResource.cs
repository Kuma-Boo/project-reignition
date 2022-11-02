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

			if (!isStaticCamera)
			{
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Distance", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Height", Variant.Type.Float, PropertyHint.Range, "0,32,0.1"));

				properties.Add(ExtensionMethods.CreateProperty("Position Settings/Follow Horizontal", Variant.Type.Bool));

				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/" + nameof(enableZTilting), Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/" + nameof(pitchMode), Variant.Type.Int, PropertyHint.Enum, "Add, Override"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/" + nameof(yawMode), Variant.Type.Int, PropertyHint.Enum, "Add, Override"));
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/" + nameof(viewAngle), Variant.Type.Vector2));
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
				case "Static Camera":
					return isStaticCamera;
				case "Position Settings/Static Position":
					return staticPosition;
				case "Position Settings/Autoset Position":
					return autosetStaticPosition;

				case "Position Settings/Distance":
					return distance;
				case "Position Settings/Height":
					return height;
				case "Position Settings/Follow Horizontal":
					return isTrackingHorizontal;

				case "Rotation Settings/" + nameof(enableZTilting):
					return enableZTilting;
				case "Rotation Settings/" + nameof(pitchMode):
					return (int)pitchMode;
				case "Rotation Settings/" + nameof(yawMode):
					return (int)yawMode;
				case "Rotation Settings/" + nameof(viewAngle):
					return viewAngle;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Static Camera":
					isStaticCamera = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Position Settings/Static Position":
					staticPosition = (Vector3)value;
					break;
				case "Position Settings/Autoset Position":
					autosetStaticPosition = (bool)value;
					NotifyPropertyListChanged();
					break;

				case "Position Settings/Distance":
					distance = (float)value;
					break;
				case "Position Settings/Height":
					height = (float)value;
					break;
				case "Position Settings/Follow Horizontal":
					isTrackingHorizontal = (bool)value;
					break;

				case "Rotation Settings/" + nameof(enableZTilting):
					enableZTilting = (bool)value;
					break;
				case "Rotation Settings/" + nameof(pitchMode):
					pitchMode = (OverrideMode)(int)value;
					break;
				case "Rotation Settings/" + nameof(yawMode):
					yawMode = (OverrideMode)(int)value;
					break;
				case "Rotation Settings/" + nameof(viewAngle):
					viewAngle = (Vector2)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		/// Track the player from a static position? </summary>
		public bool isStaticCamera;
		/// <summary> Position to view from. </summary>
		public Vector3 staticPosition;
		/// <summary> Automatically set staticPosition based on CameraTrigger.cs. </summary>
		public bool autosetStaticPosition;

		//Dynamic camera settings
		public float distance = 1.5f;
		public float height; //View offset. Doesn't affect rotations, only translation based on camera's vertical direction.
		public bool isTrackingHorizontal = true; //Follow the player horizontally? Generally turned on.

		public enum OverrideMode
		{
			Add,
			Override,
		}
		public OverrideMode pitchMode;
		public OverrideMode yawMode;
		public Vector2 viewAngle; //View angle, in degrees

		public bool enableZTilting; //Tilts the camera along the z axis to match the ground angle
	}
}
