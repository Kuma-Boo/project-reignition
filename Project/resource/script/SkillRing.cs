using System;
using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

public class SkillRing
{
	/// <summary> List of equipped skills. </summary>
	public Array<SkillKey> EquippedSkills => SaveManager.ActiveGameData.equippedSkills;
	/// <summary> List of equipped Augments. </summary>
	public Dictionary<SkillKey, int> EquippedAugments => SaveManager.ActiveGameData.equippedAugments;

	/// <summary> Checks whether a skill is equipped and has the correct augment index. </summary>
	public bool IsSkillEquipped(SkillResource skill) => IsSkillEquipped(skill.Key) && GetAugmentIndex(skill.Key) == skill.AugmentIndex;
	/// <summary> Checks whether a skill's key is equipped. </summary>
	public bool IsSkillEquipped(SkillKey key) => EquippedSkills.Contains(key);
	/// <summary> Returns the augment of a particular skill. </summary>
	public int GetAugmentIndex(SkillKey key) => EquippedAugments.TryGetValue(key, out int currentAugmentIndex) ? currentAugmentIndex : 0;

	/// <summary> Cost of all equipped skills. </summary>
	public int TotalCost { get; private set; }
	/// <summary> Amount of available skill points. </summary>
	public int MaxSkillPoints { get; private set; }
	/// <summary> Number of skills equipped per element. Indexes line up with SkillResource.SkillElement when casted to an int. </summary>
	private int[] SkillCountByElement = new int[(int)SkillResource.SkillElement.Count];
	public int GetSkillCountByElement(SkillResource.SkillElement element) => SkillCountByElement[(int)element];

	/// <summary> Returns whether the autorun skill is equipped, or the player has Gyro enabled. </summary>
	public bool IsAutorunActive => IsSkillEquipped(SkillKey.Autorun) || (SaveManager.Config.isGyroEnabled && Runtime.Instance.IsUsingController && Input.IsJoyMotionSensorsEnabled(Runtime.Instance.ActiveController));
	/// <summary> Returns whether the freeroam skill is equipped and the player doesn't have Gyro enabled. </summary>
	public bool IsFreeRoamActive => IsSkillEquipped(SkillKey.FreeRoam) && !(SaveManager.Config.isGyroEnabled && Runtime.Instance.IsUsingController && Input.IsJoyMotionSensorsEnabled(Runtime.Instance.ActiveController));

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
	private void UpdateTotalCost()
	{
		TotalCost = 0;
		for (int i = 0; i < EquippedSkills.Count; i++)
		{
			SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(EquippedSkills[i]);
			if (baseSkill == null) // Skill not found?
				continue;

			int augmentIndex = GetAugmentIndex(EquippedSkills[i]);
			if (augmentIndex == 0)
			{
				TotalCost += baseSkill.Cost;
				continue;
			}

			TotalCost += baseSkill.Augments[augmentIndex - 1].Cost;
		}
	}

