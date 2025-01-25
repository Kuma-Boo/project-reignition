using Godot;
using Godot.Collections;
using System.Collections.Generic;
using Project.Core;
using Project.Gameplay;
using System;

namespace Project.Interface.Menus;



public partial class SkillSelect : Menu
{

    public enum SortType
    {
        Default,
        Name,
        Cost,
        Wind,
        Fire,
        Dark,
    }

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
    private Vector2 cursorVelocity;
    private const float CursorSmoothing = .1f;

    private int scrollAmount;
    private float scrollRatio;
    private Vector2 scrollVelocity;
    private Vector2 containerVelocity;
    private const float ScrollSmoothing = .1f;
    /// <summary> How much to scroll per skill. </summary>
    private readonly int ScrollInterval = 63;
    /// <summary> Number of skills on a single page. </summary>
    private readonly int PageSize = 8;


    private Array<SkillOption> skillOptionList = [];
    private Array<SkillOption> currentSkillOptionList = [];

    private System.Collections.Generic.List<SkillResource> sortedSkillOptionList;


    [Export]
    private Label sortTypeLabel;
    [Export]
    private Sprite2D sortOrderCursor;
    private SortType currentSortType = SortType.Default;

    protected override void SetUp()
    {
        GD.Print("Current Locale:" + TranslationServer.GetLocale().ToString());
        sortedSkillOptionList = new System.Collections.Generic.List<SkillResource>();
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
            sortedSkillOptionList.Add(skillOptionList[i].Skill);

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

        // Update cursor position
        float targetCursorPosition = cursorPosition * ScrollInterval;
        cursor.Position = cursor.Position.SmoothDamp(Vector2.Down * targetCursorPosition, ref cursorVelocity, CursorSmoothing);

        Vector2 targetContainerPosition = new(optionContainer.Position.X, -scrollAmount * ScrollInterval);
        optionContainer.Position = optionContainer.Position.SmoothDamp(targetContainerPosition, ref containerVelocity, ScrollSmoothing);
    }

    protected override void ProcessMenu()
    {
        if (Input.IsActionJustPressed("button_pause"))
        {
            OpenPresetMenu();
            return;
        }

        if (Input.IsActionJustPressed("button_speedbreak") && !IsEditingAugment)
        {
            currentSortType += 1;
            if ((int)currentSortType > 5)
                currentSortType = 0;
            SortSkills(currentSortType);
            sortTypeLabel.Text = "sys_sort_" + currentSortType.ToString().ToLower();
        }

        base.ProcessMenu();
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
            {
                AugmentSelection = WrapSelection(AugmentSelection + inputSign, SelectedSkill.AugmentMenuCount);
                cursorPosition = VerticalSelection - scrollAmount + AugmentSelection + 1;
            }

            MoveCursor();
            UpdateDescription();
            return;
        }

        if (inputSign != 0)
        {
            VerticalSelection = WrapSelection(VerticalSelection + inputSign, currentSkillOptionList.Count);
            UpdateScrollAmount(inputSign);
            MoveCursor();
            UpdateDescription();
        }

        // TODO Change sort method when speedbreak is pressed
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

