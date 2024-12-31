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

    private bool isInvalid;
    

    [Export]
    private Label presetLabel;

    [Export]
    private Label numLabel;

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
    private AnimationPlayer animator;

    [Export]
    private AnimationPlayer animatorData;

    



    public void Initialize()
    {

        
        numLabel.Text = index.ToString();
        if (thisPreset == null)
        {
            animator.Play("no-preset");
            //saveLabel.Text = "sys_savep";
            isInvalid = true;
            return;
        }
        else
        {
            //saveLabel.Text = "sys_overwrite";
            
            if (thisPreset.presetName == "");
                thisPreset.presetName = "New Preset";
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
            
            windAmtLabel.Text = windAmt.ToString();
            fireAmtLabel.Text = fireAmt.ToString();
            darkAmtLabel.Text = darkAmt.ToString();
            numSkillsLabel.Text = numSkills.ToString();
            skillCostLabel.Text = skillCost.ToString();
            //animator.Play("new-preset");
            animatorData.Play("show");
        }
    }

    public void SelectUp()
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

    public void SavePreset()
    {
        animator.Play("save-preset");
    }

    public void SelectPreset()
    {
        animator.Play("load-preset");
    }

    public void Deselect()
    {
        animator.Play("deselect");
    }

    public void DeselectInstant()
    {
        animator.Play("deselect_instant");
    }

    private void _on_text_edit_focus_exited()
    {

    }
}
