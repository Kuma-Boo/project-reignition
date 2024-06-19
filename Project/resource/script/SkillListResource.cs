using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Dev keys for all possible skills in the game, in numerical order. </summary>
public enum SkillKeys
{
	// Passive skills
	AllRounder, // Reduces acceleration loss caused by steep terrain
	PearlRange, // Makes collecting pearls easier
	RingRange, // Makes collecting rings easier

	// Ring skills
	RingSpawn, // Start with some rings at the game's start
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

/// <summary> Master skill list. </summary>
[Tool]
public partial class SkillListResource : Resource
{
	public override Array<Dictionary> _GetPropertyList() => [ExtensionMethods.CreateProperty("Rebuild", Variant.Type.Bool)];

	public override bool _Set(StringName property, Variant value)
	{
		if (property == "Rebuild")
		{
			RebuildSkillList();
			NotifyPropertyListChanged();
		}

		return base._Set(property, value);
	}

	[Export]
	private string skillResourcePath;
	[Export]
	private Array<SkillResource> skillList = [];

	/// <summary> Creates a skill. </summary>
	public SkillResource GetSkill(SkillKeys key)
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
			{
				GD.Print($"Couldn't find file {targetFile}.");
				continue;
			}

			Resource resource = ResourceLoader.Load<SkillResource>(targetFile, "SkillResource");
			GD.PrintT(targetFile, resource, resource is SkillResource);
			SkillResource skill = (SkillResource)resource;
			skillList.Add(skill);

			if (skill.Key != key)
				skill.Key = key;
		}

		GD.Print("Skill List has been rebuilt.");
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
			TotalCost += Runtime.Instance.completeSkillList.GetSkill(equippedSkills[i]).Cost;
	}

	/// <summary> Checks whether a skill is unlocked on the active save file. </summary>
	public static bool IsSkillUnlocked(SkillKeys key)
	{
		SkillResource skill = Runtime.Instance.completeSkillList.GetSkill(key);

		if (SaveManager.ActiveGameData.level < skill.LevelRequirement) // Under-leveled
			return false;

		// Check stage and medal requirements
		if (skill.StageRequirement?.IsEmpty == false) // Check required stage
		{
			if (!SaveManager.ActiveGameData.levelData.ContainsKey(skill.StageRequirement))
				return false; // Player didn't finish the required stage

			if (SaveManager.ActiveGameData.GetRank(skill.StageRequirement) < skill.MedalRequirement)
				return false; // Best rank is too low
		}
		else
		{
			if (skill.MedalRequirement != 0) // Global medal conditions
			{
				// TODO check global medal requirements
				return false;
			}
		}

		// Finish with firesoul requirements
		return SaveManager.ActiveGameData.fireSoul >= skill.FireSoulRequirement;
	}
}