	private void UpdateSkillCounts()
	{
		for (int i = 0; i < SkillCountByElement.Length; i++) // Reset counts
			SkillCountByElement[i] = 0;

		foreach (SkillKey key in EquippedSkills)
		{
			SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);
			skill = skill.GetAugment(GetAugmentIndex(key));
			SkillCountByElement[(int)skill.Element]++;
		}
	}

	/// <summary> Returns the precise skill resource that is conflicting with a given skill key. </summary>
	public SkillResource GetConflictingSkill(SkillKey key)
	{
		SkillKey conflictKey = IsConflictingSkillEquipped(key);
		SkillResource conflict = Runtime.Instance.SkillList.GetSkill(conflictKey);
		if (conflict.IsAugment)
			return conflict.GetAugment(GetAugmentIndex(conflictKey));

		return conflict;
	}

	/// <summary> Checks whether a conflicting skill is already equipped. </summary>
	public SkillKey IsConflictingSkillEquipped(SkillKey key)
	{
		SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);

		if (skill.SkillConflicts == null)
			return SkillKey.Count;

		foreach (string conflict in skill.SkillConflicts)
		{
			if (!Enum.TryParse(conflict, out SkillKey conflictKey))
				continue;

			if (!IsSkillEquipped(conflictKey))
				continue;

			return conflictKey;
		}

		return SkillKey.Count; // No conflicts
	}

	/// <summary> Equips a skill onto the skill ring. </summary>
	public SkillEquipStatusEnum EquipSkill(SkillKey key, int augmentIndex = 0, bool isForceToggled = false)
	{
		if (EquippedSkills.Contains(key) && augmentIndex == GetAugmentIndex(key))
			return SkillEquipStatusEnum.Equipped; // Already equipped

		SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(key);

		// Process conflicts
		SkillKey conflictingKey = SaveManager.ActiveSkillRing.IsConflictingSkillEquipped(baseSkill.Key);
		SkillResource conflict = Runtime.Instance.SkillList.GetSkill(conflictingKey);
		if (conflict != null)
		{
			GD.Print($"You cannot equip {conflict.NameKey} when {baseSkill.NameKey} is active.");
			return SkillEquipStatusEnum.ConflictEquip;
		}

		// Process augments
		if (baseSkill.HasAugments)
		{
			SkillResource augment = baseSkill.GetAugment(augmentIndex);
			if (augment == null)
			{
				GD.PushError($"Couldn't find augment with index {augmentIndex} on skill {baseSkill.Key}!");
				return SkillEquipStatusEnum.Missing;
			}

			int currentCost = IsSkillEquipped(key) ? baseSkill.GetAugment(GetAugmentIndex(key)).Cost : 0;
			if (!isForceToggled) // Check for total cost
			{
				int targetTotalCost = TotalCost - currentCost + augment.Cost;
				if (targetTotalCost > MaxSkillPoints)
					return SkillEquipStatusEnum.Expensive; // Too expensive!
			}

			if (!EquippedSkills.Contains(key))
				EquippedSkills.Add(key);
			TotalCost -= currentCost; // Refund currently equipped cost
			TotalCost += augment.Cost; // Take skill points
			SkillCountByElement[(int)augment.Element]++;

			// Update augment index
			if (EquippedAugments.ContainsKey(key))
				EquippedAugments[key] = augmentIndex;
			else
				EquippedAugments.Add(key, augmentIndex);

			return SkillEquipStatusEnum.Success;
		}

		// Not an augment skill
		if (!isForceToggled) // Check for total cost
		{
			int targetTotalCost = TotalCost + baseSkill.Cost;
			if (targetTotalCost > MaxSkillPoints)
				return SkillEquipStatusEnum.Expensive; // Too expensive!
		}

		int skillCount = SaveManager.ActiveSkillRing.GetSkillCountByElement(baseSkill.Element);
		if (skillCount < baseSkill.ElementRequirement && !isForceToggled)
			return SkillEquipStatusEnum.ElementRequirement;

		if (!EquippedSkills.Contains(key))
			EquippedSkills.Add(key);

		TotalCost += baseSkill.Cost; // Take skill points
		SkillCountByElement[(int)baseSkill.Element]++;
		return SkillEquipStatusEnum.Success;
	}

	/// <summary> Unequips a skill from the skill ring. </summary>
	public SkillKey UnequipSkill(SkillKey key, int augmentIndex = 0)
	{
		if (augmentIndex != GetAugmentIndex(key))
			return SkillKey.Count; // Augment index mismatch

		SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(key);
		SkillResource augment = baseSkill.GetAugment(augmentIndex);

		// Check for unequip requirements
		int resultingElementCount = SkillCountByElement[(int)augment.Element] - 1;
		foreach (SkillKey conflictKey in SaveManager.ActiveSkillRing.EquippedSkills)
		{
			SkillResource conflictSkill = Runtime.Instance.SkillList.GetSkill(conflictKey);
			if (conflictSkill.ElementRequirement == 0 || conflictSkill.Element != augment.Element || conflictSkill.Key == key)
				continue;

			if (resultingElementCount <= conflictSkill.ElementRequirement)
			{
				// Can't unequip bc of a different skill
				return conflictSkill.Key;
			}
		}

		ForceUnequipSkill(key, augmentIndex);
		return baseSkill.Key;
	}

	public void ForceUnequipSkill(SkillKey key, int augmentIndex = 0)
	{
		SkillResource targetSkill = Runtime.Instance.SkillList.GetSkill(key).GetAugment(augmentIndex);

		if (EquippedSkills.Remove(key))
		{
			TotalCost -= targetSkill.Cost; // Refund skill points
			SkillCountByElement[(int)targetSkill.Element]--;
		}
	}

	/// <summary> Resets a skill's augment index to 0. </summary>
	public void ResetAugmentIndex(SkillKey key)
	{
		if (EquippedAugments.ContainsKey(key))
			EquippedAugments[key] = 0;
	}

	/// <summary> Checks whether a skill is unlocked on the active save file. </summary>
	public bool IsSkillUnlocked(SkillKey key, bool countEquipAsUnlocked = true)
	{
		SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(key);
		if (IsSkillUnlocked(baseSkill, countEquipAsUnlocked))
			return true;

		if (baseSkill.HasAugments)
		{
			foreach (SkillResource augment in baseSkill.Augments)
			{
				if (IsSkillUnlocked(augment, countEquipAsUnlocked))
					return true;
			}
		}

		return false;
	}

	/// <summary> Overload method for checking a skill resource directly. </summary>
	public bool IsSkillUnlocked(SkillResource skill, bool countEquipAsUnlocked = true)
	{
		if (skill == null) // Skill hasn't been created yet...
			return false;

		if (skill.Key == SkillKey.Character)
			return ModManager.Instance.CharacterMods.Count != 0;

		if (TimeAttackManager.Instance.IsRunActive)
			return skill.Key != SkillKey.Darkspine; // Disables darkspine skill in timeattack mode

		if (DebugManager.Instance.UseDemoSave)
			return true;

		if (countEquipAsUnlocked && IsSkillEquipped(skill)) // Equipped skills should be unlocked automatically to allow the player to unequip them...
			return true;

		if (SaveManager.ActiveGameData.level < skill.LevelRequirement) // Under-leveled
			return false;

		// Check stage and medal requirements
		if (skill.StageRequirement?.IsEmpty == false) // Check required stage
		{
			if (SaveManager.ActiveGameData.LevelData.GetClearStatus(skill.StageRequirement) != SaveManager.LevelSaveData.LevelStatus.Cleared)
				return false; // Player didn't finish the required stage

			if (SaveManager.ActiveGameData.LevelData.GetRank(skill.StageRequirement) < skill.MedalRequirement)
				return false; // Best rank is too low
		}
		else
		{
			// Check global medal requirements
			if (skill.MedalRequirement == 3 && SaveManager.ActiveGameData.LevelData.GoldMedalCount < skill.MedalRequirementCount)
				return false;
			if (skill.MedalRequirement == 2 && SaveManager.ActiveGameData.LevelData.SilverMedalCount < skill.MedalRequirementCount)
				return false;
			if (skill.MedalRequirement == 1 && SaveManager.ActiveGameData.LevelData.BronzeMedalCount < skill.MedalRequirementCount)
				return false;
		}

		// Finish with fire soul requirements
		return SaveManager.ActiveGameData.LevelData.FireSoulCount >= skill.FireSoulRequirement;
	}

	public bool AreSkillsSingleElement(SkillResource.SkillElement element)
	{
		bool hasValidSkill = false;

		foreach (SkillKey key in EquippedSkills)
		{
			SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);
			if (skill.Element == element)
			{
				hasValidSkill = true;
				continue;
			}

			if (skill.Element != SkillResource.SkillElement.Config)
				return false;
		}

		return hasValidSkill;
	}

	/// <summary> Updates a skill ring to match the active game data. </summary>
	public void LoadFromActiveData()
	{
		ValidateSkills();
		UpdateTotalSkillPoints();
		UpdateTotalCost();
		UpdateSkillCounts();
	}

	public void ValidateSkills()
	{
		// Validate modded characters
		SkillResource characterSkill = Runtime.Instance.SkillList.GetSkill(SkillKey.Character);
		if (GetAugmentIndex(SkillKey.Character) > characterSkill.Augments.Count) // Referencing a mod that no longer exists
			EquippedSkills.Remove(SkillKey.Character);

		UpdateSkillCounts();

		for (int i = EquippedSkills.Count - 1; i >= 0; i--)
		{
			SkillResource skill = Runtime.Instance.SkillList.GetSkill(EquippedSkills[i]);

			if (skill.ElementRequirement == 0 || GetSkillCountByElement(skill.Element) >= skill.ElementRequirement)
				continue;

			EquippedSkills.Remove(EquippedSkills[i]);
			UpdateSkillCounts();
		}
	}

	public int UnlockedSkillCount()
	{
		int count = 0;
		for (int i = 0; i < (int)SkillKey.Count; i++)
		{
			if (i != (int)SkillKey.Character && IsSkillUnlocked((SkillKey)i))
				count++;
		}
		return count;
	}

	/// <summary> Sorts skill resources based on their key (number). </summary>
	public class KeySorter : System.Collections.Generic.IComparer<SkillResource>
	{
		int System.Collections.Generic.IComparer<SkillResource>.Compare(SkillResource x, SkillResource y) => x.Key.CompareTo(y.Key);
	}

	/// <summary> Sorts skill resources based on their cost. </summary>
	public class CostSorter : System.Collections.Generic.IComparer<SkillResource>
	{
		int System.Collections.Generic.IComparer<SkillResource>.Compare(SkillResource x, SkillResource y) => x.Cost.CompareTo(y.Cost);
	}

	/// <summary> Sorts skill resources based on their key augment index. </summary>
	public class AugmentSorter : System.Collections.Generic.IComparer<SkillResource>
	{
		int System.Collections.Generic.IComparer<SkillResource>.Compare(SkillResource x, SkillResource y)
		{
			if (x.AugmentIndex == y.AugmentIndex)
				GD.PushWarning($"Augment {x.ResourcePath} and {y.ResourcePath} contain the same augment index of {x.AugmentIndex}!");
			return x.AugmentIndex.CompareTo(y.AugmentIndex);
		}
	}
}

