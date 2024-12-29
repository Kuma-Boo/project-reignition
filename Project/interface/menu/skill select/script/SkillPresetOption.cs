using Godot;
using System;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillPresetOption : Menu
{


    public SkillPreset thisPreset;
    private SkillRing activeSkillRing => SaveManager.ActiveSkillRing;

    public int index {get; set;}// The preset option's menu number
    public bool isSubMenuActive;
    public bool enableControls;

    private bool isInvalid;
    private int subIndex; 
    

    [Export]
    private Label presetLabel;

    [Export]
    private Label numSkillsLabel;

    [Export]
    private Label skillCostLabel;

    [Export]
    private Label windAmtLabel;

    [Export]
    private Label fireAmtLabel;

    [Export]
    private Label darkAmtLabel;

    [Export]
    private Label saveLabel; //We're changing this to "overwrite" if a save already exists

    [Export]
    private AnimationPlayer animator;

    [Export]
    private AnimationPlayer animatorOptions;



    public void Initialize()
    {
        isSubMenuActive = false;
        subIndex = 0;
        if (thisPreset == null)
        {
            animator.Play("no-preset");
            saveLabel.Text = "sys_savep";
            isInvalid = true;
            return;
        }
        else
        {
            saveLabel.Text = "sys_overwrite";
            presetLabel.Text = thisPreset.presetName;

            int numSkills = 0;
            int skillCost = 0;
            int windAmt = 0;
            int fireAmt = 0;
            int darkAmt = 0;

            //update all the above totals
            for (int i = 0; i < thisPreset.skills.Count; i++)
            {
                SkillResource baseSkill = Runtime.Instance.SkillList.GetSkill(thisPreset.skills[i]);
                if (baseSkill == null)
                    continue;
                int augmentIndex = activeSkillRing.GetAugmentIndex(thisPreset.skills[i]);
                if (augmentIndex == 0)
                {
                    switch (baseSkill.Element)
                    {
                        case SkillResource.SkillElement.Wind:
                            windAmt += 1;
                        break;

                        case SkillResource.SkillElement.Fire:
                            fireAmt += 1;
                        break;

                        case SkillResource.SkillElement.Dark:
                            fireAmt += 1;
                        break;
                    }


                    skillCost += baseSkill.Cost;
                    numSkills += 1;
                    continue;
                }

                switch (baseSkill.Augments[augmentIndex - 1].Element)
                {
                    case SkillResource.SkillElement.Wind:
                        windAmt += 1;
                    break;

                    case SkillResource.SkillElement.Fire:
                        fireAmt += 1;
                    break;

                    case SkillResource.SkillElement.Dark:
                        darkAmt += 1;
                    break;
                }
                skillCost += baseSkill.Augments[augmentIndex - 1].Cost;
                numSkills += 1;
            }
            

            numSkillsLabel.Text = numSkills.ToString();
            skillCostLabel.Text = skillCost.ToString();

        }
    }

    protected override void UpdateSelection()
    {
        if (isSubMenuActive && enableControls)
        {
            if (Input.IsActionPressed("move_up"))
            {
                subIndex -= 1;
                if (subIndex < 0)
                    subIndex = 4;
                MoveCursor();
            }

            if (Input.IsActionPressed("move_down"))
            {
                subIndex += 1;
                if (subIndex > 4)
                    subIndex = 0;
                MoveCursor();
            }
        }
    }

    protected override void Confirm()
    {
        if (enableControls)
        {
            if (isSubMenuActive)
            {

                switch (subIndex)
                {
                    case 0:
                    SavePreset();
                    break;
                    case 1:
                    LoadPreset();
                    break;
                    case 2:
                    //RenamePreset();
                    break;
                    case 3:
                    DeletePreset();
                    break;
                    case 4:
                    animatorOptions.Play("hide");
                    isSubMenuActive = false;
                    break;
                }

            }
            else
            {
                animatorOptions.Play("show");
                isSubMenuActive = true;
            }
        }
        

    }

    protected override void Cancel()
    {
        if (enableControls)
        {
            if (isSubMenuActive)
            {
                animatorOptions.Play("hide");
                isSubMenuActive = false;
            }
            else
            {
                //Return to skill editing
            }
        }
    }

    private void MoveCursor()
    {
        switch (subIndex)
        {
            case 0:
            if (isInvalid)
                animatorOptions.Play("select_save");
            else
                animatorOptions.Play("select_save_invalid");
            break;
            case 1:
            if (isInvalid)
                animatorOptions.Play("select_load");
            else
                animatorOptions.Play("select_load_invalid");
            break;
            case 2:
            if (isInvalid)
                animatorOptions.Play("select_rename");
            else
                animatorOptions.Play("select_rename_invalid");
            break;
            case 3:
            if (isInvalid)
                animatorOptions.Play("select_delete");
            else
                animatorOptions.Play("select_delete_invalid");
            break;
            case 4:
            if (isInvalid)
                animatorOptions.Play("select_cancel");
            else
                animatorOptions.Play("select_cancel_invalid");
            break;
        }
    }

    private void SavePreset()
    {
        thisPreset = SaveManager.ActiveGameData.presetList[index];
        SaveManager.SaveGameData();
        Initialize();

    }

    private void LoadPreset()
    {
        SaveManager.ActiveGameData.equippedSkills = thisPreset.skills;
        SaveManager.ActiveGameData.equippedAugments = thisPreset.skillAugments;

    }

    private void RenamePreset()
    {

    }

    private void DeletePreset()
    {
        if (SaveManager.ActiveGameData.presetList[index] != null)
        {
            SaveManager.ActiveGameData.presetList[index] = null;
            SaveManager.SaveGameData();
            Initialize();
        }
        
    }

    public void EnableControls()
    {
        enableControls = true;
    }

    public void DisableControls()
    {
        enableControls = false;
    }

    public void SelectUp() //used in the parent menu
    {
        animator.Play("select-up");
    }

    public void SelectDown()
    {
        animator.Play("select-down");
    }

    public void SelectLeft()
    {
        animator.Play("select-left");
    }

    public void SelectRight()
    {
        animator.Play("select-right");
    }

    public void Deselect()
    {
        animator.Play("deselect");
    }

    private void _on_text_edit_focus_exited()
    {

    }
}
