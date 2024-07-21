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
	private VBoxContainer augmentContainer;
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

	private bool IsEditingAugment { get; set; }

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

			if (newSkill.Skill.Augments == null) // Create augments
				continue;

			for (int j = 0; j < newSkill.Skill.Augments.Count; j++)
			{
				SkillOption newAugment = skillOption.Instantiate<SkillOption>();
				newAugment.Skill = newSkill.Skill.Augments[j];
				newAugment.Number = newAugment.Skill.AugmentIndex;
				newAugment.Initialize();
				newSkill.augments.Add(newAugment);
				newSkill.AddChild(newAugment);
				newAugment.Visible = false;
			}
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
		if (IsEditingAugment)
		{
			HideAugmentMenu();
			return;
		}

		SaveManager.SaveGameData();
		animator.Play("hide");
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (IsEditingAugment)
		{
			if (inputSign != 0)
				AugmentSelection = WrapSelection(AugmentSelection + inputSign, augmentContainer.GetChildCount());

			cursor.Position = Vector2.Up * -AugmentSelection * ScrollInterval;
			UpdateCursor();
			return;
		}

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

			cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;

			UpdateCursor();
			optionContainer.Position = new(optionContainer.Position.X, -scrollAmount * ScrollInterval);
			description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);
		}

		// TODO Change sort method when speedbreak is pressed
	}

	private void UpdateCursor()
	{
		animator.Play("select");
		animator.Seek(0);
		animator.Advance(0);
		if (!isSelectionScrolling)
			StartSelectionTimer();
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

		if (IsEditingAugment)
		{
			foreach (Node option in augmentContainer.GetChildren())
			{
				if (option is not SkillOption)
					continue;

				((SkillOption)option).Redraw();
			}
		}

		description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);
		levelLabel.Text = Tr("skill_select_level").Replace("0", SaveManager.ActiveGameData.level.ToString("00"));
	}

	private bool ToggleSkill()
	{
		SkillKey key = currentSkillOptionList[VerticalSelection].Skill.Key;
		if (!IsEditingAugment && currentSkillOptionList[VerticalSelection].HasUnlockedAugments()) // Open the augment menu
		{
			ShowAugmentMenu();
			return false;
		}

		if (ActiveSkillRing.UnequipSkill(key, IsEditingAugment ? AugmentSelection : 0))
		{
			animator.Play("unequip");
			GD.Print("Unequipped");
			return true;
		}

		if (ActiveSkillRing.EquipSkill(key, IsEditingAugment ? AugmentSelection : 0))
		{
			animator.Play("equip");
			GD.Print("Equipped");
			return true;
		}

		return false; // Something failed
	}

	private int AugmentSelection { get; set; }

	private void ShowAugmentMenu()
	{
		animator.Play("augment-show");
		IsEditingAugment = true;
		DisableProcessing();
		SkillOption baseSkill = currentSkillOptionList[VerticalSelection];

		for (int i = 0; i < baseSkill.augments.Count; i++)
		{
			SkillOption augment = baseSkill.augments[i];
			augment.Visible = true;

			if (!SaveManager.ActiveSkillRing.IsSkillUnlocked(augment.Skill)) // Don't add locked skills
				continue;

			if (augment.IsInsideTree() && augment.GetParent() == baseSkill)
			{
				baseSkill.RemoveChild(augment);
				augmentContainer.CallDeferred("add_child", augment);
			}
		}
	}

	private void HideAugmentMenu()
	{
		animator.Play("augment-hide");
		IsEditingAugment = false;
		DisableProcessing();
	}

	private void OnAugmentClosed()
	{
		for (int i = 0; i < augmentContainer.GetChildCount(); i++)
		{
			SkillOption augment = augmentContainer.GetChild<SkillOption>(i);
			augment.Visible = false;

			if (augment.IsInsideTree() && augment.GetParent() == augmentContainer)
			{
				augmentContainer.RemoveChild(augment);
				currentSkillOptionList[VerticalSelection].CallDeferred("add_child", augment);
			}
		}

		EnableProcessing();
	}

	private void ToggleAugmentMenu()
	{
		SkillOption baseSkill = currentSkillOptionList[VerticalSelection];
		if (IsEditingAugment)
		{
			cursorPosition = SaveManager.ActiveSkillRing.GetAugmentIndex(currentSkillOptionList[VerticalSelection].Skill.Key);

			// Move the base skill option to the augment menu
			currentSkillOptionList[VerticalSelection].Number = 0;
			currentSkillOptionList[VerticalSelection].Redraw();
			currentSkillOptionList[VerticalSelection].GetParent().RemoveChild(baseSkill);
			augmentContainer.CallDeferred("add_child", baseSkill);
			augmentContainer.CallDeferred("move_child", baseSkill, baseSkill.GetAugmentPosition());

			for (int i = 0; i < baseSkill.augments.Count; i++)
				augmentContainer.CallDeferred("move_child", baseSkill.augments[i], i + 1);
		}
		else
		{
			cursorPosition = VerticalSelection - scrollAmount;

			// Move the correct skill to the skill menu
			currentSkillOptionList[VerticalSelection].Number = VerticalSelection + 1;
			augmentContainer.RemoveChild(currentSkillOptionList[VerticalSelection]);
			optionContainer.CallDeferred("add_child", currentSkillOptionList[VerticalSelection]);
			optionContainer.CallDeferred("move_child", currentSkillOptionList[VerticalSelection], VerticalSelection);
		}

		Redraw();
		cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
	}
}
