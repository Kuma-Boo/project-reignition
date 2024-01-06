using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus
{
	public partial class SkillSelect : Menu
	{
		[Export]
		private PackedScene skillOption;
		[Export]
		private VBoxContainer optionContainer;
		[Export]
		private Node2D cursor;
		[Export]
		private Description description;

		private SkillListResource MasterSkillList => Runtime.Instance.masterSkillList;
		private SkillRing ActiveSkillRing => SaveManager.ActiveGameData.skillRing;

		private int scrollAmount;
		private int cursorPosition;
		/// <summary> How much to scroll per skill. </summary>
		private readonly int SCROLL_INTERVAL = 63;
		/// <summary> Number of skills on a single page. </summary>
		private readonly int PAGE_SIZE = 8;

		private Array<SkillOption> skillList = new();

		protected override void SetUp()
		{
			for (int i = 0; i < (int)SkillKeyEnum.Max; i++)
			{
				SkillOption newSkill = skillOption.Instantiate<SkillOption>();
				newSkill.Key = (SkillKeyEnum)i;
				newSkill.Number = i + 1;
				newSkill.Cost = MasterSkillList.GetSkillCost(newSkill.Key);
				newSkill.IsSkillActive = ActiveSkillRing.equippedSkills.Contains(newSkill.Key);
				newSkill.RedrawData();

				optionContainer.AddChild(newSkill);
				skillList.Add(newSkill);
			}

			description.ShowDescription();
			description.SetText(skillList[VerticalSelection].DescriptionKey);

			base.SetUp();
		}


		protected override void UpdateSelection()
		{
			int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
			if (inputSign != 0)
			{
				VerticalSelection = WrapSelection(VerticalSelection + inputSign, (int)SkillKeyEnum.Max);

				if (VerticalSelection == 0 || VerticalSelection == (int)SkillKeyEnum.Max - 1)
					cursorPosition = scrollAmount = VerticalSelection;
				else if ((inputSign < 0 && cursorPosition == 1) || (inputSign > 0 && cursorPosition == 6))
					scrollAmount += inputSign;
				else
					cursorPosition += inputSign;

				scrollAmount = Mathf.Clamp(scrollAmount, 0, (int)SkillKeyEnum.Max - PAGE_SIZE);
				cursorPosition = Mathf.Clamp(cursorPosition, 0, PAGE_SIZE - 1);
				optionContainer.Position = new(optionContainer.Position.X, -scrollAmount * SCROLL_INTERVAL);
				cursor.Position = Vector2.Up * -cursorPosition * SCROLL_INTERVAL;
				description.SetText(skillList[VerticalSelection].DescriptionKey);

				if (!isSelectionScrolling)
					StartSelectionTimer();

				return;
			}
		}


		protected override void Confirm()
		{
			if (!ToggleSkill(skillList[VerticalSelection].Key))
				return;


			// TODO Play SFX
			skillList[VerticalSelection].IsSkillActive = ActiveSkillRing.equippedSkills.Contains(skillList[VerticalSelection].Key);
		}

		private bool ToggleSkill(SkillKeyEnum key)
		{
			if (ActiveSkillRing.equippedSkills.Contains(key))
			{
				ActiveSkillRing.equippedSkills.Remove(key);
				return true;
			}

			// TODO check if the player has enough SP to equip the skill
			ActiveSkillRing.equippedSkills.Add(key);
			return true;
		}


		private void SortMenuByCost()
		{

		}
	}
}
