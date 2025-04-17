using Godot;
using System;
using Project.Core;


[GlobalClass]
public partial class BookPage : Resource
{
    public enum World
    { LP, SO, DJ, EF, LR, PS, SD, NP }

    [Export]
    public string name;

    ///<summary>Has the player unlocked this page?</summary>
    [Export]
    public bool unlocked;

    ///<summary>Unlolck via clearing a stage</summary>
    [Export]
    public bool unlock_clear;

    ///<summary>Unlock via getting gold in a stage</summary>
    [Export]
    public bool unlock_gold;

    ///<summary>Unlock by getting a certain number of Silver Medals</summary>
    [Export]
    public bool unlock_silver;

    ///<summary>Unlock by getting all Medals of a type in a stage</summary>
    [Export]
    public bool unlock_allstage;

    ///<summary>Which world for the All Stages condition, Clear condition, and Gold Medal condition</summary>
    [Export]
    public World unlock_world;

    ///<summary>Stage string used for determining unlocks</summary>
    [Export]
    public string unlock_stage;

    ///<summary>Stage number used for determining unlocks</summary>
    [Export]
    public int unlock_stageNum;

    ///<summary>Amount of Silver Medals needed to unlock this stage</summary>
    [Export]
    public int unlock_numSilver;

    ///<summary>The image used to preview this page in the main view</summary>
    [Export]
    public Texture page_preview;

    ///<summary>The image used to preview this page in the description view</summary>
    [Export]
    public Texture page_preview_big;

    ///<summary>The image used in the full-image view</summary>
    [Export]
    public Texture page_full;

    ///<summary>The music track to be played</summary>
    [Export]
    public AudioStream track;

    ///<summary>The video file to be played</summary>
    [Export(PropertyHint.File)]
    public string videoFilePath;


    [Export]
    public Rect2 image_preview; //the region preview for page in the list


    public BookPage() : this("", false, false, false, null, 0, 0, null, null, null, new Rect2()) { }

    public BookPage(
                    string name, bool unlock_clear, bool unlock_gold, bool unlock_silver,
                    string unlock_stage, int unlock_stageNum, int unlock_numSilver,
                    Texture page_preview, Texture page_preview_big, Texture page_full,
                    Rect2 image_preview)
    {
        this.name = name;
        this.unlock_clear = unlock_clear;
        this.unlock_gold = unlock_gold;
        this.unlock_silver = unlock_silver;
        this.unlock_stage = unlock_stage;
        this.unlock_stageNum = unlock_stageNum;
        this.unlock_numSilver = unlock_numSilver;
        this.page_preview = page_preview;
        this.page_preview_big = page_preview_big;
        this.page_full = page_full;
        this.image_preview = image_preview;
    }

}
