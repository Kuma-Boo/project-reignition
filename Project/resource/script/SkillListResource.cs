using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Dev keys of all possible skills, sorted in the order they appear on the in-game list.
	/// </summary>
	public enum SkillKeyEnum
	{
		//Passive skills
		AllRounder, // Reduces acceleration loss caused by steep terrain
		PearlCollector, // Makes collecting pearls easier
		RingSaver, // Reduce the number of rings lost when taking damage
		RingBonus, // Start with some rings at the game's start
		RingRespawn, // Respawn with a few rings handy

		// Slide skills
		FlameSlide, // Replace slide with an attack
		AegisSlide, // Intangible to attacks when sliding

		// Action skills
		CancelDash, // Gives a speed boost when landing
		LandingDash, // Gives a speed boost when landing
		RocketStart, // Press a button during countdown for a speedboost
		FlameStomp, // Replace jump cancel with an attack
		SplashJump, // Bounce the player when jump dashing into an obstacle
		ManualDrift, // Manually perform a drift for more speed and points/exp

		PerfectHomingAttack, // Colors Ultimate style

		Max, // Number of skills
	}


	[Tool]
	/// <summary>
	/// Master skill list.
	/// </summary>
	public partial class SkillListResource : Resource
	{
		private SkillKeyEnum editingSkill;

		[ExportGroup("DO NOT EDIT!")]
		/// <summary> Enum containing all skills. </summary>
		private Array<SkillKeyEnum> skillKeyList = new();
		/// <summary> How much does the skill cost to equip? </summary>
		[Export]
		private Array<int> skillCostList = new();

		// Unlock requiremnts
		/// <summary> What level does the player need to be for the skill to be unlocked? </summary>
		[Export]
		private Array<int> levelList = new();
		/// <summary> How many fire souls does the player need to unlock this skill? </summary>
		[Export]
		private Array<int> fireSoulList = new();


		#region Editor
		private const string REBUILD_KEY = "Rebuild Skill List";
		private const string EDIT_KEY = "Editing Skill";
		private const string COST_KEY = "Skill/Cost";
		private const string LEVEL_KEY = "Skill/Level Requirement";
		private const string FIRE_SOUL_KEY = "Skill/Fire Soul Requirement";

		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty(REBUILD_KEY, Variant.Type.Bool),
				ExtensionMethods.CreateProperty(EDIT_KEY, Variant.Type.Int, PropertyHint.Enum, editingSkill.EnumToString()),

				ExtensionMethods.CreateProperty(COST_KEY, Variant.Type.Int),

				ExtensionMethods.CreateProperty(LEVEL_KEY, Variant.Type.Int, PropertyHint.Range, "0, 99"),
				ExtensionMethods.CreateProperty(FIRE_SOUL_KEY, Variant.Type.Int, PropertyHint.Range, "0, 126"),
			};

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case REBUILD_KEY:
					return false;
				case EDIT_KEY:
					return (int)editingSkill;
				case COST_KEY:
					return skillCostList[GetSkillIndex(editingSkill)];
				case LEVEL_KEY:
					return levelList[GetSkillIndex(editingSkill)];
				case FIRE_SOUL_KEY:
					return fireSoulList[GetSkillIndex(editingSkill)];
				default:
					break;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case REBUILD_KEY:
					RebuildSkillList();
					NotifyPropertyListChanged();
					break;
				case EDIT_KEY:
					editingSkill = (SkillKeyEnum)(int)value;
					NotifyPropertyListChanged();
					break;
				case COST_KEY:
					skillCostList[GetSkillIndex(editingSkill)] = (int)value;
					break;
				case LEVEL_KEY:
					levelList[GetSkillIndex(editingSkill)] = (int)value;
					break;
				case FIRE_SOUL_KEY:
					fireSoulList[GetSkillIndex(editingSkill)] = (int)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion


		private int GetSkillIndex(SkillKeyEnum key)
		{
			for (int i = 0; i < skillKeyList.Count; i++)
			{
				if (skillKeyList[i] == key)
					return i;
			}

			CreateSkill(key);
			return skillKeyList.Count - 1;
		}


		public int GetSkillCost(SkillKeyEnum key) => skillCostList[GetSkillIndex(key)];
		public int GetSkillLevelRequirement(SkillKeyEnum key) => levelList[GetSkillIndex(key)];
		public int GetSkillFireSoulRequirement(SkillKeyEnum key) => fireSoulList[GetSkillIndex(key)];


		/// <summary> Creates a skill. </summary>
		private void CreateSkill(SkillKeyEnum key)
		{
			skillKeyList.Add(key);
			skillCostList.Add(0);
		}


		/// <summary> Reorders a skill. </summary>
		private void ReorderSkill(int currentIndex, int targetIndex)
		{
			skillKeyList.Insert(targetIndex, skillKeyList[currentIndex]);
			skillCostList.Insert(targetIndex, skillCostList[currentIndex]);

			// Remove old skill
			currentIndex++;
			skillKeyList.RemoveAt(currentIndex);
			skillCostList.RemoveAt(currentIndex);
		}


		// Rebuilds the skill list
		private void RebuildSkillList()
		{
			// Create missing skills
			for (int i = 0; i < (int)SkillKeyEnum.Max; i++)
			{
				int skillIndex = GetSkillIndex((SkillKeyEnum)i);
				ReorderSkill(skillIndex, i);
			}

			// Remove extras
			skillKeyList.Resize((int)SkillKeyEnum.Max);
			skillCostList.Resize((int)SkillKeyEnum.Max);
			levelList.Resize((int)SkillKeyEnum.Max);
			fireSoulList.Resize((int)SkillKeyEnum.Max);
		}
	}

	public class SkillRing
	{
		/// <summary> List of equipped skills. </summary>
		public Array<SkillKeyEnum> equippedSkills = new();
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

		public static bool IsSkillUnlocked(SkillKeyEnum skill)
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
}

