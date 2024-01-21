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
		[Export]
		private Sprite2D scrollbar;
		[Export]
		private Sprite2D skillPointFill;
		[Export]
		private Label skillPointLabel;

		private SkillListResource MasterSkillList => Runtime.Instance.masterSkillList;
		private SkillRing ActiveSkillRing => SaveManager.ActiveGameData.skillRing;

		private int cursorPosition;


		private int scrollAmount;
		private float scrollRatio;
		private Vector2 scrollVelocity;
		private const float SCROLL_SMOOTHING = .05f;
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

			Redraw();
			base.SetUp();
		}


		public override void _Process(double _)
		{
			scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * (160 * scrollRatio - 80), ref scrollVelocity, SCROLL_SMOOTHING);
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
				scrollRatio = (float)scrollAmount / ((int)SkillKeyEnum.Max - PAGE_SIZE);
				cursorPosition = Mathf.Clamp(cursorPosition, 0, PAGE_SIZE - 1);
				optionContainer.Position = new(optionContainer.Position.X, -scrollAmount * SCROLL_INTERVAL);
				cursor.Position = Vector2.Up * -cursorPosition * SCROLL_INTERVAL;
				description.SetText(skillList[VerticalSelection].DescriptionKey);

				animator.Play("select");
				if (!isSelectionScrolling)
					StartSelectionTimer();

				return;
			}

			// TODO Change sort method when horizontal input is detected
		}


		protected override void Confirm()
		{
			if (!ToggleSkill(skillList[VerticalSelection].Key))
				return;

			Redraw();
		}


		private void Redraw()
		{
			skillPointLabel.Text = ActiveSkillRing.TotalCost.ToString("000") + "/" + ActiveSkillRing.MaxSkillPoints.ToString("000");
			skillPointFill.Scale = new(ActiveSkillRing.TotalCost / (float)ActiveSkillRing.MaxSkillPoints, skillPointFill.Scale.Y);
			skillList[VerticalSelection].IsSkillActive = ActiveSkillRing.equippedSkills.Contains(skillList[VerticalSelection].Key);
		}


		private bool ToggleSkill(SkillKeyEnum key)
		{
			if (ActiveSkillRing.equippedSkills.Contains(key))
			{
				ActiveSkillRing.equippedSkills.Remove(key);
				ActiveSkillRing.TotalCost -= MasterSkillList.GetSkillCost(key);
				animator.Play("unequip");
				return true;
			}

			// Ensure the player has enough skill points
			int targetTotalCost = ActiveSkillRing.TotalCost + MasterSkillList.GetSkillCost(key);
			if (targetTotalCost > ActiveSkillRing.MaxSkillPoints)
				return false;

			ActiveSkillRing.equippedSkills.Add(key);
			ActiveSkillRing.TotalCost = targetTotalCost;
			animator.Play("equip");
			return true;
		}


		private void SortMenuByCost()
		{

		}
	}
}
