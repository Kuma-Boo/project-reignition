using Godot;
using Godot.Collections;
using System;

namespace Project.Core
{
	public partial class SaveManager : Node
	{
		public override void _EnterTree()
		{
			LoadConfig();
			LoadGame();
		}

		#region Config
		public enum VoiceLanguage
		{
			English,
			Japanese
		}
		public enum TextLanguage
		{
			English, //Retail english script
			Retranslated, //Use the retranslation script
			Japanese,
			German,
			Italian,
			French,
			Spanish
		}
		private enum AudioBuses
		{
			MASTER,
			BGM,
			SFX,
			VOICE,
			COUNT
		}

		private readonly Vector2I[] SCREEN_RESOLUTIONS =
		{
			new Vector2I(640, 360), //360p
			new Vector2I(854, 480), //480p
			new Vector2I(1280, 720), //720p
			new Vector2I(1600, 900), //900p
			new Vector2I(1920, 1080), //1080p
			new Vector2I(2560, 1440), //1440p
			new Vector2I(3840, 2160), //4K
		};

		public static ConfigData settings;
		public static bool UseEnglishVoices => settings.voiceLanguage == VoiceLanguage.English;

		public partial class ConfigData : GodotObject
		{
			//Video
			public bool useVsync;
			public bool isFullscreen;
			public int screenResolution = 4; //Default to 1080p

			//Audio
			public bool isMasterMuted;
			public float masterVolume = .5f;
			public bool isBgmMuted;
			public float bgmVolume = 1f;
			public bool isSfxMuted;
			public float sfxVolume = 1f;
			public bool isVoiceMuted;
			public float voiceVolume = 1f;

			public string inputConfiguration;

			//Language
			public bool subtitlesEnabled = true;
			public VoiceLanguage voiceLanguage = VoiceLanguage.English;
			public TextLanguage textLanguage = TextLanguage.English;
		}

		private const string SAVE_DIRECTORY = "user://";
		private const string CONFIG_FILE = "config.cfg";

		public void LoadConfig()
		{
			FileAccess configFile = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE, FileAccess.ModeFlags.Read);

			//Attempt to load.
			if (FileAccess.GetOpenError() == Error.Ok) //Load Default settings
				settings = (ConfigData)Json.ParseString(configFile.GetAsText());
			else
			{
				settings = new ConfigData();

				if (OS.IsDebugBuild())
				{
					settings.screenResolution = 0;
					settings.bgmVolume = 0;
					settings.masterVolume = 0f;
				}
			}

			ApplyLocalization();

			//TODO Attempt to load control configuration.
			InputManager.LoadControls(settings.inputConfiguration);

			DisplayServer.WindowSetVsyncMode(settings.useVsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
			DisplayServer.WindowSetSize(SCREEN_RESOLUTIONS[settings.screenResolution]);
			DisplayServer.WindowSetMode(settings.isFullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

			SetAudioBusVolume((int)AudioBuses.MASTER, settings.masterVolume, settings.isMasterMuted);
			SetAudioBusVolume((int)AudioBuses.BGM, settings.bgmVolume, settings.isBgmMuted);
			SetAudioBusVolume((int)AudioBuses.SFX, settings.sfxVolume, settings.isSfxMuted);
			SetAudioBusVolume((int)AudioBuses.VOICE, settings.voiceVolume, settings.isVoiceMuted);
		}

		public void SaveConfig()
		{
			FileAccess configFile = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE, FileAccess.ModeFlags.Write);
			configFile.StoreString(Json.Stringify(settings));
		}

		private void ApplyLocalization()
		{
			switch (settings.textLanguage)
			{
				case TextLanguage.Retranslated:
					TranslationServer.SetLocale("en_US");
					break;
				case TextLanguage.Japanese:
					TranslationServer.SetLocale("ja");
					break;
				case TextLanguage.Spanish:
					TranslationServer.SetLocale("es");
					break;
				case TextLanguage.French:
					TranslationServer.SetLocale("fr");
					break;
				case TextLanguage.Italian:
					TranslationServer.SetLocale("it");
					break;
				case TextLanguage.German:
					TranslationServer.SetLocale("de");
					break;
				default:
					TranslationServer.SetLocale("en");
					break;
			}
		}

