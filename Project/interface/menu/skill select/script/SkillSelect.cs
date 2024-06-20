using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

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

	private SkillListResource SkillList => Runtime.Instance.completeSkillList;
	private SkillRing ActiveSkillRing => SaveManager.ActiveGameData.skillRing;

	private int cursorPosition;

	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private const float ScrollSmoothing = .05f;
	/// <summary> How much to scroll per skill. </summary>
	private readonly int ScrollInterval = 63;
	/// <summary> Number of skills on a single page. </summary>
	private readonly int PageSize = 8;

	private Array<SkillOption> skillOptionList = [];

	protected override void SetUp()
	{
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;
			SkillOption newSkill = skillOption.Instantiate<SkillOption>();
			newSkill.Key = key;
			newSkill.Number = i + 1;
			newSkill.Cost = SkillList.GetSkill(key).Cost;
			newSkill.RedrawData();

			optionContainer.AddChild(newSkill);
			skillOptionList.Add(newSkill);
		}

		description.ShowDescription();
		description.SetText(skillOptionList[VerticalSelection].DescriptionKey);

		Redraw();
		base.SetUp();
	}


	public override void _Process(double _)
	{
		float targetScrollPosition = (160 * scrollRatio) - 80;
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, ScrollSmoothing);
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign != 0)
		{
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, (int)SkillKey.Max);

			if (VerticalSelection == 0 || VerticalSelection == (int)SkillKey.Max - 1)
				cursorPosition = scrollAmount = VerticalSelection;
			else if ((inputSign < 0 && cursorPosition == 1) || (inputSign > 0 && cursorPosition == 6))
				scrollAmount += inputSign;
			else
				cursorPosition += inputSign;

			scrollAmount = Mathf.Clamp(scrollAmount, 0, (int)SkillKey.Max - PageSize);
			scrollRatio = (float)scrollAmount / ((int)SkillKey.Max - PageSize);
			cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
			optionContainer.Position = new(optionContainer.Position.X, -scrollAmount * ScrollInterval);
			cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
			description.SetText(skillOptionList[VerticalSelection].DescriptionKey);

			animator.Play("select");
			if (!isSelectionScrolling)
				StartSelectionTimer();
		}

		// TODO Change sort method when horizontal input is detected
	}

	protected override void Confirm()
	{
		if (!ToggleSkill(skillOptionList[VerticalSelection].Key))
			return;

		Redraw();
	}

	private void Redraw()
	{
		skillPointLabel.Text = ActiveSkillRing.TotalCost.ToString("000") + "/" + ActiveSkillRing.MaxSkillPoints.ToString("000");
		skillPointFill.Scale = new(ActiveSkillRing.TotalCost / (float)ActiveSkillRing.MaxSkillPoints, skillPointFill.Scale.Y);
		skillOptionList[VerticalSelection].IsSkillActive = ActiveSkillRing.equippedSkills.Contains(skillOptionList[VerticalSelection].Key);
	}

	private bool ToggleSkill(SkillKey key)
	{
		if (ActiveSkillRing.equippedSkills.Remove(key))
		{
			ActiveSkillRing.TotalCost -= SkillList.GetSkill(key).Cost;
			animator.Play("unequip");
			return true;
		}

		// Ensure the player has enough skill points
		int targetTotalCost = ActiveSkillRing.TotalCost + SkillList.GetSkill(key).Cost;
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
