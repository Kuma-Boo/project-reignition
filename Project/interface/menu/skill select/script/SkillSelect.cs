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
	private Label levelLabel;
	[Export]
	private Label skillPointLabel;

	private SkillListResource SkillList => Runtime.Instance.SkillList;
	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

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
	private Array<SkillOption> currentSkillOptionList = [];

	protected override void SetUp()
	{
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;

			if (SkillList.GetSkill(key) == null)
				continue;

			SkillOption newSkill = skillOption.Instantiate<SkillOption>();
			newSkill.Skill = SkillList.GetSkill(key);
			newSkill.Number = i + 1;
			newSkill.Initialize();

			optionContainer.AddChild(newSkill);
			skillOptionList.Add(newSkill);
		}

		base.SetUp();
	}

	public override void _Process(double _)
	{
		float targetScrollPosition = (160 * scrollRatio) - 80;
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, ScrollSmoothing);
	}

	protected override void Cancel()
	{
		SaveManager.SaveGameData();
		animator.Play("hide");
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign != 0)
		{
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, currentSkillOptionList.Count);

			if (currentSkillOptionList.Count <= PageSize)
			{
				// Disable scrolling
				scrollAmount = 0;
				scrollRatio = 0;
				cursorPosition = VerticalSelection;
			}
			else
			{
				// Update scroll
				if (VerticalSelection == 0 || VerticalSelection == skillOptionList.Count - 1)
					cursorPosition = scrollAmount = VerticalSelection;
				else if ((inputSign < 0 && cursorPosition == 1) || (inputSign > 0 && cursorPosition == 6))
					scrollAmount += inputSign;
				else
					cursorPosition += inputSign;

				scrollAmount = Mathf.Clamp(scrollAmount, 0, currentSkillOptionList.Count - PageSize);
				scrollRatio = (float)scrollAmount / (currentSkillOptionList.Count - PageSize);
				cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
			}

			optionContainer.Position = new(optionContainer.Position.X, -scrollAmount * ScrollInterval);
			cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
			description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);

			animator.Play("select");
			animator.Seek(0);
			animator.Advance(0);
			if (!isSelectionScrolling)
				StartSelectionTimer();
		}

		// TODO Change sort method when speedbreak is pressed
	}

	public override void ShowMenu()
	{
		// Update visible skill list to account for multiple save files
		currentSkillOptionList.Clear();
		for (int i = 0; i < skillOptionList.Count; i++)
		{
			SkillKey key = (SkillKey)i;
			skillOptionList[i].Visible = SaveManager.ActiveSkillRing.IsSkillUnlocked(key);
			if (skillOptionList[i].Visible)
				currentSkillOptionList.Add(skillOptionList[i]);
		}

		Redraw();
		base.ShowMenu();
	}

	protected override void Confirm()
	{
		if (!ToggleSkill())
			return;

		Redraw();
	}

	private void Redraw()
	{
		skillPointLabel.Text = ActiveSkillRing.TotalCost.ToString("000") + "/" + ActiveSkillRing.MaxSkillPoints.ToString("000");
		skillPointFill.Scale = new(ActiveSkillRing.TotalCost / (float)ActiveSkillRing.MaxSkillPoints, skillPointFill.Scale.Y);
		foreach (SkillOption option in currentSkillOptionList)
			option.Redraw();

		description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);
		levelLabel.Text = Tr("skill_select_level").Replace("0", SaveManager.ActiveGameData.level.ToString("00"));
	}

	private bool ToggleSkill()
	{
		SkillKey key = currentSkillOptionList[VerticalSelection].Skill.Key;
		if (ActiveSkillRing.UnequipSkill(key))
		{
			animator.Play("unequip");
			return true;
		}

		if (ActiveSkillRing.EquipSkill(key))
		{
			animator.Play("equip");
			return true;
		}

		return false; // Something failed
	}

	private void SortMenuByCost()
	{
		currentSkillOptionList.Sort();
	}
}