		public void SetAudioBusVolume(int bus, float volume, bool forceMute = default)
		{
			bool isMuted = Mathf.IsZeroApprox(volume) || forceMute;
			AudioServer.SetBusMute(bus, isMuted); //Mute or unmute

			if (isMuted) return;
			AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(volume));
		}

		#endregion

		#region Game data
		public static int ActiveSaveSlotIndex = -1;
		/// <summary> Reference to the current save being used. </summary>
		public static GameData ActiveGameData => ActiveSaveSlotIndex == -1 ? null : GameSaveSlots[ActiveSaveSlotIndex];
		public static GameData[] GameSaveSlots = new GameData[MAX_SAVE_SLOTS]; //List of all saves created.
		public const int MAX_SAVE_SLOTS = 9; //Maximum number of save slots that can be created.

		public partial class GameData : GodotObject
		{
			#region Data
			/// <summary> Which area was the player in last? (Used for save select) </summary>
			public WorldEnum lastPlayedWorld;
			public WorldFlagEnum worldRingsCollected;
			public WorldFlagEnum worldsUnlocked;

			/// <summary> Player level, from 1 -> 99 </summary>
			public int level;
			/// <summary> How much exp the player currently has. </summary>
			public int exp;
			/// <summary> Total playtime, in seconds. </summary>
			public float playTime;
			/// <summary> Flag enum of all skills enabled. </summary>
			public SkillEnum skillRing;
			#endregion

			/// <summary> Is this a new file? </summary>
			public bool IsNewFile => level == 0; //Since the player must at least be level one, a level zero file can be assumed to be empty.

			public bool IsWorldUnlocked(int worldIndex) => worldsUnlocked.IsSet(ConvertIntToWorldEnum(worldIndex));
			public bool IsWorldRingObtained(int worldIndex) => worldRingsCollected.IsSet(ConvertIntToWorldEnum(worldIndex));
			/// <summary> Soul gauge's level, normalized from [0 -> 1] </summary>
			public float SoulGaugeLevel => Mathf.Clamp(level, 0, 50) / 50f;
			/// <summary>
			/// Converts worldIndex to WorldEnum. World index starts at zero.
			/// </summary>
			private WorldFlagEnum ConvertIntToWorldEnum(int worldIndex)
			{
				int returnIndex = 1;
				for (int i = 0; i < worldIndex; i++)
					returnIndex *= 2;

				return (WorldFlagEnum)returnIndex;
			}

			/// <summary>
			/// Creates a new GameData object that contains default values.
			/// </summary>
			public static GameData DefaultData()
			{
				return new GameData()
				{
					level = 1,
					worldsUnlocked = WorldFlagEnum.All,
					lastPlayedWorld = WorldEnum.LostPrologue,
				};
			}

			/// <summary>
			/// Creates a dictionary based on GameData.
			/// </summary>
			public Dictionary SaveDictionary()
			{
				Dictionary dictionary = new Dictionary();

				//WorldEnum data
				dictionary.Add(nameof(lastPlayedWorld), (int)lastPlayedWorld);
				dictionary.Add(nameof(worldRingsCollected), (int)worldRingsCollected);
				dictionary.Add(nameof(worldsUnlocked), (int)worldsUnlocked);

				//Player stats
				dictionary.Add(nameof(level), level);
				dictionary.Add(nameof(exp), exp);
				dictionary.Add(nameof(playTime), Mathf.RoundToInt(playTime));

				dictionary.Add(nameof(skillRing), (int)skillRing);

				return dictionary;
			}

