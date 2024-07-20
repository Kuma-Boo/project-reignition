using Godot;
using Godot.Collections;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay;

public class SkillRing
{
	public bool IsSkillEquipped(SkillKey key) => EquippedSkills.Contains(key);
	/// <summary> List of equipped skills. </summary>
	public Array<SkillKey> EquippedSkills => SaveManager.ActiveGameData.equippedSkills;
	/// <summary> Cost of all equipped skills. </summary>
	public int TotalCost { get; private set; }
	/// <summary> Amount of available skill points. </summary>
	public int MaxSkillPoints { get; private set; }

	/// <summary> Calculates how many skill points the player has based on their level. </summary>
	public static int CalculateSkillPointsByLevel(int level)
	{
		int skillPoints = 10; // Start at 10
		if (level > 1)
			skillPoints += (level - 1) * 5; // +5 per level--ends at 500.

		return skillPoints;
	}

	/// <summary> Updates how many skill points the player has based on their save data. </summary>
	public void UpdateTotalSkillPoints() => MaxSkillPoints = CalculateSkillPointsByLevel(SaveManager.ActiveGameData.level);

	/// <summary> Updates the total cost based on the skills currently equipped on a skill ring. </summary>
	public void UpdateTotalCost()
	{
		TotalCost = 0;
		for (int i = 0; i < EquippedSkills.Count; i++)
			TotalCost += Runtime.Instance.SkillList.GetSkill(EquippedSkills[i]).Cost;
	}

	/// <summary> Checks whether a conflicting skill is already equipped. </summary>
	public SkillKey IsConflictingSkillEquipped(SkillResource skill)
	{
		if (skill.SkillConflicts == null)
			return SkillKey.Max;

		foreach (SkillKey conflict in skill.SkillConflicts)
		{
			if (!IsSkillEquipped(conflict))
				continue;

			return conflict;
		}

		return SkillKey.Max; // No conflicts
	}

	/// <summary> Equips a skill onto the skill ring. </summary>
	public bool EquipSkill(SkillKey key, bool allowSkillPointOverflow = false)
	{
		if (EquippedSkills.Contains(key))
			return false; // Already equipped

		SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);
		SkillResource conflict = Runtime.Instance.SkillList.GetSkill(SaveManager.ActiveSkillRing.IsConflictingSkillEquipped(skill));
		if (conflict != null)
		{
			GD.Print($"You cannot equip {conflict.NameKey} when {skill.NameKey} is active.");
			return false;
		}

		if (!allowSkillPointOverflow) // Check for total cost
		{
			int targetTotalCost = TotalCost + skill.Cost;
			if (targetTotalCost > MaxSkillPoints)
				return false; // Too expensive!
		}

		EquippedSkills.Add(key);
		TotalCost += skill.Cost; // Take skill points
		return true;
	}

	/// <summary> Unequips a skill from the skill ring. </summary>
	public bool UnequipSkill(SkillKey key)
	{
		if (EquippedSkills.Remove(key))
		{
			TotalCost -= Runtime.Instance.SkillList.GetSkill(key).Cost; // Refund skill points
			return true;
		}

		return false;
	}

	/// <summary> Checks whether a skill is unlocked on the active save file. </summary>
	public bool IsSkillUnlocked(SkillKey key)
	{
		if (DebugManager.Instance.UseDemoSave)
			return true;

		if (IsSkillEquipped(key)) // Equipped skills should be unlocked automatically to allow the player to unequip them...
			return true;

		SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);

		if (skill == null) // Skill hasn't been created yet...
			return false;

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
			// Check global medal requirements
			if (skill.MedalRequirement == 3 && SaveManager.ActiveGameData.GoldMedalCount < skill.MedalRequirementCount)
				return false;
			if (skill.MedalRequirement == 2 && SaveManager.ActiveGameData.SilverMedalCount < skill.MedalRequirementCount)
				return false;
			if (skill.MedalRequirement == 1 && SaveManager.ActiveGameData.BronzeMedalCount < skill.MedalRequirementCount)
				return false;
		}

		// Finish with fire soul requirements
		return SaveManager.ActiveGameData.FireSoulCount >= skill.FireSoulRequirement;
	}

	/// <summary> Updates a skill ring to match the active game data. </summary>
	public void LoadFromActiveData()
	{
		UpdateTotalSkillPoints();
		UpdateTotalCost();
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

	/// <summary> Sorts skill resources based on their key augment index. </summary>
	public class AugmentSorter : IComparer<SkillResource>
	{
		int IComparer<SkillResource>.Compare(SkillResource x, SkillResource y)
		{
			if (x.AugmentIndex == y.AugmentIndex)
				GD.PushWarning($"Augment {x.ResourcePath} and {y.ResourcePath} contain the same augment index of {x.AugmentIndex}!");
			return x.AugmentIndex.CompareTo(y.AugmentIndex);
		}
	}
}

/// <summary> Dev keys for all possible skills in the game, in numerical order. </summary>
public enum SkillKey
{
	// Control skills
	Autorun,

	// Passive skills
	AllRounder, // Reduces acceleration loss caused by steep terrain
	PearlRange, // Makes collecting pearls easier

	// Ring skills
	RingSpawn, // Start with some rings at the game's start
	RingRespawn, // Respawn with a few rings handy
	RingDamage, // Reduce the number of rings lost when taking damage

	// Slide skills
	SlideAttack, // Replace slide with an attack
	SlideDefense, // Intangible to attacks when sliding
	SlideDistance, // Allows sliding to last longer

	// Stomp skills
	StompDash, // Gives a speed boost when stomping/jump canceling
	StompAttack, // Replace jump cancel with an attack
	RocketStart, // Press a button during countdown for a speedboost

	// Jump skills
	RankPreview, // Shows the current rank on the heads-up display
	DownCancel, // Negate knockback by pressing the jump button
	SplashJump, // Bounce the player when jump dashing into an obstacle
	LandDash, // Gives a speed boost when landing
	PerfectHomingAttack, // Perfect homing attack, Colors Ultimate style

	DriftExperience, // Manually perform a drift for more speed and points/exp

	CrestWind,
	CrestFire,
	CrestDark,

	Max, // Number of skills
}