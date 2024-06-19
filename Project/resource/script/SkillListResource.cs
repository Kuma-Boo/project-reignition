using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Dev keys for all possible skills in the game, in numerical order. </summary>
public enum SkillKeys
{
	// Passive skills
	AllRounder, // Reduces acceleration loss caused by steep terrain
	PearlCollector, // Makes collecting pearls easier
	RingCollector, // Makes collecting rings easier

	// Ring skills
	RingStart, // Start with some rings at the game's start
	RingRespawn, // Respawn with a few rings handy
	RingDamage, // Reduce the number of rings lost when taking damage

	// Slide skills
	SlideAttack, // Replace slide with an attack
	SlideDefense, // Intangible to attacks when sliding

	// Stomp skills
	StompDash, // Gives a speed boost when stomping/jump canceling
	StompAttack, // Replace jump cancel with an attack
	RocketStart, // Press a button during countdown for a speedboost

	// Jump skills
	DownCancel, // Negate knockback by pressing the jump button
	SplashJump, // Bounce the player when jump dashing into an obstacle
	LandDash, // Gives a speed boost when landing
	PerfectHomingAttack, // Perfect homing attack, Colors Ultimate style

	DriftExperience, // Manually perform a drift for more speed and points/exp
	Max, // Number of skills
}

[Tool]
/// <summary> Master skill list. </summary>
public partial class SkillListResource : Resource
{
	[Export]
	private string skillResourcePath;
	[Export]
	private Array<SkillResource> skillList = [];

	private SkillKeys editingSkill;

	[ExportGroup("DO NOT EDIT!")]
	/// <summary> Enum containing all skills. </summary>
	private Array<SkillKeys> skillKeyList = [];
	/// <summary> How much does the skill cost to equip? </summary>
	[Export]
	private Array<int> skillCostList = [];

	/// <summary> Returns the cost required to equip a skill. </summary>
	public int GetSkillCost(SkillKeys key) => GetSkill(key).Cost;
	public int GetSkillLevelRequirement(SkillKeys key) => GetSkill(key).LevelRequirement;
	public int GetSkillFireSoulRequirement(SkillKeys key) => GetSkill(key).FireSoulRequirement;

	/// <summary> Creates a skill. </summary>
	private SkillResource GetSkill(SkillKeys key)
	{
		foreach (var skill in skillList)
		{
			if (skill.Key == key)
				return skill;
		}

		GD.PushError($"Couldn't find a skill with the key: {key}!");
		return null;
	}

	// Rebuilds the skill list
	private void RebuildSkillList()
	{
		skillList.Clear();

		// Create missing skills
		for (int i = 0; i < (int)SkillKeys.Max; i++)
		{
			SkillKeys key = (SkillKeys)i;
			string targetFile = skillResourcePath + key.ToString() + ".tres";
			if (!ResourceLoader.Exists(targetFile))
				continue;

			SkillResource skill = ResourceLoader.Load<SkillResource>(targetFile);
			skillList.Add(skill);

			if (skill.Key != key)
				skill.Key = key;
		}
	}
}

public class SkillRing
{
	/// <summary> List of equipped skills. </summary>
	public Array<SkillKeys> equippedSkills = [];
	/// <summary> Cost of all equipped skills. </summary>
	public int TotalCost { get; set; }
	/// <summary> Amount of available skill points. </summary>
	public int MaxSkillPoints { get; private set; }

	public void RefreshSkillRingData(int level)
	{
		// +5 per level, starts at 10, ends at 500.
		MaxSkillPoints = 10;
		if (level > 1)
			MaxSkillPoints += (level - 1) * 5;

		TotalCost = 0;
		for (int i = 0; i < equippedSkills.Count; i++)
			TotalCost += Runtime.Instance.masterSkillList.GetSkillCost(equippedSkills[i]);
	}

	public static bool IsSkillUnlocked(SkillKeys skill)
	{
		int levelRequirement = Runtime.Instance.masterSkillList.GetSkillLevelRequirement(skill);
		int fireSoulRequirement = Runtime.Instance.masterSkillList.GetSkillFireSoulRequirement(skill);

		if (levelRequirement != 0 && SaveManager.ActiveGameData.level < levelRequirement)
			return false;

		if (fireSoulRequirement != 0 && SaveManager.ActiveGameData.fireSoul < fireSoulRequirement)
			return false;

		return true;
	}
}