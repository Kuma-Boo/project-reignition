using Godot;
using Godot.Collections;

namespace Project.Core
{
	[Tool]
	public partial class LevelDataResource : Resource
	{
		public enum MissionTypes
		{
			None, // Add a goal node or a boss so the player doesn't get stuck!
			Objective, // Add custom nodes that call IncrementObjective()
			Ring, // Collect a certain amount of rings
			Pearl, // Collect a certain amount of pearls (normally zero)
			Enemy, // Destroy a certain amount of enemies
			Race, // Race against an enemy
		}

		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Level ID", Variant.Type.StringName),
				ExtensionMethods.CreateProperty("Level Path", Variant.Type.String),
				ExtensionMethods.CreateProperty("Story Event Index", Variant.Type.Int, PropertyHint.Range, "-1,31"),

				ExtensionMethods.CreateProperty("Is Side Mission", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Has Fire Souls", Variant.Type.Bool),


				ExtensionMethods.CreateProperty("Mission/Type", Variant.Type.Int, PropertyHint.Enum, MissionType.EnumToString()),
				ExtensionMethods.CreateProperty("Mission/Type Key", Variant.Type.String),
				ExtensionMethods.CreateProperty("Mission/Description Key", Variant.Type.String),
				ExtensionMethods.CreateProperty("Mission/Disable Countdown", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Mission/Time Limit", Variant.Type.Int, PropertyHint.Range, "0,640"),
			};

			if (MissionType != MissionTypes.None && MissionType != MissionTypes.Race)
				properties.Add(ExtensionMethods.CreateProperty("Mission/Objective Count", Variant.Type.Int, PropertyHint.Range, "0,256"));

			properties.Add(ExtensionMethods.CreateProperty("Camera Settings", Variant.Type.Object));
			properties.Add(ExtensionMethods.CreateProperty("Dialog Library", Variant.Type.Object));

			properties.Add(ExtensionMethods.CreateProperty("Ranking/Skip Score", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Gold Time", Variant.Type.Int));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Silver Time", Variant.Type.Int));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Bronze Time", Variant.Type.Int));

			if (!skipScore)
			{
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Gold Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Silver Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Bronze Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
			}

			properties.Add(ExtensionMethods.CreateProperty("Completion/Delay", Variant.Type.Float, PropertyHint.Range, "0,2.5,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Completion/Lockout", Variant.Type.Object));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Level ID":
					return LevelID;
				case "Level Path":
					return LevelPath;
				case "Story Event Index":
					return StoryEventIndex;

				case "Is Side Mission":
					return IsSideMission;
				case "Has Fire Souls":
					return HasFireSouls;

				case "Mission/Type":
					return (int)MissionType;
				case "Mission/Type Key":
					return MissionTypeKey;
				case "Mission/Description Key":
					return MissionDescriptionKey;
				case "Mission/Disable Countdown":
					return DisableCountdown;
				case "Mission/Time Limit":
					return MissionTimeLimit;
				case "Mission/Objective Count":
					return MissionObjectiveCount;

					/*
					case "Camera Settings":
						return InitialCameraSettings;
					case "Dialog Library":
						return dialogLibrary;

					case "Ranking/Skip Score":
						return skipScore;
					case "Ranking/Gold Time":
						return goldTime;
					case "Ranking/Silver Time":
						return silverTime;
					case "Ranking/Bronze Time":
						return bronzeTime;
					case "Ranking/Gold Score":
						return goldScore;
					case "Ranking/Silver Score":
						return silverScore;
					case "Ranking/Bronze Score":
						return bronzeScore;

					case "Completion/Delay":
						return completionDelay;
					case "Completion/Lockout":
						return CompletionLockout;
					*/
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Level ID":
					LevelID = (StringName)value;
					break;
				case "Level Path":
					LevelPath = (string)value;
					break;
				case "Story Event Index":
					StoryEventIndex = (int)value;
					break;

				case "Is Side Mission":
					IsSideMission = (bool)value;
					break;
				case "Has Fire Souls":
					HasFireSouls = (bool)value;
					break;

				case "Mission/Type":
					MissionType = (MissionTypes)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Mission/Type Key":
					MissionTypeKey = (string)value;
					break;
				case "Mission/Description Key":
					MissionDescriptionKey = (string)value;
					break;
				case "Mission/Disable Countdown":
					DisableCountdown = (bool)value;
					break;
				case "Mission/Time Limit":
					MissionTimeLimit = (int)value;
					break;
				case "Mission/Objective Count":
					MissionObjectiveCount = (int)value;
					break;

				/*
				case "Camera Settings":
					InitialCameraSettings = (CameraSettingsResource)value;
					break;
				case "Dialog Library":
					dialogLibrary = (SFXLibraryResource)value;
					break;

				case "Ranking/Skip Score":
					skipScore = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Ranking/Gold Time":
					goldTime = (int)value;
					break;
				case "Ranking/Silver Time":
					silverTime = (int)value;
					break;
				case "Ranking/Bronze Time":
					bronzeTime = (int)value;
					break;
				case "Ranking/Gold Score":
					goldScore = (int)value;
					break;
				case "Ranking/Silver Score":
					silverScore = (int)value;
					break;
				case "Ranking/Bronze Score":
					bronzeScore = (int)value;
					break;

				case "Completion/Delay":
					completionDelay = (float)value;
					break;
				case "Completion/Lockout":
					CompletionLockout = (LockoutResource)value;
					break;
				*/
				default:
					return false;
			}

			return true;
		}
		#endregion


		private bool skipScore; // Don't use score when ranking (i.e. for bosses)

		/// <summary> Level's id - used for save data. </summary>
		public StringName LevelID { get; private set; }
		/// <summary> Path to the level's scene. </summary>
		public string LevelPath { get; private set; }

		/// <summary> Does this mission contain fire souls? </summary>
		public bool HasFireSouls { get; private set; }
		/// <summary> Should this mission be shown as optional? </summary>
		public bool IsSideMission { get; private set; }

		/// <summary> Localization key for the type of mission (Goal, Rampage, Rings, etc.). </summary>
		public string MissionTypeKey { get; private set; }
		/// <summary> Localization key for the more specific description. </summary>
		public string MissionDescriptionKey { get; private set; }


		/// <summary> Type of mission. </summary>
		public MissionTypes MissionType { get; private set; }
		/// <summary> What's the target amount for the mission objective? </summary>
		public int MissionObjectiveCount { get; private set; }
		/// <summary> Level time limit, in seconds. </summary>
		public float MissionTimeLimit { get; private set; }
		/// <summary> Should the countdown be disabled for this stage (i.e. bosses, control test, etc.)? </summary>
		public bool DisableCountdown { get; private set; }


		/// <summary> Story event index to play after completing the stage. Set to -1 if no story event is meant to be played. </summary>
		public int StoryEventIndex = -1;

	}
}
