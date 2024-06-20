using System.Text.RegularExpressions;
using Godot;

namespace Project.Gameplay;

/// <summary> A single skill. </summary>
[Tool]
public partial class SkillResource : Resource
{
	[ExportCategory("General Settings")]
	[Export]
	/// <summary> Which skill does this resource represent? </summary>
	public SkillKey Key { get; set; }
	/// <summary> The skill's elemental type. </summary>
	[Export]
	public SkillElement Element { get; private set; }
	public enum SkillElement
	{
		Wind,
		Fire,
		Dark
	}

	/// <summary> The skill's category type. </summary>
	[Export]
	public SkillCategory Category { get; private set; }
	public enum SkillCategory
	{
		Ground,
		Air,
		Contract, // Called "hidden" skills in the original game
		Augment, // Called "assist" skills in the original game
		Combat, // Called "damage" skills in the original game
		Experience,
		Crest, // Called "special" skills in the original game
	}
	/// <summary> How many skill points does this skill cost to equip? </summary>
	[Export]
	public int Cost { get; private set; }

	// Unlock requiremnts
	/// <summary> The level required to unlock this skill. Don't confuse this with StageRequirement. </summary>
	[ExportCategory("Unlock Settings")]
	[Export(PropertyHint.Range, "0, 99")]
	public int LevelRequirement { get; private set; }
	/// <summary> The level id of the stage required to unlock this skill. Pair with RankRequirement to specify the unlock criteria. </summary>
	[Export]
	public StringName StageRequirement { get; private set; }
	/// <summary> The medal rank required to unlock this skill. </summary>
	[Export(PropertyHint.Enum, "None, Bronze, Silver, Gold")]
	public int MedalRequirement { get; private set; }
	/// <summary> How many stages the player must have medals on. The required medal is determined by RankRequirement. </summary>
	[Export]
	public int MedalRequirementCount { get; private set; }
	/// <summary> How many fire souls does the player need to unlock this skill? </summary>
	[Export(PropertyHint.Range, "0, 162, 1")]
	public int FireSoulRequirement { get; private set; }

	/// <summary> Converts the internal key to snake case for localization. </summary>
	[GeneratedRegex(@"(\w)([A-Z])")]
	private static partial Regex SnakeCaseRegex();
	public string NameString => SnakeCaseRegex().Replace(Key.ToString(), "$1_$2").ToLower();
	/// <summary> Returns the localization key for this skill. </summary>
	public StringName NameKey => $"skill_{NameString}";
	/// <summary> Returns the localization description key for this skill. </summary>
	public StringName DescriptionKey => $"skill_{NameString}_description";
}