    private void UpdateScrollAmount(int inputSign)
    {
        int listSize = currentSkillOptionList.Count;
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
            if (VerticalSelection == 0 || VerticalSelection == listSize - 1)
                cursorPosition = scrollAmount = VerticalSelection;
            else if ((inputSign < 0 && cursorPosition == 1) || (inputSign > 0 && cursorPosition == 6))
                scrollAmount += inputSign;
            else
                cursorPosition += inputSign;

            scrollAmount = Mathf.Clamp(scrollAmount, 0, listSize - PageSize);
            scrollRatio = (float)VerticalSelection / (listSize - 1);
            cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
        }
    }

    private void SnapCursor()
    {
        cursorVelocity = Vector2.Zero;
        cursor.Position = Vector2.Up * -cursorPosition * ScrollInterval;
    }

    private void MoveCursor()
    {
        animator.Play("select");
        animator.Seek(0, true);
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

        if (menuMemory[MemoryKeys.PresetsOpen] == 1)
            animator.Play("show-from-preset");
        else
            base.ShowMenu();

        menuMemory[MemoryKeys.PresetsOpen] = 0; // Reset memory
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

    public override void OpenSubmenu() => _submenus[0].ShowMenu();

    public void Redraw()
    {
        skillPointLabel.Text = ActiveSkillRing.TotalCost.ToString("000") + "/" + ActiveSkillRing.MaxSkillPoints.ToString("000");
        skillPointFill.Scale = new(ActiveSkillRing.TotalCost / (float)ActiveSkillRing.MaxSkillPoints, skillPointFill.Scale.Y);
        foreach (SkillOption skillOption in currentSkillOptionList)
        {
            if (skillOption.HasUnlockedAugments())
            {
                UpdateAugmentHierarchy(skillOption);
                continue;
            }

            skillOption.Redraw();
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
        {
            // Revert to base skill if unequipped
            ActiveSkillRing.ResetAugmentIndex(conflictingSkill.Key);
            ActiveSkillRing.EquipSkill(baseSkill.Key, IsEditingAugment ? AugmentSelection : 0);
        }

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

    private void SortSkills(SortType sortType)
    {
        int i, j;
        int n = skillOptionList.Count;

        for (j = n - 1; j > 0; j--)
        {
            for (i = 0; i < j; i++)
            {
                switch (sortType)
                {
                    case SortType.Default:
                        if (skillOptionList[i].Skill.Key > skillOptionList[i + 1].Skill.Key)
                            PerformExchange(i);
                        break;
                    case SortType.Name:
                        if (String.Compare(Tr("skill_" + skillOptionList[i].Skill.NameString), Tr("skill_" + skillOptionList[i + 1].Skill.NameString), true, new System.Globalization.CultureInfo(TranslationServer.GetLocale().ToString())) > 0)
                            PerformExchange(i);
                        break;
                    case SortType.Cost:
                        if (skillOptionList[i].Skill.Cost > skillOptionList[i + 1].Skill.Cost)
                            PerformExchange(i);
                        break;
                    case SortType.Wind:
                        if (skillOptionList[i].Skill.Element != skillOptionList[i + 1].Skill.Element && skillOptionList[i + 1].Skill.Element == SkillResource.SkillElement.Wind)
                            PerformExchange(i);
                        break;
                    case SortType.Fire:
                        if (skillOptionList[i].Skill.Element != skillOptionList[i + 1].Skill.Element && skillOptionList[i + 1].Skill.Element == SkillResource.SkillElement.Fire)
                            PerformExchange(i);
                        break;
                    case SortType.Dark:
                        if (skillOptionList[i].Skill.Element != skillOptionList[i + 1].Skill.Element && skillOptionList[i + 1].Skill.Element == SkillResource.SkillElement.Dark)
                            PerformExchange(i);
                        break;
                }
            }
        }


        Redraw();

    }


    private void PerformExchange(int i)
    {
        ExchangeSkill(skillOptionList, i, i + 1);
        ExchangeSkill(currentSkillOptionList, i, i + 1);
        ExchangeOption(optionContainer, i, i + 1);
    }
    private static void ExchangeSkill(Array<SkillOption> skill, int m, int n)
    {
        SkillOption temporary;

        temporary = skill[m];
        skill[m] = skill[n];
        skill[n] = temporary;
    }

    private static void ExchangeOption(Node option, int m, int n)
    {
        Node temporary;

        temporary = option.GetChild(m);
        option.MoveChild(option.GetChild(n), m);
        option.MoveChild(temporary, n);
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

        // Frame augments to stay on screen
        if (VerticalSelection + SelectedSkill.AugmentMenuCount - scrollAmount >= PageSize - 1)
        {
            scrollAmount = VerticalSelection + SelectedSkill.AugmentMenuCount - (PageSize - 2);
            UpdateScrollAmount(0);
        }

        AugmentSelection = SaveManager.ActiveSkillRing.GetAugmentIndex(SelectedSkill.Skill.Key);
        cursorPosition = VerticalSelection - scrollAmount + AugmentSelection + 1;
        SelectedSkill.ShowAugmentMenu();
    }

    private void HideAugmentMenu()
    {
        IsEditingAugment = false;
        animator.Play("augment-hide");

        // Revert to base skill if unequipped
        if (!ActiveSkillRing.IsSkillEquipped(SelectedSkill.Skill.Key))
        {
            ActiveSkillRing.ResetAugmentIndex(SelectedSkill.Skill.Key);
            UpdateAugmentHierarchy(SelectedSkill);
        }

        cursorPosition = VerticalSelection - scrollAmount;
        SelectedSkill.HideAugmentMenu();

        UpdateScrollAmount(0);
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