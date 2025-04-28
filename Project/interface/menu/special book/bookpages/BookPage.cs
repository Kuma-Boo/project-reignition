using Godot;
using System;
using Project.Core;


[GlobalClass]
public partial class BookPage : Resource
{
    public enum World
    { LP = 0, SO = 1, DJ = 2, EF = 3, LR = 4, PS = 5, SD = 6, NP = 7 }

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


    public BookPage() : this("", false, false, false, 0, 0, null, null, null, new Rect2()) { }

    public BookPage(
                    string name, bool unlock_clear, bool unlock_gold, bool unlock_silver,
                    int unlock_stageNum, int unlock_numSilver,
                    Texture page_preview, Texture page_preview_big, Texture page_full,
                    Rect2 image_preview)
    {
        this.name = name;
        this.unlock_clear = unlock_clear;
        this.unlock_gold = unlock_gold;
        this.unlock_silver = unlock_silver;
        this.unlock_stageNum = unlock_stageNum;
        this.unlock_numSilver = unlock_numSilver;
        this.page_preview = page_preview;
        this.page_preview_big = page_preview_big;
        this.page_full = page_full;
        this.image_preview = image_preview;
    }


    public string StageUnlock(World world, int stageNum)
    {
        switch (world)
        {
            case World.SO:
                switch (stageNum)
                {
                    case 1:
                        return "so_a1_main";
                    case 2:
                        return "so_a1_deathless";
                    case 3:
                        return "so_a1_race";
                    case 4:
                        return "so_a1_pearless";
                    case 5:
                        return "so_a2_jarless";
                    case 6:
                        return "so_a2_deathless";
                    case 7:
                        return "so_a2_timed";
                    case 8:
                        return "so_a2_perfect";
                    case 9:
                        return "so_a3_jar";
                    case 10:
                        return "so_a3_ring";
                    case 11:
                        return "so_a3_rampage";
                    case 12:
                        return "so_a3_chain";
                    case 13:
                        return "so_boss";
                    default:
                        return "";
                }
            case World.DJ:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "dj_a1_main";
                    case 2:
                        return "dj_a1_deathless";
                    case 3:
                        return "dj_a1_ring";
                    case 4:
                        return "dj_a1_perfect";
                    case 5:
                        return "dj_a2_rampage";
                    case 6:
                        return "dj_a2_stealth";
                    case 7:
                        return "dj_a2_race";
                    case 8:
                        return "dj_a2_chain";
                    case 9:
                        return "dj_a3_majin_egg";
                    case 10:
                        return "dj_a3_dino_egg";
                    case 11:
                        return "dj_a3_ring";
                    case 12:
                        return "dj_a3_pearless";
                    default:
                        return "";
                }
            case World.EF:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "ef_a1_main";
                    case 2:
                        return "ef_a1_deathless";
                    case 3:
                        return "ef_a1_ringless";
                    case 4:
                        return "ef_a1_race";
                    case 5:
                        return "ef_a2_time";
                    case 6:
                        return "ef_a2_stealth";
                    case 7:
                        return "ef_a2_ringless";
                    case 8:
                        return "ef_a2_perfect";
                    case 9:
                        return "ef_a3_rampage";
                    case 10:
                        return "ef_a3_ring";
                    case 11:
                        return "ef_a3_perfect";
                    case 12:
                        return "ef_a3_chain";
                    case 13:
                        return "ef_boss";
                    default:
                        return "";

                }
            case World.LR:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "lr_a1_main";
                    case 2:
                        return "lr_a1_rampage";
                    case 3:
                        return "lr_a1_race";
                    case 4:
                        return "lr_a1_perfect";
                    case 5:
                        return "lr_a2_cage";
                    case 6:
                        return "lr_a2_deathless";
                    case 7:
                        return "lr_a2_ringless";
                    case 8:
                        return "lr_a2_perfect";
                    case 9:
                        return "lr_a3_rampage";
                    case 10:
                        return "lr_a3_time";
                    case 11:
                        return "lr_a3_ring";
                    case 12:
                        return "lr_a3_pearless";
                    default:
                        return "";
                }

            case World.PS:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "ps_a1_main";
                    case 2:
                        return "ps_a1_race";
                    case 3:
                        return "ps_a1_ring";
                    case 4:
                        return "ps_a1_pearless";
                    case 5:
                        return "ps_a2_rampage";
                    case 6:
                        return "ps_a2_ringless";
                    case 7:
                        return "ps_a2_time";
                    case 8:
                        return "ps_a2_chain";
                    case 9:
                        return "ps_a3_deathless";
                    case 10:
                        return "ps_a3_stealth";
                    case 11:
                        return "ps_a3_ring";
                    case 12:
                        return "ps_a3_perfect";
                    case 13:
                        return "ps_boss";
                    default:
                        return "";
                }
            case World.SD:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "sd_a1_main";
                    case 2:
                        return "sd_a1_race";
                    case 3:
                        return "sd_a1_ringless";
                    case 4:
                        return "sd_a1_pearless";
                    case 5:
                        return "sd_a2_rampage";
                    case 6:
                        return "sd_a2_deathless";
                    case 7:
                        return "sd_a2_time";
                    case 8:
                        return "sd_a2_pearless";
                    case 9:
                        return "sd_a3_bones";
                    case 10:
                        return "sd_a3_rampage";
                    case 11:
                        return "sd_a3_ring";
                    case 12:
                        return "sd_a3_chain";
                    default:
                        return "";
                }
            case World.NP:
                switch (unlock_stageNum)
                {
                    case 1:
                        return "np_a1_main";
                    case 2:
                        return "np_a1_race";
                    case 3:
                        return "np_a1_ringless";
                    case 4:
                        return "np_a1_pearless";
                    case 5:
                        return "np_a2_rampage";
                    case 6:
                        return "np_a2_stealth";
                    case 7:
                        return "np_a2_ring";
                    case 8:
                        return "np_a2_chain";
                    case 9:
                        return "np_a3_deathless";
                    case 10:
                        return "np_a3_ringless";
                    case 11:
                        return "np_a3_time";
                    case 12:
                        return "np_a3_perfect";
                    case 13:
                        return "np_boss";
                    case 14:
                        return "np_last";
                    default:
                        return "";
                }
        }

        return "";
    }
    public string StageUnlock()
    {
        return StageUnlock(unlock_world, unlock_stageNum);
    }

}