/// <summary> Return values for equipping skills. </summary>
public enum SkillEquipStatusEnum
{
	Success,
	Expensive,
	ConflictEquip,
	ConflictUnequip,
	Equipped,
	Missing,
	ElementRequirement
}

/// <summary> Dev keys for all possible skills in the game, in numerical order. </summary>
public enum SkillKey
{
	// Control skills
	Autorun,
	ChargeJump,
	VariableJumpHeight, // Allows finer control of jump height (can mess up beginners)
	FreeRoam, // Allows Sonic to turn around normally
	QuickStart, // Reduces the length of the countdown
	SlowTurn, // Decreases Sonic's turning sensitivity
	QuickTurn, // Increases Sonic's turning sensitivity
	LockedSoulGauge, // Limits the soul gauge to lvl 1 Sonic
	RankPreview, // Shows the current rank on the heads-up display

	SpeedUp, // Increases Sonic's top speed
	TractionUp, // Increases Sonic's traction
	TurnaroundUp, // Increases Sonic's friction
	GrindUp, // Increase Grinding speed
	AllRounder, // Reduces acceleration loss caused by steep terrain

	// Slide skills
	SlideAttack, // Replace slide with an attack
	SlideDefense, // Intangible to attacks when sliding
	SlideExp, // Grants EXP when sliding
	SlideDistance, // Allows sliding to last longer