			/// <summary>
			/// Sets GameData based on dictionary.
			/// </summary>
			public void LoadFromDictionary(Dictionary dictionary)
			{
				//WorldEnum data
				if (dictionary.TryGetValue(nameof(lastPlayedWorld), out Variant var))
					lastPlayedWorld = (WorldEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldRingsCollected), out var))
					worldRingsCollected = (WorldFlagEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldsUnlocked), out var))
					worldsUnlocked = (WorldFlagEnum)(int)var;

				if (dictionary.TryGetValue(nameof(level), out var))
					level = (int)var;
				if (dictionary.TryGetValue(nameof(exp), out var))
					exp = (int)var;
				if (dictionary.TryGetValue(nameof(playTime), out var))
					playTime = (float)var;

				if (dictionary.TryGetValue(nameof(skillRing), out var))
					skillRing = (SkillEnum)(int)var;
			}
		}

		public const int MAX_PLAY_TIME = 359999; //99:59:59, in seconds.
		[Flags]
		public enum WorldFlagEnum
		{
			LostPrologue = 1,
			SandOasis = 2,
			DinosaurJungle = 4,
			EvilFoundry = 8,
			LevitatedRuin = 16,
			PirateStorm = 32,
			SkeletonDome = 64,
			NightPalace = 128,
			All = LostPrologue + SandOasis + DinosaurJungle + EvilFoundry + LevitatedRuin + PirateStorm + SkeletonDome + NightPalace
		}
		public enum WorldEnum
		{
			LostPrologue,
			SandOasis,
			DinosaurJungle,
			EvilFoundry,
			LevitatedRuin,
			PirateStorm,
			SkeletonDome,
			NightPalace,
			Max
		}

		[Flags]
		public enum SkillEnum
		{
			//Standard skills
			None = 0,
			LandingBoost = 1, //Gives a speed boost when landing
			PearlAttractor = 2, //Makes collecting pearls easier
			SplashJump = 4, //Bounces the player when JumpDashing an obstacle
			ManualDrift = 8, //Manually perform a drift for more speed and points/exp
		}

		public enum LevelStateEnum
		{
			Locked, //Level is locked
			New, //Player has never tried the level
			Played, //Player has at least attempted the level
			Completed, //Player has completed the level at least once
		}

		public static void SaveGame()
		{
			if (ActiveSaveSlotIndex == -1) return; //Invalid save slot

			//TODO Write save data to a file.
			string saveNumber = ActiveSaveSlotIndex.ToString("00");
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + $"save{saveNumber}.dat", FileAccess.ModeFlags.Write);

			if (FileAccess.GetOpenError() == Error.Ok)
			{
				file.StoreString(Json.Stringify(ActiveGameData.SaveDictionary(), "\t"));
				file.Flush();
			}
			else
			{
				//TODO Show an error message to the player? 
			}
		}

		public static void LoadGame()
		{
			//TODO actually try and load from save files.
			for (int i = 0; i < GameSaveSlots.Length; i++)
			{
				GameSaveSlots[i] = new GameData();

				string saveNumber = i.ToString("00");
				FileAccess file = FileAccess.Open(SAVE_DIRECTORY + $"save{saveNumber}.dat", FileAccess.ModeFlags.Read);
				if (FileAccess.GetOpenError() == Error.Ok)
				{
					GameSaveSlots[i].LoadFromDictionary((Dictionary)Json.ParseString(file.GetAsText()));
					file.Flush();
				}
			}

			//Debug game data
			if (CheatManager.UseDebugSave) //For testing
			{
				ActiveSaveSlotIndex = 0;
				GameSaveSlots[ActiveSaveSlotIndex] = new GameData()
				{
					worldsUnlocked = WorldFlagEnum.All
				};
			}
		}

		/// <summary>
		/// Deletes the current save data, then creates a new one in it's place.
		/// </summary>
		public static void ResetSaveData(int index)
		{
			GameSaveSlots[index].Free();
			GameSaveSlots[index] = GameData.DefaultData();
		}
		#endregion
	}
}