using Godot;
using System;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillPresetSelect : Menu
{

    [Export]
    private int numPresets;

    [Export]
    private PackedScene presetOption;
    [Export]
    private VBoxContainer presetContainer;
    [Export]
    private Sprite2D scrollbar;
    [Export]
    private AnimationPlayer animator;


    private int scrollAmount;
    private float scrollRatio;
    private Vector2 scrollVelocity;
    private Vector2 containerVelocity;
    private const float scrollSmoothing = .1f;

    private readonly int scrollInterval = 63;
    private readonly int pageSize = 5;

    private Array<SkillPresetOption> presetList = [];
    private Array<SkillPreset> currentPresets = [];

    private bool firstLoad;

    
    protected override void SetUp()
    {
        firstLoad = false;
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
        presetList = new Array<SkillPresetOption>();
        currentPresets = new Array<SkillPreset>();
        
        if (firstLoad == false)
        {
            for (int i = 0; i < numPresets; i++)
            {
                SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();
                newPreset.Initialize();
            
                //if (SaveManager.ActiveGameData.presetList[i] == null)
                    //continue;
                   
            
                
                presetList.Add(newPreset);
                presetContainer.AddChild(newPreset);

            }
        }
        firstLoad = true;
        

    }

    

}
