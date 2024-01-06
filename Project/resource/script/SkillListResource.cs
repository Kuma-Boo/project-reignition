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
		LandingDash, // Gives a speed boost when landing
		RocketStart, // Press a button during countdown for a speedboost
		FlameStomp, // Replace jump cancel with an attack
		SplashJump, // Bounce the player when jump dashing into an obstacle
		ManualDrift, // Manually perform a drift for more speed and points/exp

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
		[Export]
		private Array<SkillKeyEnum> skillKeyList = new();
		[Export]
		private Array<int> skillCostList = new(); // How much does the skill cost to equip?


		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Rebuild Skill List", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Editing Skill", Variant.Type.Int, PropertyHint.Enum, editingSkill.EnumToString()),

				ExtensionMethods.CreateProperty("Skill/Cost", Variant.Type.Int),
			};

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Rebuild Skill List":
					return false;
				case "Editing Skill":
					return (int)editingSkill;
				case "Skill/Cost":
					return skillCostList[GetSkillIndex(editingSkill)];
				default:
					break;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Rebuild Skill List":
					RebuildSkillList();
					NotifyPropertyListChanged();
					break;
				case "Editing Skill":
					editingSkill = (SkillKeyEnum)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Skill/Cost":
					skillCostList[GetSkillIndex(editingSkill)] = (int)value;
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
	}
}

