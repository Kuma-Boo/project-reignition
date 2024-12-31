using Godot;
using Godot.Collections;
using System;
using Project.Core;
using Project.Gameplay;

public partial class SkillPreset : Resource
{
    public string presetName {get; set;}
    public Array<SkillKey> skills {get; set;}
    public Dictionary<SkillKey, int> skillAugments {get; set;}

    
}
