using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

[Tool]
public partial class SpecialBookPage : Resource
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = [];

		properties.Add(ExtensionMethods.CreateCategory("Basic Settings"));
		properties.Add(ExtensionMethods.CreateProperty("Page Type", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(PageType)));
		switch (PageType)
		{
			case PageTypeEnum.Music:
				properties.Add(ExtensionMethods.CreateProperty("Audio Stream", Variant.Type.String, PropertyHint.FilePath, "*.mp3,*.wav"));
				break;
			case PageTypeEnum.Video:
				properties.Add(ExtensionMethods.CreateProperty("Event Path", Variant.Type.String, PropertyHint.FilePath, "*.tscn"));
				break;
			case PageTypeEnum.Achievement:
				properties.Add(ExtensionMethods.CreateProperty("Achievement Key", Variant.Type.StringName));
				properties.Add(ExtensionMethods.CreateProperty("Achievement Name", Variant.Type.StringName));
				properties.Add(ExtensionMethods.CreateProperty("Achievement Type", Variant.Type.Int, PropertyHint.Range, "1, 5"));
				break;
		}

		if (PageType == PageTypeEnum.Achievement) // Achievements are unlocked through unique means
			return properties;

		properties.Add(ExtensionMethods.CreateCategory("Unlock Settings"));
		properties.Add(ExtensionMethods.CreateProperty("Unlock Type", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(UnlockType)));
		if (UnlockType == UnlockTypeEnum.BigCameo || UnlockType == UnlockTypeEnum.SpecificLevel)
			properties.Add(ExtensionMethods.CreateProperty("Level Resource", Variant.Type.Object, PropertyHint.ResourceType, "LevelDataResource"));

		if (UnlockType == UnlockTypeEnum.SpecificWorld)
			properties.Add(ExtensionMethods.CreateProperty("World Resource", Variant.Type.Object, PropertyHint.ResourceType, "WorldDataResource"));

		if (UnlockType == UnlockTypeEnum.MedalCount || UnlockType == UnlockTypeEnum.SpecificLevel || UnlockType == UnlockTypeEnum.SpecificWorld)
			properties.Add(ExtensionMethods.CreateProperty("Rank Requirement", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(Rank)));

		if (UnlockType == UnlockTypeEnum.MedalCount)
			properties.Add(ExtensionMethods.CreateProperty("Medal Count", Variant.Type.Int, PropertyHint.Range, "0,1,or_greater"));

		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Page Type":
				PageType = (PageTypeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case "Audio Stream":
				AudioStreamPath = (string)value;
				break;
			case "Event Path":
				VideoEventPath = (string)value;
				break;

			case "Achievement Key":
				AchievementKey = (StringName)value;
				break;
			case "Achievement Name":
				AchievementName = (StringName)value;
				break;
			case "Achievement Type":
				AchievementType = (int)value;
				break;

			case "Unlock Type":
				UnlockType = (UnlockTypeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case "Level Resource":
				LevelData = (LevelDataResource)value;
				break;
			case "World":
				WorldData = (WorldDataResource)value;
				break;
			case "Rank Requirement":
				Rank = (RankEnum)(int)value;
				break;
			case "Medal Count":
				MedalCount = (int)value;
				break;

			default:
				return false;
		}

		return true;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case "Page Type":
				return (int)PageType;
			case "Audio Stream":
				return AudioStreamPath;
			case "Event Path":
				return VideoEventPath;

			case "Achievement Key":
				return AchievementKey;
			case "Achievement Name":
				return AchievementName;
			case "Achievement Type":
				return AchievementType;

			case "Unlock Type":
				return (int)UnlockType;
			case "Level Resource":
				return LevelData;
			case "World":
				return WorldData;
			case "Rank Requirement":
				return (int)Rank;
			case "Medal Count":
				return MedalCount;
		}

		return base._Get(property);
	}
	#endregion

	/// <summary> Specifies what type of media this page is. </summary>
	public PageTypeEnum PageType { get; private set; }
	public enum PageTypeEnum
	{
		Image,
		Music,
		Video,
		Achievement,
	}

	/// <summary> Path to the audio stream. </summary>
	public string AudioStreamPath { get; private set; }
	/// <summary> Path to the video's cutscene event. </summary>
	public string VideoEventPath { get; private set; }

	public string AchievementKey { get; private set; }
	public string AchievementName { get; private set; }
	public int AchievementType { get; private set; }

	public UnlockTypeEnum UnlockType { get; private set; }
	public enum UnlockTypeEnum
	{
		MedalCount,
		SpecificLevel,
		SpecificWorld,
		BigCameo,
		Key,
	}

	/// <summary> Determines what kind of completion is needed for an unlock type. </summary>
	public RankEnum Rank { get; private set; }
	public enum RankEnum
	{
		Gold,
		Silver,
		Bronze,
		Completed
	}

	/// <summary> Determines how many medals are needed for unlock requirements to be met. </summary>
	public int MedalCount { get; private set; }

	public LevelDataResource LevelData { get; private set; }
	public WorldDataResource WorldData { get; private set; }

	/// <summary> Is this page unimplemented? </summary>
	private bool IsInvalid()
	{
		if (PageType == PageTypeEnum.Music && string.IsNullOrEmpty(AudioStreamPath))
			return true;

		if (PageType == PageTypeEnum.Video && string.IsNullOrEmpty(VideoEventPath))
			return true;

		return false;
	}

	/// <summary> Calculates whether this page should be unlocked or not. </summary>
	public bool IsUnlocked()
	{
		if (IsInvalid())
			return false;

		if (PageType == PageTypeEnum.Achievement)
			return SaveManager.SharedData.achievements.Contains(AchievementName);

		if (UnlockType == UnlockTypeEnum.MedalCount)
		{
			return Rank switch
			{
				RankEnum.Gold => SaveManager.SharedData.LevelData.GoldMedalCount >= MedalCount,
				RankEnum.Silver => SaveManager.SharedData.LevelData.SilverMedalCount >= MedalCount,
				RankEnum.Bronze => SaveManager.SharedData.LevelData.BronzeMedalCount >= MedalCount,
				_ => false,
			};
		}

		if (UnlockType == UnlockTypeEnum.BigCameo)
			return SaveManager.SharedData.bigCameos.Contains(LevelData.LevelID);

		if (UnlockType == UnlockTypeEnum.SpecificLevel)
		{
			if (SaveManager.SharedData.LevelData.GetClearStatus(LevelData.LevelID) != SaveManager.LevelSaveData.LevelStatus.Cleared)
				return false;

			if (Rank == RankEnum.Completed)
				return true;

			int currentRank = SaveManager.SharedData.LevelData.GetRank(LevelData.LevelID);
			int targetRank = 3 - (int)Rank; // Convert from RankEnum to save format
			return targetRank <= currentRank;
		}

		if (UnlockType == UnlockTypeEnum.SpecificWorld)
		{
			if (WorldData == null)
				GD.PushError("No World Data!");

			for (int i = 0; i < WorldData.Levels.Length; i++)
			{
				if (SaveManager.SharedData.LevelData.GetRank(WorldData.Levels[i].LevelID) != 3)
					return false;
			}

			return true;
		}

		return false;
	}

	/// <summary> Constructs a localized string that describes the unlock requirements. </summary>
	public string GetLocalizedUnlockRequirements()
	{
		string localizedString = "???";
		int number = 0;

		if (IsInvalid())
			return localizedString;

		if (PageType == PageTypeEnum.Achievement)
			return localizedString;

		// NOTE: This switch statement only covers the retail game. Add more conditions as needed.
		switch (UnlockType)
		{
			case UnlockTypeEnum.MedalCount:
				localizedString = Tr("spb_hint_silver");
				number = MedalCount;
				break;
			case UnlockTypeEnum.SpecificLevel:
				localizedString = Tr(Rank == RankEnum.Completed ? "spb_hint_complete" : "spb_hint_gold");
				number = LevelData.LevelIndex;
				break;
			case UnlockTypeEnum.SpecificWorld:
				localizedString = Tr("spb_hint_all_gold");
				break;
			case UnlockTypeEnum.BigCameo:
				return localizedString;
				/* TODO uncomment this after Big has been implemented
				localizedString = Tr("spb_hint_big");
				number = LevelData.LevelIndex;
				break;
				*/
		}

		localizedString = localizedString.Replace("[NUMBER]", number.ToString());
		localizedString = localizedString.Replace("[AREA]", Tr(AbbreviateWorld()));

		return localizedString;
	}

	private string AbbreviateWorld()
	{
		if (UnlockType == UnlockTypeEnum.SpecificWorld)
			return WorldData.WorldKey;

		if (LevelData == null)
			return string.Empty;

		string worldKey = LevelData.LevelID.ToString().Split('_')[0];
		return worldKey switch
		{
			"lp" => "lost_prologue",
			"so" => "sand_oasis",
			"dj" => "dinosaur_jungle",
			"ef" => "evil_foundry",
			"lr" => "levitated_ruin",
			"ps" => "pirate_storm",
			"sd" => "skeleton_dome",
			"np" => "night_palace",
			_ => string.Empty,
		};
	}
}
