using Godot;
using Godot.Collections;

namespace Project.Gameplay;

/// <summary> A single skill. </summary>
[Tool]
[GlobalClass]
public partial class SkillResource : Resource
{
	/// <summary> Which skill does this resource represent? </summary>
	[ExportCategory("General Settings")]
	[Export]
	public SkillKey Key { get; set; }

	/// <summary> The skill's elemental type. </summary>
	[Export] public SkillElement Element { get; set; }
	public enum SkillElement
	{
		Wind,
		Fire,
		Dark,
		Config,
		Count
	}

	/// <summary> The skill's category type. </summary>
	[Export] public SkillCategory Category { get; set; }
	public enum SkillCategory
	{
		Ground,
		Air,
		Contract, // Called "hidden" skills in the original game
		Assist,
		Combat, // Called "damage" skills in the original game
		Experience,
		Crest, // Called "special" skills in the original game
		Setting, // Skills that help adjust how the game plays
	}

	/// <summary> How many skill points does this skill cost to equip? </summary>
	[Export] public int Cost { get; private set; }
	/// <summary> How many other skills of the same element need to be equipped first? </summary>
	[Export] public int ElementRequirement { get; private set; }

	// Unlock requiremnts
	/// <summary> The level required to unlock this skill. Don't confuse this with StageRequirement. </summary>
	[ExportCategory("Unlock Settings")]
	[Export(PropertyHint.Range, "0, 99")]
	public int LevelRequirement { get; private set; }
	/// <summary> The level id of the stage required to unlock this skill. Pair with RankRequirement to specify the unlock criteria. </summary>
	[Export] public StringName StageRequirement { get; private set; }
	/// <summary> The medal rank required to unlock this skill. </summary>
	[Export(PropertyHint.Enum, "None, Bronze, Silver, Gold")]
	public int MedalRequirement { get; private set; }
	/// <summary> How many stages the player must have medals on. The required medal is determined by RankRequirement. </summary>
	[Export] public int MedalRequirementCount { get; private set; }
	/// <summary> How many fire souls does the player need to unlock this skill? </summary>
	[Export(PropertyHint.Range, "0, 162, 1")]
	public int FireSoulRequirement { get; private set; }

	/// <summary> Converts the internal key to snake case for localization. </summary>
	public string NameString => Key.ToString().ToSnakeCase();
	/// <summary> Returns the localization key for this skill. </summary>
	public StringName NameKey
	{
		get
		{
			if (Key == SkillKey.Character && IsAugment)
				return CustomCharacterName;

			StringName name = $"skill_{NameString}";
			if (IsAugment)
				name += AugmentIndex.ToString();

			return name;
		}
	}
	/// <summary> Returns the localization description key for this skill. </summary>
	public StringName DescriptionKey
	{
		get
		{
			if (Key == SkillKey.Character && IsAugment)
				return Tr("skill_custom_character_description").Replace("[CHARACTER]", CustomCharacterName);

			StringName description = $"skill_{NameString}_description";
			if (IsAugment)
				description += AugmentIndex.ToString();

			return description;
		}
	}

	/// <summary> Reference to a skill that creates a conflict with the current skill. </summary>
	[Export] public Array<string> SkillConflicts { get; set; }

	/// <summary> Does this skill have augments? </summary>
	public bool HasAugments => Augments != null && Augments.Count != 0;
	/// <summary> Does this skill build off of a previous skill? </summary>
	public bool IsAugment => AugmentIndex != 0;
	[Export] public int AugmentIndex { get; set; }
	/// <summary> List of skill augments. </summary>
	[Export] public Array<SkillResource> Augments { get; set; }

	/// <summary> Returns an augment based on its index. Only call this on the base skill to avoid issues. </summary>
	public SkillResource GetAugment(int augmentIndex)
	{
		if (augmentIndex == 0) // Return the base skill
			return this;

		foreach (SkillResource augment in Augments)
		{
			if (augment.AugmentIndex == augmentIndex)
				return augment;
		}

		return null;
	}

	public string VisibilityKey => IsAugment ? $"{Key}{AugmentIndex}" : Key.ToString();

	[ExportGroup("Custom Character Settings")]
	/// <summary> The name of this custom character. Only used if key is set to Character. </summary>
	[Export] private string CustomCharacterName;
	[Export(PropertyHint.File)] public string NormalModel { get; private set; }
	[Export(PropertyHint.File)] public string SuperModel { get; private set; }
	[Export] public SFXLibraryResource VoiceLibraryOverride { get; private set; }
}