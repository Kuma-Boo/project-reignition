using Godot;
using System;
using Project.Core;


[GlobalClass]
public partial class BookPage : Resource
{
    [Export]
    public bool unlocked; //has the player unlocked this page?
    [Export]
    public bool unlock_clear; //unlock via clearing a stage
    [Export]
    public bool unlock_gold; //unlock via getting gold in a stage
    [Export]
    public bool unlock_silver; //unlock by getting a certain number of silver medals

    [Export]
    public string[] unlock_stage; //stage string used for determining unlocks
    [Export]
    public int unlock_stageNum; //stage number used for determining unlocks
    [Export]
    public int unlock_numSilver; //amount of silver medals needed to unlock the stage

    [Export]
    public Texture page_preview; //the image used to preview the page in the list
    [Export]
    public Texture page_preview_big; //the image used to preivew the page in the description view
    [Export]
    public Texture page_full; //the full image used for the page
    [Export]
    public AudioStream track; //the music track to be played

    [Export(PropertyHint.File)]
    public string videoFilePath; //the video file to be played


    [Export]
    public Rect2 image_preview; //the region preview for page in the list


    public BookPage() : this(false, false, false, null, 0, 0, null, null, null, new Rect2()) { }

    public BookPage(
                    bool unlock_clear, bool unlock_gold, bool unlock_silver,
                    string[] unlock_stage, int unlock_stageNum, int unlock_numSilver,
                    Texture page_preview, Texture page_preview_big, Texture page_full,
                    Rect2 image_preview)
    {
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
