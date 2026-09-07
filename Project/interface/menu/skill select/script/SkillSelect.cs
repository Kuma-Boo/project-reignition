using System.Linq;
using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillSelect : Menu
{
	[Export] private PackedScene skillOption;
	[Export] private VBoxContainer optionContainer;
	[Export] private Node2D cursor;
	[Export] private AnimationPlayer cursorAnimator;
	[Export] private Description description;
	[Export] private Sprite2D scrollbar;
	[Export] private Sprite2D skillPointFill;
	[Export] private Label levelLabel;
	[Export] private Label skillPointLabel;
	[Export] private AnimationPlayer alertAnimator;
	[Export] private Label alertLabel;
	private int AlertSelection;
	private bool IsAlertMenuActive { get; set; }
	private SkillKey AlertMenuTargetSkill = SkillKey.Count;

	private bool IsEditingAugment { get; set; }

	private SkillOption SelectedSkill => skillOptionList[VerticalSelection];
	private bool isNothingSelected;

	private SkillListResource SkillList => Runtime.Instance.SkillList;
	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

	private int cursorPosition;
	private Vector2 cursorVelocity;
	private const float CursorSmoothing = .1f;

	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private Vector2 containerVelocity;
	private const float ScrollSmoothing = .1f;
	/// <summary> How much to scroll per skill. </summary>
	private readonly int ScrollInterval = 62;
	/// <summary> Number of skills on a single page. </summary>
	private readonly int PageSize = 8;

	/// <summary> Tracks the number of unlocked skill (excluding augments). </summary>
	private int unlockedSkillCount;
	/// <summary> Tracks the number of unlocked wind skills (excluding augments). </summary>
	private int unlockedWindSkillCount;
	private readonly Array<SkillOption> skillOptionList = [];

	[Export] private Label sortTypeLabel;
	[Export] private TextureRect sortOrderCursor;

	private bool isDescendingSort;
	private SortEnum currentSortType;
	public enum SortEnum
	{
		Default,
		Name,
		Cost,
		Wind,
		Fire,
		Dark,
		Count,
	}

	protected override void SetUp()
	{
		if (SaveManager.Config.useRetailMenuMusic) // Disable bgm
			bgm = null;

		for (int i = 0; i < (int)SkillKey.Count; i++)
		{
			SkillKey key = (SkillKey)i;
			SkillResource skill = SkillList.GetSkill(key);

			if (key == SkillKey.Character)
				continue;

			if (skill == null)
			{
				skillOptionList.Add(null);
				continue;
			}

			SkillOption newSkill = skillOption.Instantiate<SkillOption>();
			newSkill.Skill = skill;
			newSkill.Number = i + 1;
			newSkill.Initialize();
			newSkill.MouseEntered += () => ReceiveMouseInput(newSkill, false);
			newSkill.MouseExited += () => ReceiveMouseInput(null, false);

			skillOptionList.Add(newSkill);
			optionContainer.AddChild(newSkill);

			if (!newSkill.Skill.HasAugments) // Skip augments
				continue;

			for (int j = 0; j < newSkill.Skill.Augments.Count; j++)
			{
				SkillOption newAugment = skillOption.Instantiate<SkillOption>();
				newAugment.Skill = newSkill.Skill.Augments[j];
				newSkill.RegisterAugment(newAugment);
				newAugment.MouseEntered += () => ReceiveMouseInput(newAugment, true);
				newAugment.MouseExited += () => ReceiveMouseInput(null, true);
			}

			// Create base augment skill option
			SkillOption baseAugment = skillOption.Instantiate<SkillOption>();
			baseAugment.Skill = newSkill.Skill;
			newSkill.RegisterAugment(baseAugment);
			baseAugment.MouseEntered += () => ReceiveMouseInput(baseAugment, true);
			baseAugment.MouseExited += () => ReceiveMouseInput(null, true);
		}

		base.SetUp();
	}

	public override void _Process(double _)
	{
		float targetScrollPosition = 360 * scrollRatio;
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, ScrollSmoothing);

		// Update cursor position
		float targetCursorPosition = cursorPosition * ScrollInterval;
		cursor.Position = cursor.Position.SmoothDamp(Vector2.Down * targetCursorPosition, ref cursorVelocity, CursorSmoothing);

		Vector2 targetContainerPosition = new(optionContainer.Position.X, -scrollAmount * ScrollInterval);
		optionContainer.Position = optionContainer.Position.SmoothDamp(targetContainerPosition, ref containerVelocity, ScrollSmoothing);
	}

	protected override void ProcessMenu()
	{
		if (Runtime.Instance.IsActionJustPressed("sys_pause", "ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen"))
		{
			OpenPresetMenu();
			return;
		}

		if (!IsEditingAugment && !IsAlertMenuActive)
		{
			if (Runtime.Instance.MouseScrollInput != 0)
			{
				int targetIndex = VerticalSelection + Runtime.Instance.MouseScrollInput;
				targetIndex = Mathf.Clamp(targetIndex, 0, unlockedSkillCount);

				int sign = targetIndex - VerticalSelection;
				if (sign != 0)
				{
					isNothingSelected = false;
					VerticalSelection = WrapSelection(targetIndex, unlockedSkillCount);
					UpdateScrollAmount(sign);
					MoveCursor();
					UpdateDescription();
					StartSelectionTimer();
				}

				return;
			}

			// Quick scrolling
			if (Input.IsActionJustPressed("button_step_left"))
			{
				int targetSelection = Mathf.Max(VerticalSelection - PageSize, 0);
				ScrollSelection(targetSelection);
				return;
			}

			if (Input.IsActionJustPressed("button_step_right"))
			{
				int targetSelection = Mathf.Min(VerticalSelection + PageSize, unlockedSkillCount - 1);
				ScrollSelection(targetSelection);
				return;
			}
		}

		if (Runtime.Instance.IsActionJustPressed("sys_sort", "ui_focus_next") && !IsEditingAugment)
		{
			SortEnum targetSortType = currentSortType;
			if (isDescendingSort || targetSortType >= SortEnum.Wind)
			{
				targetSortType++;
				if (targetSortType >= SortEnum.Count)
					targetSortType = SortEnum.Default;
			}

			SetSortType(targetSortType, isDescendingSort = sortOrderCursor.Visible && !isDescendingSort);
		}

		base.ProcessMenu();
	}

	private void SetSortType(SortEnum sortType, bool isDecending)
	{
		currentSortType = sortType;
		isDescendingSort = isDecending;
		sortOrderCursor.FlipV = !isDescendingSort;
		sortOrderCursor.Visible = currentSortType < SortEnum.Wind;

		SortSkills();
		Redraw();
	}

	protected override void Cancel()
	{
		if (IsAlertMenuActive)
		{
			if (AlertSelection != 2)
			{
				AlertSelection = 1;
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
		cursorAnimator.Play("hide");

		// Return to level select music
		FadeBgm(.5f);
		parentMenu.PlayBgm();
	}

	private void UpdateAlertMenuVisuals()
	{
		if (AlertSelection < 0)
		{
			alertAnimator.Play("select-none");
			return;
		}

		if (AlertSelection == 2)
		{
			alertAnimator.Play("select-cancel");
			return;
		}

		alertAnimator.Play(AlertSelection == 0 ? "select-yes" : "select-no");
	}

	protected override void UpdateSelection()
	{
		if (IsAlertMenuActive)
		{
			if (Mathf.Abs(AlertSelection) != 2)
			{
				int input = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));
				if ((input > 0 && AlertSelection != 1) || (input < 0 && AlertSelection != 0))
				{
					AlertSelection = input > 0 ? 1 : 0;
					UpdateAlertMenuVisuals();
				}
			}

			return;
		}

		int inputSign = Mathf.Sign(Input.GetAxis("ui_up", "ui_down"));
		if (inputSign == 0)
			return;

		if (isNothingSelected)
		{
			cursorAnimator.Play("show");
			isNothingSelected = false;
			return;
		}

		int changeAmount = inputSign;
		if (IsEditingAugment)
		{
			changeAmount = AugmentSelection;
			AugmentSelection = WrapSelection(AugmentSelection + inputSign, SelectedSkill.AugmentMenuCount);
			changeAmount = AugmentSelection - changeAmount;
		}
		else
		{
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, unlockedSkillCount);
		}

		UpdateScrollAmount(changeAmount);
		MoveCursor();
		UpdateDescription();
	}

	private void UpdateDescription()
	{
		if (IsEditingAugment)
		{
			description.Text = SelectedSkill.GetAugmentDescription(AugmentSelection);
			return;
		}

		description.Text = SelectedSkill.Skill.DescriptionKey;
	}

	private void UpdateScrollAmount(int amount)
	{
		int listSize = unlockedSkillCount;
		if (IsEditingAugment)
			listSize += SelectedSkill.AugmentMenuCount;

		if (listSize <= PageSize)
		{
			// Disable scrolling
			scrollAmount = 0;
			scrollRatio = 0;
			cursorPosition = VerticalSelection;
		}
		else
		{
			// Update scroll
			int selection = VerticalSelection;
			if (IsEditingAugment)
				selection += AugmentSelection + 1;

			if (selection == 0 || selection == listSize - 1)
				cursorPosition = scrollAmount = selection;
			else if (amount != 0)
			{
				if ((amount < 0 && cursorPosition == 1) || (amount > 0 && cursorPosition == PageSize - 2))
					scrollAmount += Mathf.Sign(amount);
				else
					cursorPosition += Mathf.Sign(amount);

				amount -= Mathf.Sign(amount);
				UpdateScrollAmount(amount);
			}

			scrollAmount = Mathf.Clamp(scrollAmount, 0, listSize - PageSize);
			scrollRatio = (float)selection / (listSize - 1);
			cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
			UpdateNewText();
		}
	}

	private void SnapCursor()
	{
		cursorVelocity = Vector2.Zero;
		cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
		ShowCursor();
	}

	private void MoveCursor()
	{
		animator.Play("select");
		animator.Seek(0, true);
		StartSelectionTimer();
	}

	public override void ShowMenu()
	{
		if (menuMemory[MemoryKeys.PresetsOpen] == 1)
		{
			animator.Play("show-from-preset");
			menuMemory[MemoryKeys.PresetsOpen] = 0; // Reset memory

			// Check if the player loaded a preset with an unlocked skill (Backwards Compatability)
			for (int i = 0; i < ActiveSkillRing.EquippedSkills.Count; i++)
			{
				if (!ActiveSkillRing.IsSkillUnlocked(ActiveSkillRing.EquippedSkills[i], false))
				{
					// Need to reinitialize skill select menu
					menuMemory[MemoryKeys.SkillMenuInitialized] = 0;
					break;
				}
			}

			if (menuMemory[MemoryKeys.SkillMenuInitialized] != 0)
				return;
		}

		if (bgm?.Playing == false)
		{
			// Start skill select music
			parentMenu?.FadeBgm(0.5f);
			PlayBgm();
		}

		// If the skill menu has already been initialized, just show the menu
		if (menuMemory[MemoryKeys.SkillMenuInitialized] != 0 && unlockedSkillCount != 0)
		{
			base.ShowMenu();
			return;
		}

		// Reset the skill list
		unlockedSkillCount = skillOptionList.Count;
		SetSortType(SortEnum.Default, false);

		SkillOption[] oldSkillOptionList = skillOptionList.ToArray();
		for (int i = oldSkillOptionList.Length - 1; i >= 0; i--)
		{
			skillOptionList[(int)oldSkillOptionList[i].Skill.Key] = oldSkillOptionList[i];
			optionContainer.MoveChild(oldSkillOptionList[i], (int)oldSkillOptionList[i].Skill.Key);
		}

		// Update unlocked skill count to account for multiple save files
		unlockedSkillCount = 0;
		unlockedWindSkillCount = 0;
		for (int i = skillOptionList.Count - 1; i >= 0; i--)
		{
			if (skillOptionList[i] == null)
				continue;

			SkillKey key = (SkillKey)i;
			skillOptionList[i].Visible = SaveManager.ActiveSkillRing.IsSkillUnlocked(key);

			if (!skillOptionList[i].Visible)
			{
				// Locked skills go to the bottom and are never processed
				MoveSkillToBottom(i);
				continue;
			}

			// Process augments
			unlockedSkillCount++;

			if (skillOptionList[i].Skill.Element == SkillResource.SkillElement.Wind)
				unlockedWindSkillCount++;

			UpdateAugmentHierarchy(skillOptionList[i]);
			skillOptionList[i].SetNewTag(skillOptionList[i].IsNew());
		}

		SortSkills();
		Redraw();

		ScrollSelection(0);
		base.ShowMenu();

		menuMemory[MemoryKeys.SkillMenuInitialized] = 1;
	}

	private void ShowCursor()
	{
		if (isNothingSelected)
			return;

		cursorAnimator.Play("show");
		cursorAnimator.Queue("loop");
	}

	public void ShowSkills()
	{
		for (int i = 0; i < skillOptionList.Count; i++)
			skillOptionList[i].Visible = true;

		description.Visible = true;
	}

	private void OpenPresetMenu()
	{
		if (IsAlertMenuActive)
			return;

		menuMemory[MemoryKeys.PresetsOpen] = 1; // Set flag so we can play the correct animation later
		animator.Play("enter-skill-preset");
		cursorAnimator.Play("hide");
	}

	protected override void Confirm()
	{
		if (IsAlertMenuActive)
		{
			if (AlertSelection == 0)
			{
				if (AlertMenuTargetSkill != SkillKey.Count)
				{
					// Unequip a different skill before toggling this one
					ActiveSkillRing.ForceUnequipSkill(AlertMenuTargetSkill);
					ToggleSkill();
					Redraw();
				}
				else
				{
					// Toggle skills
					SwapConflictSkills();
				}

				alertAnimator.Play("confirm");
			}
			else
			{
				alertAnimator.Play("hide");
			}

			return;
		}

		if (isNothingSelected)
			return;

		if (!ToggleSkill())
			return;

		Redraw();
	}

	public override void OpenSubmenu() => _submenus[0].ShowMenu();

	public void Redraw()
	{
		skillPointLabel.Text = ActiveSkillRing.TotalCost.ToString("000") + "/" + ActiveSkillRing.MaxSkillPoints.ToString("000");
		skillPointFill.Scale = new(ActiveSkillRing.TotalCost / (float)ActiveSkillRing.MaxSkillPoints, skillPointFill.Scale.Y);

		for (int i = 0; i < unlockedSkillCount; i++)
		{
			if (skillOptionList[i].HasUnlockedAugments())
			{
				UpdateAugmentHierarchy(skillOptionList[i]);
				continue;
			}

			skillOptionList[i].Redraw();
		}

		UpdateDescription();
		levelLabel.Text = Tr("skill_select_level").Replace("0", SaveManager.ActiveGameData.level.ToString("00"));
	}

	public void UpdateNewText()
	{
		SkillOption targetSkill = IsEditingAugment ? SelectedSkill.GetUnlockedAugment(AugmentSelection) : SelectedSkill;
		if (!targetSkill.IsAugmentDropdown)
			targetSkill.SetNewTag(false);

		if (SelectedSkill.IsAugmentDropdown && IsEditingAugment)
			SelectedSkill.SetNewTag(SelectedSkill.IsNew());
	}

	private void SwapConflictSkills()
	{
		// NOTE: It's technically possible to put the game into an "illegal" state by having multiple conflicting skills
		// Be mindful when designing skill conflicts to avoid this
		SkillResource baseSkill = SelectedSkill.Skill;
		if (IsEditingAugment)
			baseSkill = SelectedSkill.GetUnlockedAugment(AugmentSelection).Skill;
		SkillResource conflictingSkill = ActiveSkillRing.GetConflictingSkill(baseSkill.Key);

		ActiveSkillRing.ForceUnequipSkill(conflictingSkill.Key, ActiveSkillRing.GetAugmentIndex(conflictingSkill.Key));

		// Revert to base skill if unequipped
		ActiveSkillRing.ResetAugmentIndex(conflictingSkill.Key);
		ActiveSkillRing.EquipSkill(baseSkill.Key, IsEditingAugment ? SelectedSkill.GetUnlockedAugment(AugmentSelection).Skill.AugmentIndex : 0);

		Redraw();
	}

	private bool ToggleSkill()
	{
		SkillKey key = SelectedSkill.Skill.Key;
		if (!IsEditingAugment && SelectedSkill.HasMultipleAugments()) // Open the augment menu
		{
			ShowAugmentMenu();
			return false;
		}

		int augmentIndex = IsEditingAugment ? SelectedSkill.GetUnlockedAugment(AugmentSelection).Skill.AugmentIndex : 0;
		if (!IsEditingAugment && SelectedSkill.HasUnlockedAugments())
			augmentIndex = SelectedSkill.GetUnlockedAugment(0).Skill.AugmentIndex;

		if (key == SkillKey.Character)
			augmentIndex++;

		if (ActiveSkillRing.IsSkillEquipped(key))
		{
			SkillKey unequippedKey = ActiveSkillRing.UnequipSkill(key, augmentIndex);
			if (unequippedKey == key)
			{
				animator.Play("unequip");
				return true;
			}
			else if (unequippedKey != SkillKey.Count)
			{
				// Conflict due to element count
				ShowAlertMenu(Runtime.Instance.SkillList.GetSkill(key), SkillEquipStatusEnum.ConflictUnequip, unequippedKey);
				return false;
			}
		}

		SkillEquipStatusEnum status = ActiveSkillRing.EquipSkill(key, augmentIndex);
		if (status == SkillEquipStatusEnum.Success)
		{
			animator.Play("equip");
			return true;
		}

		if (status == SkillEquipStatusEnum.ConflictEquip ||
			status == SkillEquipStatusEnum.Expensive ||
			status == SkillEquipStatusEnum.ElementRequirement)
		{
			SkillResource baseSkill = SelectedSkill.Skill;
			if (IsEditingAugment)
				baseSkill = baseSkill.GetAugment(AugmentSelection);

			ShowAlertMenu(baseSkill, status);
		}

		return false; // Something failed
	}

	private void ShowAlertMenu(SkillResource skill, SkillEquipStatusEnum status, SkillKey skillKey = SkillKey.Count)
	{
		// Open alert menu
		IsAlertMenuActive = true;
		AlertMenuTargetSkill = skillKey;
		alertAnimator.Play("RESET");
		alertAnimator.Advance(0.0);

		if (status == SkillEquipStatusEnum.ConflictEquip || status == SkillEquipStatusEnum.ConflictUnequip)
		{
			bool isEquippingSkill = status == SkillEquipStatusEnum.ConflictEquip;
			SkillResource conflictingSkill = isEquippingSkill ? ActiveSkillRing.GetConflictingSkill(skill.Key) : Runtime.Instance.SkillList.GetSkill(skillKey);
			alertLabel.Text = Tr(isEquippingSkill ? "skill_equip_conflict" : "skill_unequip_conflict");
			alertLabel.Text = alertLabel.Text.Replace("SKILL", Tr(skill.NameKey));
			alertLabel.Text = alertLabel.Text.Replace("CONFLICT", Tr(conflictingSkill.NameKey));

			alertAnimator.Play("select-no");
			if (Runtime.Instance.IsUsingMouse)
			{
				AlertSelection = -1;
				alertAnimator.Advance(0.0);
				alertAnimator.Play("select-none");
			}
			else
			{
				AlertSelection = 1; // Set to "No"
			}
			alertAnimator.Advance(0.0);
		}
		else
		{
			if (status == SkillEquipStatusEnum.ElementRequirement)
			{
				string translationText = string.Empty;
				int equippedAmount = ActiveSkillRing.GetSkillCountByElement(skill.Element);
				int amountNeeded = skill.ElementRequirement - equippedAmount;

				switch (skill.Element)
				{
					case SkillResource.SkillElement.Wind:
						translationText = "skill_element_requirement_wind";
						break;
					case SkillResource.SkillElement.Fire:
						translationText = "skill_element_requirement_fire";
						break;
					case SkillResource.SkillElement.Dark:
						translationText = "skill_element_requirement_dark";
						break;
				}

				if (amountNeeded > 1)
					translationText += "_multiple";

				alertLabel.Text = Tr(translationText);
				alertLabel.Text = alertLabel.Text.Replace("[AMOUNT]", amountNeeded.ToString());
			}
			else
			{
				alertLabel.Text = Tr("skill_sp_shortage");
			}

			alertAnimator.Play("select-cancel");
			if (Runtime.Instance.IsUsingMouse)
			{
				AlertSelection = -2;
				alertAnimator.Advance(0.0);
				alertAnimator.Play("select-none");
			}
			else
			{
				AlertSelection = 2;
			}
			alertAnimator.Advance(0.0);
		}

		alertAnimator.Play("show");
	}

	/// <summary> Sorts the skill list. </summary>
	private void SortSkills(SortEnum sortType = SortEnum.Count, int startIndex = 0)
	{
		// NOTE: Sorting is HIGHLY unoptimized, but I'm not going to worry about it unless we have performance issues
		// Update label
		sortTypeLabel.Text = "sys_sort_" + currentSortType.ToString().ToLower();
		SkillOption currentSkill = SelectedSkill;

		if (sortType == SortEnum.Count)
			sortType = currentSortType;

		if (isDescendingSort)
		{
			ReverseSkillList();
		}
		else
		{
			// Basic bubble sort (Done twice)
			for (int i = startIndex; i < unlockedSkillCount; i++) // Apply actual sorting
			{
				for (int j = unlockedSkillCount - 1; j > i; j--)
					CalculateExchange(i, j, sortType);
			}

			// Sort again starting from the end of the wind elements to keep skills organized by element
			// Since we're using bubble sort, the other elements are sorted for free
			if (sortType == SortEnum.Wind)
				SortSkills(SortEnum.Fire, unlockedWindSkillCount);
		}


		// Maintain selection
		int targetSelection = skillOptionList.IndexOf(currentSkill);
		ScrollSelection(targetSelection);
	}

	private void ScrollSelection(int targetSelection)
	{
		int initialSelection = VerticalSelection + AugmentSelection;
		scrollAmount += targetSelection - initialSelection;
		VerticalSelection = targetSelection;
		UpdateScrollAmount(0);

		// Reupdate cursor since clamping is applied in UpdateScrollAmount()
		cursorPosition = VerticalSelection - scrollAmount;

		if (VerticalSelection != 0 && VerticalSelection != unlockedSkillCount - 1)
		{
			// Ensure cursor doesn't get stuck on the edges of the list
			if (cursorPosition == 0) // Top of the list
			{
				cursorPosition++;
				scrollAmount--;
			}
			else if (cursorPosition == PageSize - 1)
			{
				cursorPosition--;
				scrollAmount++;
			}
		}

		if (VerticalSelection != initialSelection)
			MoveCursor();
	}

	private string GetSkillName(int index)
	{
		SkillOption skill = skillOptionList[index];
		StringName nameString = skill.HasUnlockedAugments() ?
			skill.GetAugmentSkill(ActiveSkillRing.GetAugmentIndex(skill.Skill.Key)).NameKey :
			skill.Skill.NameKey;

		return Tr(nameString);
	}

	private void CalculateExchange(int i, int j, SortEnum sortType)
	{
		bool isNumberOutOfOrder = (!isDescendingSort && skillOptionList[i].Skill.Key > skillOptionList[j].Skill.Key) ||
			(isDescendingSort && skillOptionList[i].Skill.Key < skillOptionList[j].Skill.Key);

		if (sortType == SortEnum.Default)
		{
			if (isNumberOutOfOrder)
				PerformExchange(i, j);

			return;
		}

		if (sortType == SortEnum.Name)
		{
			string skill1 = GetSkillName(i);
			string skill2 = GetSkillName(j);
			if ((!isDescendingSort && skill1.CompareTo(skill2) > 0) ||
				(isDescendingSort && skill1.CompareTo(skill2) < 0))
			{
				PerformExchange(i, j);
			}

			return;
		}

		if (sortType == SortEnum.Cost)
		{
			if ((!isDescendingSort && skillOptionList[i].Skill.Cost > skillOptionList[j].Skill.Cost) ||
				(isDescendingSort && skillOptionList[i].Skill.Cost < skillOptionList[j].Skill.Cost) ||
				(isNumberOutOfOrder && skillOptionList[i].Skill.Cost == skillOptionList[j].Skill.Cost))
			{
				PerformExchange(i, j);
			}

			return;
		}


		// TODO Make this sort the excess elements nicely too
		bool isElementDifferent = skillOptionList[i].Skill.Element != skillOptionList[j].Skill.Element;
		bool isElementCorrect = skillOptionList[j].Skill.Element == ToSkillElement(sortType);
		if ((isElementDifferent && isElementCorrect) ||
			(!isElementDifferent && isNumberOutOfOrder))
		{
			PerformExchange(i, j);
		}
	}

	/// <summary> Converts a sorted skill list to a reverse sorted skill list. </summary>
	private void ReverseSkillList()
	{
		int endIndex = unlockedSkillCount - 1;
		for (int i = 0; i <= (endIndex / 2) - 1; i++)
			PerformExchange(i, endIndex - i);
	}

	private SkillResource.SkillElement ToSkillElement(SortEnum sort)
	{
		return sort switch
		{
			SortEnum.Wind => SkillResource.SkillElement.Wind,
			SortEnum.Fire => SkillResource.SkillElement.Fire,
			SortEnum.Dark => SkillResource.SkillElement.Dark,
			_ => SkillResource.SkillElement.Count,
		};
	}

	private void PerformExchange(int i, int j)
	{
		// Swap skills
		(skillOptionList[j], skillOptionList[i]) = (skillOptionList[i], skillOptionList[j]);

		// Swap the skills in the tree
		Node temporary = optionContainer.GetChild(i);
		optionContainer.MoveChild(optionContainer.GetChild(j), i);
		optionContainer.MoveChild(temporary, j);
	}

	private void MoveSkillToBottom(int index)
	{
		// Unoptimized as we're removing a space [O(N)], but I don't think we have enough skills for this to matter
		skillOptionList.Add(skillOptionList[index]);
		skillOptionList.RemoveAt(index);
		optionContainer.MoveChild(optionContainer.GetChild(index), optionContainer.GetChildCount());
	}

	public void AlertMenuClosed()
	{
		IsAlertMenuActive = false;
		EnableProcessing();
	}

	private int AugmentSelection { get; set; }
	private void ShowAugmentMenu()
	{
		IsEditingAugment = true;
		SelectedSkill.UpdateUnlockedAugments();
		animator.Play("augment-show");
		cursorAnimator.Play("hide");

		AugmentSelection = 0;
		for (int i = 0; i < SelectedSkill.AugmentMenuCount; i++)
		{
			if (SelectedSkill.GetAugmentSkill(i) == SelectedSkill.Skill)
				break;

			if (SaveManager.ActiveSkillRing.IsSkillUnlocked(SelectedSkill.GetAugmentSkill(i)))
				AugmentSelection++;
		}

		if (SelectedSkill.Skill.Key == SkillKey.Character && AugmentSelection != 0)
			AugmentSelection--;

		// Frame augments to stay on screen
		scrollAmount += AugmentSelection + 1;
		if (cursorPosition == PageSize - 1 && AugmentSelection < SelectedSkill.AugmentMenuCount)
		{
			cursorPosition--;
			scrollAmount += 1;
		}

		UpdateScrollAmount(0);

		SelectedSkill.ShowAugmentMenu();

		if (Runtime.Instance.IsUsingMouse)
			isNothingSelected = true;
	}

	private void HideAugmentMenu()
	{
		IsEditingAugment = false;
		animator.Play("augment-hide");
		cursorAnimator.Play("hide");

		// Revert to base skill if unequipped
		if (!ActiveSkillRing.IsSkillEquipped(SelectedSkill.Skill.Key))
		{
			ActiveSkillRing.ResetAugmentIndex(SelectedSkill.Skill.Key);
			UpdateAugmentHierarchy(SelectedSkill);
		}

		cursorPosition = VerticalSelection - scrollAmount;
		SelectedSkill.HideAugmentMenu();

		UpdateScrollAmount(0);
		SortSkills();

		if (Runtime.Instance.IsUsingMouse)
			isNothingSelected = true;
	}

	/// <summary> Updates a skill option so the correct augment appears on the skill select menu. </summary>
	private void UpdateAugmentHierarchy(SkillOption skillOption)
	{
		if (!Runtime.Instance.SkillList.GetSkill(skillOption.Skill.Key).HasAugments)
			return;

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(skillOption.Skill.Key))
		{
			int augmentIndex = ActiveSkillRing.GetAugmentIndex(skillOption.Skill.Key);
			skillOption.Skill = skillOption.GetAugmentSkill(augmentIndex);
		}
		else if (skillOption.HasUnlockedAugments())
		{
			skillOption.Skill = skillOption.GetUnlockedAugment(0).Skill;
		}

		skillOption.UpdateUnlockedAugments();
		skillOption.Initialize();
	}

	private void ReceiveAlertMouseInput(int index)
	{
		if (!isProcessing || !IsAlertMenuActive)
			return;

		AlertSelection = index;
		UpdateAlertMenuVisuals();
	}

	private void ReceiveMouseInput(SkillOption skill, bool isAugment)
	{
		if (skill != null)
			GD.PrintT(skill.Name, skill.Skill.Key, skill.Skill.AugmentIndex);

		if (!isProcessing)
			return;

		if (IsEditingAugment != isAugment)
			return;

		Runtime.Instance.IsUsingMouse = true;

		if (skill == null)
		{
			isNothingSelected = true;
			cursorAnimator.Play("hide");
			return;
		}

		if (IsEditingAugment)
		{
			isNothingSelected = false;
			AugmentSelection = 0;
			for (int i = 0; i < SelectedSkill.AugmentMenuCount; i++)
			{
				if (SelectedSkill.GetAugmentSkill(i) == skill.Skill)
					break;

				if (SaveManager.ActiveSkillRing.IsSkillUnlocked(SelectedSkill.GetAugmentSkill(i)))
					AugmentSelection++;
			}

			cursorPosition = VerticalSelection - scrollAmount + AugmentSelection + 1;
			MoveCursor();
			UpdateDescription();
			return;
		}

		isNothingSelected = false;
		int targetIndex = skillOptionList.IndexOf(skill);
		int sign = targetIndex - VerticalSelection;
		VerticalSelection = targetIndex;
		UpdateScrollAmount(sign);
		MoveCursor();
		UpdateDescription();
	}
}
