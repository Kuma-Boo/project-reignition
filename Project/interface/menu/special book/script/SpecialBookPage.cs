using Godot;
using Project.Core;

[GlobalClass]
public partial class SpecialBookPage : Resource
{
	[Export] public string name;

	/// <summary> Has the player unlocked this page?. </summary>
	public bool Unlocked { get; set; }

	/// <summary> Unlock via clearing a stage. </summary>
	[Export] public bool unlockClear;

	/// <summary> Unlock via getting gold in a stage. </summary>
	[Export] public bool unlockGold;

	/// <summary> Unlock by getting a certain number of Silver Medals. </summary>
	[Export] public bool unlockSilver;

	/// <summary> Unlock by getting all Medals of a type in a stage. </summary>
	[Export] public bool unlockAllStage;

	/// <summary> Which world for the All Stages condition, Clear condition, and Gold Medal condition. </summary>
	[Export] public SaveManager.WorldEnum unlockWorld;

	/// <summary> Stage number used for determining unlocks. </summary>
	[Export] public int unlockStageNumber;

	/// <summary> Amount of Silver Medals needed to unlock this stage. </summary>
	[Export] public int unlockSilverMedalRequirement;

	/// <summary> The image used to preview this page in the description view. </summary>
	[Export] public Texture2D previewImage;

	/// <summary> The image used in the full-image view. </summary>
	[Export] public Texture2D fullImage;

	/// <summary> The music track to be played. </summary>
	[Export] public AudioStream track;

	/// <summary> The video file to be played. </summary>
	[Export(PropertyHint.File)]
	public string videoFilePath;

	/// <summary> The region preview for page in the list. </summary>
	[Export] public Rect2 previewImageRegion;

	public int NumStages(SaveManager.WorldEnum world)
	{
		int num = 0;
		for (int i = 1; i < 30; i++)
		{
			if (string.IsNullOrEmpty(StageUnlock(world, i)))
				return num;

			num++;
		}

		return num;
	}

	public string StageUnlock(SaveManager.WorldEnum world, int stageNum)
	{
		switch (world)
		{
			case SaveManager.WorldEnum.SandOasis:
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
						return string.Empty;
				}
			case SaveManager.WorldEnum.DinosaurJungle:
				switch (unlockStageNumber)
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
						return string.Empty;
				}
			case SaveManager.WorldEnum.EvilFoundry:
				switch (unlockStageNumber)
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
						return string.Empty;

				}
			case SaveManager.WorldEnum.LevitatedRuin:
				switch (unlockStageNumber)
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
						return string.Empty;
				}

			case SaveManager.WorldEnum.PirateStorm:
				switch (unlockStageNumber)
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
						return string.Empty;
				}
			case SaveManager.WorldEnum.SkeletonDome:
				switch (unlockStageNumber)
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
						return string.Empty;
				}
			case SaveManager.WorldEnum.NightPalace:
				switch (unlockStageNumber)
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
						return string.Empty;
				}
		}

		return string.Empty;
	}

	public string StageUnlock() => StageUnlock(unlockWorld, unlockStageNumber);
}
