using Godot;
using Godot.Collections;
using System.Linq;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Dev keys for all possible skills in the game, in numerical order. </summary>
public enum SkillKey
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
	private Array<SkillResource> skills = [];

	/// <summary> Gets the matching skill based on a SkillKey. </summary>
	public SkillResource GetSkill(SkillKey key)
	{
		foreach (var skill in skills)
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
		if (!Engine.IsEditorHint())
			return;

		skills.Clear();

		// Load skills from skill directory
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;
			string targetFile = skillResourcePath + key.ToString() + ".tres";
			if (!ResourceLoader.Exists(targetFile))
			{
				GD.Print($"Couldn't find file {targetFile}.");
				continue;
			}

			Resource resource = ResourceLoader.Load<SkillResource>(targetFile, "SkillResource");
			GD.PrintT(targetFile, resource, resource is SkillResource);
			SkillResource skill = (SkillResource)resource;
			skills.Add(skill);
			ResourceSaver.Singleton.Save(skill, targetFile, ResourceSaver.SaverFlags.None);

			if (skill.Key != key)
				skill.Key = key;
		}

		// Reorder skill list
		List<SkillResource> skillList = [.. skills.ToArray()];
		skillList.Sort(new SkillRing.KeySorter());

		skills.Clear();
		skills.AddRange(skillList);

		ResourceSaver.Singleton.Save(this, skillResourcePath + "_SkillList.tres", ResourceSaver.SaverFlags.None);
		GD.Print("Skill List has been rebuilt.");
	}
}

public class SkillRing
{
	/// <summary> List of equipped skills. </summary>
	public Array<SkillKey> equippedSkills = [];
	/// <summary> Cost of all equipped skills. </summary>
	public int TotalCost { get; set; }
	/// <summary> Amount of available skill points. </summary>
	public int MaxSkillPoints { get; private set; }

	public static int CalculateMaxSkillPoints(int level)
	{
		// Calculates how many skill points the player has based on their level
		int skillPoints = 10; // Start at 10
		if (level > 1)
			skillPoints += (level - 1) * 5; // +5 per level--ends at 500.

		return skillPoints;
	}

	public void RefreshSkillRingData(int level)
	{
		MaxSkillPoints = CalculateMaxSkillPoints(level);

		TotalCost = 0;
		for (int i = 0; i < equippedSkills.Count; i++)
			TotalCost += Runtime.Instance.SkillList.GetSkill(equippedSkills[i]).Cost;
	}

	/// <summary> Equips a skill onto the skill ring. </summary>
	public bool EquipSkill(SkillKey key, bool allowSkillPointOverflow = false)
	{
		if (equippedSkills.Contains(key))
			return false; // Already equipped

		if (!allowSkillPointOverflow) // Check for total cost
		{
			int targetTotalCost = TotalCost + Runtime.Instance.SkillList.GetSkill(key).Cost;
			if (targetTotalCost > MaxSkillPoints)
				return false; // Too expensive!
		}

		equippedSkills.Add(key);
		TotalCost += Runtime.Instance.SkillList.GetSkill(key).Cost; // Take skill points
		return true;
	}

	/// <summary> Unequips a skill from the skill ring. </summary>
	public bool UnequipSkill(SkillKey key)
	{
		if (equippedSkills.Remove(key))
		{
			TotalCost -= Runtime.Instance.SkillList.GetSkill(key).Cost; // Refund skill points
			return true;
		}

		return false;
	}

	/// <summary> Checks whether a skill is unlocked on the active save file. </summary>
	public static bool IsSkillUnlocked(SkillKey key)
	{
		SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);

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

	public void SortByCost()
	{

	}

	/// <summary> Sorts skill resources based on their key (number). </summary>
	public class KeySorter : IComparer<SkillResource>
	{
		int IComparer<SkillResource>.Compare(SkillResource x, SkillResource y) => x.Key.CompareTo(y.Key);
	}

	/// <summary> Sorts skill resources based on their cost. </summary>
	public class CostSorter : IComparer<SkillResource>
	{
		int IComparer<SkillResource>.Compare(SkillResource x, SkillResource y) => x.Key.CompareTo(y.Key);
	}
}