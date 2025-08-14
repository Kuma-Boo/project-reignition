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
		properties.Add(ExtensionMethods.CreateProperty("Localization Key", Variant.Type.String));
		properties.Add(ExtensionMethods.CreateProperty("Page Type", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(PageType)));
		switch (PageType)
		{
			case PageTypeEnum.Music:
				properties.Add(ExtensionMethods.CreateProperty("Audio Stream", Variant.Type.String, PropertyHint.FilePath, "*.mp3,*.wav"));
				break;
			case PageTypeEnum.Video:
				properties.Add(ExtensionMethods.CreateProperty("Event Path", Variant.Type.String, PropertyHint.FilePath, "*.tscn"));
				break;
		}


		properties.Add(ExtensionMethods.CreateCategory("Unlock Settings"));
		properties.Add(ExtensionMethods.CreateProperty("Unlock Type", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(UnlockType)));
		if (UnlockType == UnlockTypeEnum.BigCameo || UnlockType == UnlockTypeEnum.SpecificLevel)
			properties.Add(ExtensionMethods.CreateProperty("Level Resource", Variant.Type.Object, PropertyHint.ResourceType, "LevelDataResource"));

		if (UnlockType == UnlockTypeEnum.SpecificWorld)
			properties.Add(ExtensionMethods.CreateProperty("World", Variant.Type.Int, PropertyHint.Enum, ExtensionMethods.EnumToString(World)));

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
			case "Localization Key":
				LocalizationKey = (string)value;
				break;
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


			case "Unlock Type":
				UnlockType = (UnlockTypeEnum)(int)value;
				NotifyPropertyListChanged();
				break;
			case "Level Resource":
				LevelData = (LevelDataResource)value;
				break;
			case "World":
				World = (SaveManager.WorldEnum)(int)value;
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
			case "Localization Key":
				return LocalizationKey;
			case "Page Type":
				return (int)PageType;
			case "Audio Stream":
				return AudioStreamPath;
			case "Event Path":
				return VideoEventPath;

			case "Unlock Type":
				return (int)UnlockType;
			case "Level Resource":
				return LevelData;
			case "World":
				return (int)World;
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
	}

	/// <summary> The localization key. </summary>
	public string LocalizationKey { get; private set; }

	/// <summary> Path to the audio stream. </summary>
	public string AudioStreamPath { get; private set; }
	/// <summary> Path to the video's cutscene event. </summary>
	public string VideoEventPath { get; private set; }

	public UnlockTypeEnum UnlockType { get; private set; }
	public enum UnlockTypeEnum
	{
		MedalCount,
		SpecificLevel,
		SpecificWorld,
		BigCameo,
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
	public SaveManager.WorldEnum World { get; private set; }

	/// <summary> Calculates whether this page should be unlocked or not. </summary>
	public bool IsUnlocked()
	{
		return false;
	}

	/// <summary> Constructs a localized string that describes the unlock requirements. </summary>
	public string GetLocalizedUnlockRequirements()
	{
		string localizedString = string.Empty;
		return localizedString;
	}
}