	// Stomp skills
	StompDash, // Gives a speed boost when stomping/jump canceling
	StompExp, // Grants exp when landing
	StompAttack, // Replace jump cancel with an attack
	StompBounce, // Bounces the player when landing and holding the action button

	// Jump skills
	AccelJumpAttack, // Increases attack power of accel jump
	SpinJump, // Have Sonic instantly enter a spinning state
	ArmorJump, // Allow Sonic to bounce off enemies
	DoubleJump, // Allow Sonic to do another small hop in the air

	// Backflip skills
	BackstepAttack, // Increases attack power of Backflip

	// Landing skills
	LandDash, // Gives a speed boost when landing
	LandSoul, // Increases soul gauge when landing

	// Passive skills
	PearlRange, // Makes collecting pearls easier
	PearlRespawn, // Allows cheating death with enough soul power
	PearlDamage, // Reduces the amount of soul power lost when taking damage
	RingLossConvert, // Increases soul power when taking damage
	RingRange, // Makes collecting rings closer
	RingPearlConvert, // Converts rings to pearls when collected

	// Ring skills
	RingSpawn, // Start with some rings at the game's start
	RingRespawn, // Respawn with a few rings handy
	RingDamage, // Reduces the number of rings lost when taking damage

	// Special skills
	RocketStart, // Press a button during countdown for a speedboost
	DownCancel, // Negate knockback by pressing the jump button
	FireSoulLockon, // Allows locking onto fire souls
	SplashJump, // Bounce the player when jump dashing into an obstacle

	// Experience skills
	DriftExp, // Manually perform a drift for more speed and points/exp

	// New skills
	QuickStep, // Quick Step, Unleashed style
	PerfectHomingAttack, // Perfect homing attack, Colors Ultimate style
	GroundedHomingAttack, // Allows Sonic to homing attack from the ground (Frontiers style)
	AirBoost, // Allows Sonic to boost in the air
	LightSpeedDash, // SA2 style
	LightSpeedAttack, // SA2 style
	InstantSpeedBreak, // Instant speedbreak--Unleashed style

	// Crest skills
	CrestWind,
	CrestFire,
	CrestDark,

	Darkspine, // Completion reward

	Character, // For custom characters.
	Count, // Number of skills
}