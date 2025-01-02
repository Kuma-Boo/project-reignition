using Godot;
using System;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillPresetSelect : Menu
{   
    enum direction {UP, DOWN}

    
    private int numPresets;


    [Export]
    private SkillSelect skillSelectMenu;
    [Export]
    private PackedScene presetOption;
    [Export]
    private VBoxContainer presetContainer;
    [Export]
    private Sprite2D scrollbar;

    [Export]
    private Label saveLabel; //We're changing this to "overwrite" if a save already exists

    [Export]
    private AnimationPlayer animator;
    [Export]
    private AnimationPlayer animatorOptions;

    private int scrollAmount;
    private float scrollRatio;
    private Vector2 scrollVelocity;
    private Vector2 containerVelocity;
    private const float scrollSmoothing = .1f;

    private readonly int scrollInterval = 63;
    private readonly int pageSize = 5;

    private Array<SkillPresetOption> presetList = [];
    
    //private Array<SkillPreset> currentPresets = [];
    private Array<string> currentPresetNames = [];
    private Array<Array<SkillKey>> currentPresetSkills = [];
    private Array<Dictionary<SkillKey, int>> currentPresetAugments = [];


    private bool firstLoad;

    private int selectedIndex;

    public bool isSubMenuActive;
    public bool enableControls;
    private int subIndex;

    
    protected override void SetUp()
    {
        firstLoad = true;
        selectedIndex = 0;
        subIndex = 0;
        numPresets = 20;

    }

    public override void ShowMenu()
    {
        GD.Print("Showing presets");
        animator.Play("show");
        LoadPresets();
        
        base.ShowMenu();
    }
    public void LoadPresets()
    {
        GD.Print("Loading Presets");
        
        
        if (firstLoad == true)
        {
            presetList = new Array<SkillPresetOption>();
            //currentPresets = new Array<SkillPreset>();
            currentPresetNames = new Array<string>();
            currentPresetSkills = new Array<Array<SkillKey>>();
            currentPresetAugments = new Array<Dictionary<SkillKey, int>>();
            for (int i = 0; i < 20; i++)
            {
                //presetList.Add(null);
                currentPresetNames.Add("");
                currentPresetSkills.Add(null);
                currentPresetAugments.Add(null);
                //currentPresets.Add(new SkillPreset("", null, null));
                SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();
                
            
                if (SaveManager.ActiveGameData.presetSkills[i] != null &&
                    SaveManager.ActiveGameData.presetSkillAugments[i] != null &&
                    SaveManager.ActiveGameData.presetNames[i] != "")
                {
                    GD.Print("LOADING PRESET " + i);
                    
                    currentPresetNames[i] = SaveManager.ActiveGameData.presetNames[i];
                    currentPresetSkills[i] = SaveManager.ActiveGameData.presetSkills[i];
                    currentPresetAugments[i] = SaveManager.ActiveGameData.presetSkillAugments[i];
                    
                    newPreset.presetName = currentPresetNames[i];
                    newPreset.skills = currentPresetSkills[i];
                    newPreset.skillAugments = currentPresetAugments[i];

                    //newPreset.thisPreset = currentPresets[i];
                    //GD.Print("PRESET " + i + ":");
                    //GD.Print(newPreset.thisPreset.presetName.ToString());
                    //GD.Print(newPreset.thisPreset.skills.ToString());
                    //GD.Print(newPreset.thisPreset.skillAugments.ToString());
                    
                }
                else
                {   
                    currentPresetNames[i] = "";
                    currentPresetSkills[i] = null;
                    currentPresetAugments[i] = null;
                    
                }
                    
                    
                    
                presetList.Add(newPreset);
                
                presetContainer.AddChild(newPreset);
                presetList[i].index = i + 1;//for displaying the number
                
                presetList[i].Initialize();
            }
        }
        firstLoad = false;
        presetList[0].SelectRight();
        enableControls = true;

    }

    protected override void UpdateSelection()
    {
        if (isSubMenuActive && enableControls)
        {
            if (Input.IsActionJustPressed("move_up"))
            {
                subIndex -= 1;
                if (subIndex < 0)
                    subIndex = 4;
                MoveSubCursor();
            }

            if (Input.IsActionJustPressed("move_down"))
            {
                subIndex += 1;
                if (subIndex > 4)
                    subIndex = 0;
                MoveSubCursor();
            }
        }
        if (isSubMenuActive == false && enableControls)
        {
            for (int i = 0; i < 20; i++)
            {
                if (i != selectedIndex)
                presetList[i].DeselectInstant();
            }
            
            if (Input.IsActionJustPressed("move_up"))
            {
                selectedIndex -= 1;
                if (selectedIndex < 0)
                    selectedIndex = presetList.Count - 1;
                MoveCursor(direction.UP, selectedIndex);
            }

            if (Input.IsActionJustPressed("move_down"))
            {
                selectedIndex += 1;
                if (selectedIndex > presetList.Count - 1)
                    selectedIndex = 0;
                MoveCursor(direction.DOWN, selectedIndex);
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
                    SaveSkills(selectedIndex);
                    break;
                    case 1:
                    if (IsInvalid(selectedIndex) == false)
                    LoadSkills(selectedIndex);
                    break;
                    case 2:
                    //RenamePreset();
                    break;
                    case 3:
                    if (IsInvalid(selectedIndex) == false)
                    DeletePreset(selectedIndex);
                    break;
                    case 4:
                    animatorOptions.Play("hide");
                    isSubMenuActive = false;
                    break;
                }

            }
            else
            {
                subIndex = 0;
                MoveSubCursor();
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
                animator.Play("hide");
                SaveManager.SaveGameData();
                OpenParentMenu();
                //Return to skill editing
            }
        }
    }

    



    public void MoveSubCursor()
    {
        switch (subIndex)
        {
            case 0:
            if (IsInvalid(selectedIndex) == false)
                animatorOptions.Play("select-save");
            else
                animatorOptions.Play("select-save-invalid");
            break;
            case 1:
            if (IsInvalid(selectedIndex) == false)
                animatorOptions.Play("select-load");
            else
                animatorOptions.Play("select-load-invalid");
            break;
            case 2:
            if (IsInvalid(selectedIndex) == false)
                animatorOptions.Play("select-rename");
            else
                animatorOptions.Play("select-rename-invalid");
            break;
            case 3:
            if (IsInvalid(selectedIndex) == false)
                animatorOptions.Play("select-delete");
            else
                animatorOptions.Play("select-delete-invalid");
            break;
            case 4:
            if (IsInvalid(selectedIndex) == false)
                animatorOptions.Play("select-cancel");
            else
                animatorOptions.Play("select-cancel-invalid");
            break;
        }
    }

    private void MoveCursor(direction dir, int index)
    {
        GD.Print("Index: " + index);

        if (dir == direction.UP)
            presetList[index].SelectUp();
        else if (dir == direction.DOWN)
            presetList[index].SelectDown();


    }

    private void SaveSkills(int preset)
    {
        if (currentPresetNames[preset] == "" &&
            currentPresetSkills[preset] == null &&
            currentPresetAugments[preset] == null)
            {
                currentPresetNames[preset] = "New Preset";
                currentPresetSkills[preset] = new Array<SkillKey>();
                currentPresetAugments[preset] = new Dictionary<SkillKey, int>();
            }
        
        
        //Storing our equipped skills into the temporary preset
        currentPresetSkills[preset] = SaveManager.ActiveGameData.equippedSkills;
        currentPresetAugments[preset] = SaveManager.ActiveGameData.equippedAugments;

        
        //Set a new name if our current one is empty
        if (currentPresetNames[preset] == null || currentPresetNames[preset] == "")
            currentPresetNames[preset] = "New Preset";

        //Sets the preset selection object to the saved temporary preset
        presetList[preset].presetName = currentPresetNames[preset];
        presetList[preset].skills = currentPresetSkills[preset];
        presetList[preset].skillAugments = currentPresetAugments[preset];

        //Turns the class back into separate data
        //SaveManager.ActiveGameData.FromSkillPreset(currentPresets[preset], preset); 
        SaveManager.ActiveGameData.presetNames[preset] = currentPresetNames[preset];
        SaveManager.ActiveGameData.presetSkills[preset] = currentPresetSkills[preset];
        SaveManager.ActiveGameData.presetSkillAugments[preset] = currentPresetAugments[preset];

        //Save our new data to the file and play the animation to initialize the on-screen data
        presetList[preset].SavePreset();
        SaveManager.SaveGameData();
        

    }

    private void LoadSkills(int preset)
    {
        

        SaveManager.ActiveGameData.equippedSkills = currentPresetSkills[preset];
        SaveManager.ActiveGameData.equippedAugments = currentPresetAugments[preset];

        skillSelectMenu.Redraw();
        presetList[preset].SelectPreset();


    }

    private void RenamePreset()
    {

    }

    private void DeletePreset(int preset)
    {
        if (SaveManager.ActiveGameData.presetNames[preset] != "" &&
            SaveManager.ActiveGameData.presetSkills[preset] != null &&
            SaveManager.ActiveGameData.presetSkillAugments[preset] != null)
        {
            currentPresetNames[preset] = "";
            currentPresetSkills[preset] = null;
            currentPresetAugments[preset] = null;

            presetList[preset].presetName = currentPresetNames[preset];
            presetList[preset].skills = currentPresetSkills[preset];
            presetList[preset].skillAugments = currentPresetAugments[preset];
            //currentPresets[preset].SetName("");
            //currentPresets[preset].SetSkills(null);
            //currentPresets[preset].SetSkills(null);
            //presetList[preset].thisPreset.SetPreset(currentPresets[preset]);

            //SaveManager.ActiveGameData.FromSkillPreset(currentPresets[preset],preset);


            SaveManager.SaveGameData();
            presetList[preset].Initialize();
        }
        
    }

    private bool IsInvalid(int index)
    {
        if (currentPresetNames[index] == "" &&
            currentPresetSkills[index] == null &&
            currentPresetAugments[index] == null)
            return true;
        else
            return false;
             
    }
    

}
