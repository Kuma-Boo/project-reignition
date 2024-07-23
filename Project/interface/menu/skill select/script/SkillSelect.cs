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
			SkillResource skill = SkillList.GetSkill(key);

			if (skill == null)
			{
				skillOptionList.Add(null);
				continue;
			}

			SkillOption newSkill = skillOption.Instantiate<SkillOption>();
			newSkill.Skill = skill;
			newSkill.Number = i + 1;
			newSkill.Initialize();

			skillOptionList.Add(newSkill);
			optionContainer.AddChild(newSkill);

			if (!newSkill.Skill.HasAugments) // Skip augments
				continue;

			for (int j = 0; j < newSkill.Skill.Augments.Count; j++)
			{
				SkillOption newAugment = skillOption.Instantiate<SkillOption>();
				newAugment.Skill = newSkill.Skill.Augments[j];
				newAugment.Number = newAugment.Skill.AugmentIndex;
				newAugment.Initialize();
				newAugment.Visible = false;

				AddChild(newAugment); // Augments are added as direct children to the skill select menu
				newSkill.augments.Add(newAugment);
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

			UpdateDescription();
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
				if (VerticalSelection == 0 || VerticalSelection == currentSkillOptionList.Count - 1)
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

			UpdateDescription();
		}

		// TODO Change sort method when speedbreak is pressed
	}

	private void UpdateDescription()
	{
		if (IsEditingAugment)
		{
			if (AugmentSelection == 0)
				description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);
			else
				description.SetText(currentSkillOptionList[VerticalSelection].augments[AugmentSelection - 1].Skill.DescriptionKey);

			return;
		}

		int augmentIndex = ActiveSkillRing.GetAugmentIndex(currentSkillOptionList[VerticalSelection].Skill.Key);
		if (currentSkillOptionList[VerticalSelection].Skill.HasAugments && augmentIndex != 0)
			description.SetText(currentSkillOptionList[VerticalSelection].augments[augmentIndex - 1].Skill.DescriptionKey);
		else
			description.SetText(currentSkillOptionList[VerticalSelection].Skill.DescriptionKey);
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
			if (skillOptionList[i] == null)
				continue;

			SkillKey key = (SkillKey)i;
			skillOptionList[i].Visible = SaveManager.ActiveSkillRing.IsSkillUnlocked(key);
			if (!skillOptionList[i].Visible)
			{
				GD.Print(key);
				continue;
			}

			currentSkillOptionList.Add(skillOptionList[i]);

			// Process augments
			if (!skillOptionList[i].Skill.HasAugments)
				continue;

			UpdateAugmentHierarchy(skillOptionList[i], i);
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
		foreach (Node option in optionContainer.GetChildren())
		{
			if (option is not SkillOption)
				continue;

			((SkillOption)option).Redraw();
		}

		if (IsEditingAugment)
		{
			foreach (Node option in augmentContainer.GetChildren())
			{
				if (option is not SkillOption)
					continue;

				((SkillOption)option).Redraw();
			}
		}

		UpdateDescription();
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
			return true;
		}

		if (ActiveSkillRing.EquipSkill(key, IsEditingAugment ? AugmentSelection : 0))
		{
			animator.Play("equip");
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

		if (baseSkill.GetParent() == this)
		{
			RemoveChild(baseSkill);
			augmentContainer.CallDeferred("add_child", baseSkill);
			baseSkill.Visible = true;
		}

		for (int i = 0; i < baseSkill.augments.Count; i++)
		{
			SkillOption augment = baseSkill.augments[i];
			augment.Visible = true;

			if (!SaveManager.ActiveSkillRing.IsSkillUnlocked(augment.Skill)) // Don't add locked skills
				continue;

			if (augment.GetParent() == this)
			{
				augment.GetParent().RemoveChild(augment);
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
		for (int i = augmentContainer.GetChildCount() - 1; i >= 0; i--)
		{
			SkillOption augment = augmentContainer.GetChild<SkillOption>(i);
			augment.Visible = false;
			augmentContainer.RemoveChild(augment);
			CallDeferred("add_child", augment);
		}

		EnableProcessing();
	}

	private void ToggleAugmentMenu()
	{
		SkillOption baseSkill = currentSkillOptionList[VerticalSelection];
		int augmentIndex = SaveManager.ActiveSkillRing.GetAugmentIndex(currentSkillOptionList[VerticalSelection].Skill.Key);
		if (IsEditingAugment)
		{
			cursorPosition = augmentIndex;
			AugmentSelection = augmentIndex;

			// Move the skill option node to the augment menu
			SkillOption skillOption = optionContainer.GetChild<SkillOption>(VerticalSelection);
			skillOption.Number = skillOption.Skill.AugmentIndex;
			optionContainer.RemoveChild(skillOption);
			augmentContainer.CallDeferred("add_child", skillOption);

			// Sort augment options
			augmentContainer.CallDeferred("move_child", baseSkill, baseSkill.GetAugmentOffset());
			for (int i = 0; i < baseSkill.augments.Count; i++)
			{
				int index = baseSkill.augments[i].Skill.AugmentIndex;
				baseSkill.augments[i].Number = index;
				augmentContainer.CallDeferred("move_child", baseSkill.augments[i], index < 0 ? i : i + 1);
			}
		}
		else
		{
			cursorPosition = VerticalSelection - scrollAmount;

			// Move the correct skill augment node to the skill option menu
			SkillOption skillOption = augmentContainer.GetChild<SkillOption>(augmentIndex);
			skillOption.Number = VerticalSelection + 1;
			augmentContainer.RemoveChild(skillOption);
			optionContainer.CallDeferred("add_child", skillOption);
			optionContainer.CallDeferred("move_child", skillOption, VerticalSelection);
		}

		CallDeferred(MethodName.Redraw);
		cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
	}

	/// <summary> Updates a skill option so the correct augment appears on the skill select menu. </summary>
	private void UpdateAugmentHierarchy(SkillOption baseSkill, int index)
	{
		SkillKey key = baseSkill.Skill.Key;
		SkillOption shownSkill = baseSkill;
		int augmentIndex = ActiveSkillRing.GetAugmentIndex(key) - 1;
		for (int i = 0; i < baseSkill.augments.Count; i++)
		{
			if (i == augmentIndex)
			{
				shownSkill = baseSkill.augments[i];
				continue;
			}

			baseSkill.augments[i].Visible = false;
			if (baseSkill.augments[i].GetParent() != this)
			{
				baseSkill.augments[i].GetParent().RemoveChild(baseSkill.augments[i]);
				CallDeferred("add_child", baseSkill.augments[i]);
			}
		}

		// Use the equipped augment instead of the base skill
		shownSkill.Number = index + 1;
		shownSkill.Visible = true;

		// Move the active augment to the correct position in the skill menu
		if (shownSkill.GetParent() != optionContainer)
		{
			shownSkill.GetParent().RemoveChild(shownSkill);
			optionContainer.CallDeferred("add_child", shownSkill);
			optionContainer.CallDeferred("move_child", shownSkill, index);
			shownSkill.CallDeferred(SkillOption.MethodName.Redraw);
		}

		// Move the base skill if needed
		if (shownSkill != baseSkill && baseSkill.GetParent() != this)
		{
			baseSkill.Number = 0;
			baseSkill.Visible = false;
			optionContainer.RemoveChild(baseSkill);
			CallDeferred("add_child", baseSkill);
		}
	}
}