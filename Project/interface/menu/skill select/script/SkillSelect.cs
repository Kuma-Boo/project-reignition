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
	[Export]
	private AnimationPlayer alertAnimator;
	[Export]
	private Label alertLabel;
	private int AlertSelection;
	private bool IsAlertMenuActive { get; set; }

	private bool IsEditingAugment { get; set; }

	private SkillOption SelectedSkill => currentSkillOptionList[VerticalSelection];

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

			// Create base skill option
			for (int j = 0; j < newSkill.Skill.Augments.Count; j++)
			{
				SkillOption newAugment = skillOption.Instantiate<SkillOption>();
				newAugment.Skill = newSkill.Skill.Augments[j];
				newSkill.RegisterAugment(newAugment);
			}

			SkillOption baseAugment = skillOption.Instantiate<SkillOption>();
			baseAugment.Skill = newSkill.Skill;
			newSkill.RegisterAugment(baseAugment);
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
		if (IsAlertMenuActive)
		{
			if (AlertSelection == 1)
			{
				AlertSelection = 0;
				alertAnimator.Play("select-no");
				alertAnimator.Advance(0.0);
			}

			alertAnimator.Play("hide");
			return;
		}

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
		if (IsAlertMenuActive)
		{
			int input = Mathf.Sign(Input.GetAxis("move_left", "move_right"));
			if (input < 0 && AlertSelection == 0)
			{
				AlertSelection = 1;
				alertAnimator.Play("select-yes");
			}
			else if (input > 0 && AlertSelection == 1)
			{
				AlertSelection = 0;
				alertAnimator.Play("select-no");
			}

			return;
		}

		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (IsEditingAugment)
		{
			if (inputSign != 0)
				AugmentSelection = WrapSelection(AugmentSelection + inputSign, SelectedSkill.AugmentMenuCount);

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
				description.SetText(SelectedSkill.Skill.DescriptionKey);
			else
				description.SetText(SelectedSkill.GetAugmentDescription(AugmentSelection - 1));

			return;
		}

		int augmentIndex = ActiveSkillRing.GetAugmentIndex(SelectedSkill.Skill.Key);
		if (SelectedSkill.Skill.HasAugments && augmentIndex != 0)
			description.SetText(SelectedSkill.GetAugmentDescription(AugmentSelection - 1));
		else
			description.SetText(SelectedSkill.Skill.DescriptionKey);
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
			skillOptionList[i].Visible = false;

			if (!SaveManager.ActiveSkillRing.IsSkillUnlocked(key))
			{
				GD.Print(key);
				continue;
			}

			currentSkillOptionList.Add(skillOptionList[i]);
			skillOptionList[i].Visible = true;

			// Process augments
			UpdateAugmentHierarchy(skillOptionList[i]);
		}

		Redraw();
		base.ShowMenu();
	}

	protected override void Confirm()
	{
		if (IsAlertMenuActive)
		{
			if (AlertSelection == 1)
			{
				// Toggle skills
				SwapConflictSkills();
				alertAnimator.Play("confirm");
			}
			else
			{
				alertAnimator.Play("hide");
			}

			return;
		}

		if (!ToggleSkill())
			return;

		UpdateAugmentHierarchy(SelectedSkill);
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

		UpdateDescription();
		levelLabel.Text = Tr("skill_select_level").Replace("0", SaveManager.ActiveGameData.level.ToString("00"));
	}

	private void SwapConflictSkills()
	{
		// NOTE: It's technically possible to put the game into an "illegal" state by having multiple conflicting skills
		// Be mindful when designing skill conflicts to avoid this
		SkillResource baseSkill = SelectedSkill.Skill;
		if (IsEditingAugment)
			baseSkill = baseSkill.GetAugment(AugmentSelection);
		SkillResource conflictingSkill = ActiveSkillRing.GetConflictingSkill(baseSkill.Key);

		if (ActiveSkillRing.UnequipSkill(conflictingSkill.Key, ActiveSkillRing.GetAugmentIndex(conflictingSkill.Key)))
			ActiveSkillRing.EquipSkill(baseSkill.Key, IsEditingAugment ? AugmentSelection : 0);

		Redraw();
	}

	private bool ToggleSkill()
	{
		SkillKey key = SelectedSkill.Skill.Key;
		if (!IsEditingAugment && SelectedSkill.HasUnlockedAugments()) // Open the augment menu
		{
			ShowAugmentMenu();
			return false;
		}

		if (ActiveSkillRing.UnequipSkill(key, IsEditingAugment ? AugmentSelection : 0))
		{
			animator.Play("unequip");
			return true;
		}

		SkillEquipStatusEnum status = ActiveSkillRing.EquipSkill(key, IsEditingAugment ? AugmentSelection : 0);
		if (status == SkillEquipStatusEnum.Success)
		{
			animator.Play("equip");
			return true;
		}

		if (status == SkillEquipStatusEnum.Conflict ||
			status == SkillEquipStatusEnum.Expensive)
		{
			// Open alert menu
			IsAlertMenuActive = true;
			alertAnimator.Play("RESET");
			alertAnimator.Advance(0.0);

			if (status == SkillEquipStatusEnum.Conflict)
			{
				SkillResource baseSkill = SelectedSkill.Skill;
				if (IsEditingAugment)
					baseSkill = baseSkill.GetAugment(AugmentSelection);
				SkillResource conflictingSkill = ActiveSkillRing.GetConflictingSkill(baseSkill.Key);

				alertLabel.Text = Tr("skill_conflict");
				alertLabel.Text = alertLabel.Text.Replace("SKILL", Tr(baseSkill.NameKey));
				alertLabel.Text = alertLabel.Text.Replace("CONFLICT", Tr(conflictingSkill.NameKey));
				AlertSelection = 0; // Set to "No"
			}
			else
			{
				alertLabel.Text = Tr("skill_sp_shortage");
				AlertSelection = -1; // Disable Selection
				alertAnimator.Play("select-cancel");
				alertAnimator.Advance(0.0);
			}

			alertAnimator.Play("show");
		}

		return false; // Something failed
	}

	public void AlertMenuClosed()
	{
		IsAlertMenuActive = false;
		EnableProcessing();
	}

	private int AugmentSelection { get; set; }
	private void ShowAugmentMenu()
	{
		DisableProcessing();
		IsEditingAugment = true;
		SelectedSkill.UpdateUnlockedAugments();
		animator.Play("augment-show");
	}

	private void HideAugmentMenu()
	{
		DisableProcessing();
		IsEditingAugment = false;
		animator.Play("augment-hide");
	}

	// TODO Replace the function in the animation with EnableProcessing()!
	private void OnAugmentClosed() => EnableProcessing();

	private void ToggleAugmentMenu()
	{
		int augmentIndex = SaveManager.ActiveSkillRing.GetAugmentIndex(SelectedSkill.Skill.Key);
		if (IsEditingAugment)
		{
			cursorPosition = augmentIndex;
			AugmentSelection = augmentIndex;
			SelectedSkill.ShowAugmentMenu();
		}
		else
		{
			cursorPosition = VerticalSelection - scrollAmount;
			SelectedSkill.HideAugmentMenu();
		}

		cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
	}

	/// <summary> Updates a skill option so the correct augment appears on the skill select menu. </summary>
	private void UpdateAugmentHierarchy(SkillOption skillOption)
	{
		if (!Runtime.Instance.SkillList.GetSkill(skillOption.Skill.Key).HasAugments)
			return;

		int augmentIndex = ActiveSkillRing.GetAugmentIndex(skillOption.Skill.Key);
		skillOption.Skill = skillOption.GetAugmentSkill(augmentIndex);
		skillOption.UpdateUnlockedAugments();
		skillOption.Initialize();
	}
}