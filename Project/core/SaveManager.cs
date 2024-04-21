using Godot;
using Godot.Collections;
using Project.Gameplay;
using System;

namespace Project.Core
{
	public partial class SaveManager : Node
	{
		public static SaveManager Instance;
		private const string SAVE_DIRECTORY = "user://";

		public override void _EnterTree()
		{
			Instance = this;
			LoadConfig();
			PreloadGameData();

			if (!OS.IsDebugBuild())
			{
				Config = DebugSettings; // Editor build
				ApplyConfig();
			}
		}


		#region Config
		public static ConfigData Config = new();
		public static bool UseEnglishVoices => Config.voiceLanguage == VoiceLanguage.English;
		private const string CONFIG_FILE_NAME = "config.cfg";

		/// <summary> Default debug settings for testing from the editor. </summary>
		private ConfigData DebugSettings => new()
		{
			screenResolution = 3,
			isMasterMuted = AudioServer.IsBusMute((int)AudioBuses.MASTER),
			isBgmMuted = AudioServer.IsBusMute((int)AudioBuses.BGM),
			isSfxMuted = AudioServer.IsBusMute((int)AudioBuses.SFX),
			isVoiceMuted = AudioServer.IsBusMute((int)AudioBuses.VOICE),

			masterVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.MASTER)) * 100),
			bgmVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.BGM)) * 100),
			sfxVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.SFX)) * 100),
			voiceVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.VOICE)) * 100),
		};

		public enum ControllerType
		{
			PlayStation, // Use PlayStation button prompts
			Xbox, // Use XBox button prompts
			Count
		}
		public enum VoiceLanguage
		{
			English,
			Japanese,
			Count
		}
		public enum TextLanguage
		{
			English, //English script (Uses Windii's retranslation when voiceover is set to Japanese)
			Japanese,
			German,
			Italian,
			French,
			Spanish,
			Count
		}
		private enum AudioBuses
		{
			MASTER,
			BGM,
			SFX,
			VOICE,
			COUNT
		}
		public static readonly Vector2I[] SCREEN_RESOLUTIONS =
		{
			new(640, 360), // 360p
			new(854, 480), // 480p
			new(1280, 720), // 720p
			new(1600, 900), // 900p
			new(1920, 1080), // 1080p
			new(2560, 1440), // 1440p
			new(3840, 2160), // 4K
		};

		public partial class ConfigData : GodotObject
		{
			// Video
			public bool useVsync;
			public bool isFullscreen;
			public int screenResolution = 4; // Defaults to 1080p

			// Audio
			public bool isMasterMuted;
			public int masterVolume = 50;
			public bool isBgmMuted;
			public int bgmVolume = 100;
			public bool isSfxMuted;
			public int sfxVolume = 100;
			public bool isVoiceMuted;
			public int voiceVolume = 100;

			// Controls
			public ControllerType controllerType = ControllerType.PlayStation;
			public Dictionary inputConfiguration = new();

			// Language
			public bool subtitlesEnabled = true;
			public VoiceLanguage voiceLanguage = VoiceLanguage.English;
			public TextLanguage textLanguage = TextLanguage.English;

			/// <summary> Creates a dictionary based on config data. </summary>
			public Dictionary ToDictionary()
			{
				Dictionary dictionary = new()
				{
					// Video
					{ nameof(useVsync), useVsync },
					{ nameof(isFullscreen), isFullscreen },
					{ nameof(screenResolution), screenResolution },

					// Audio
					{ nameof(isMasterMuted), isMasterMuted },
					{ nameof(masterVolume), masterVolume },
					{ nameof(isBgmMuted), isBgmMuted },
					{ nameof(bgmVolume), bgmVolume },
					{ nameof(isSfxMuted), isSfxMuted },
					{ nameof(sfxVolume), sfxVolume },
					{ nameof(isVoiceMuted), isVoiceMuted },
					{ nameof(voiceVolume), voiceVolume },

					{ nameof(controllerType), (int)controllerType },
					{ nameof(inputConfiguration), Json.Stringify(inputConfiguration) },

					// Langauge
					{ nameof(subtitlesEnabled), subtitlesEnabled },
					{ nameof(voiceLanguage), (int)voiceLanguage },
					{ nameof(textLanguage), (int)textLanguage },
				};

				return dictionary;
			}

			/// <summary> Sets config data based on dictionary. </summary>
			public void FromDictionary(Dictionary dictionary)
			{
				// Video
				if (dictionary.TryGetValue(nameof(useVsync), out Variant var))
					useVsync = (bool)var;
				if (dictionary.TryGetValue(nameof(isFullscreen), out var))
					isFullscreen = (bool)var;
				if (dictionary.TryGetValue(nameof(screenResolution), out var))
					screenResolution = (int)var;

				if (dictionary.TryGetValue(nameof(isMasterMuted), out var))
					isMasterMuted = (bool)var;
				if (dictionary.TryGetValue(nameof(masterVolume), out var))
					masterVolume = (int)var;
				if (dictionary.TryGetValue(nameof(isBgmMuted), out var))
					isBgmMuted = (bool)var;
				if (dictionary.TryGetValue(nameof(bgmVolume), out var))
					bgmVolume = (int)var;
				if (dictionary.TryGetValue(nameof(isSfxMuted), out var))
					isSfxMuted = (bool)var;
				if (dictionary.TryGetValue(nameof(sfxVolume), out var))
					sfxVolume = (int)var;
				if (dictionary.TryGetValue(nameof(isVoiceMuted), out var))
					isVoiceMuted = (bool)var;
				if (dictionary.TryGetValue(nameof(voiceVolume), out var))
					voiceVolume = (int)var;


				if (dictionary.TryGetValue(nameof(controllerType), out var))
					controllerType = (ControllerType)(int)var;
				if (dictionary.TryGetValue(nameof(inputConfiguration), out var))
					inputConfiguration = (Dictionary)Json.ParseString((string)var);


				if (dictionary.TryGetValue(nameof(subtitlesEnabled), out var))
					subtitlesEnabled = (bool)var;
				if (dictionary.TryGetValue(nameof(voiceLanguage), out var))
					voiceLanguage = (VoiceLanguage)(int)var;
				if (dictionary.TryGetValue(nameof(textLanguage), out var))
					textLanguage = (TextLanguage)(int)var;
			}
		}


		/// <summary> Attempts to load config data from file. </summary>
		public void LoadConfig()
		{
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE_NAME, FileAccess.ModeFlags.Read);

			if (file.GetError() == Error.Ok)
			{
				// Attempt to load.
				try
				{
					Dictionary d = (Dictionary)Json.ParseString(file.GetAsText());
					GD.Print(d);

					Config.FromDictionary(d);
					file.Close();
				}
				catch // Load Default settings
				{
					Config = new();
				}
			}

			ApplyConfig();
		}


		/// <summary> Attempts to save config data to file. </summary>
		public void SaveConfig()
		{
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE_NAME, FileAccess.ModeFlags.Write);
			file.StoreString(Json.Stringify(Config.ToDictionary(), "\t"));
			file.Close();
		}


		/// <summary> Applies active configuration data. </summary>
		public void ApplyConfig()
		{
			ApplyInputMap();
			ApplyLocalization();

			DisplayServer.WindowSetVsyncMode(Config.useVsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
			DisplayServer.WindowSetMode(Config.isFullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetSize(SCREEN_RESOLUTIONS[Config.screenResolution]);

			SetAudioBusVolume((int)AudioBuses.MASTER, Config.masterVolume, Config.isMasterMuted);
			SetAudioBusVolume((int)AudioBuses.BGM, Config.bgmVolume, Config.isBgmMuted);
			SetAudioBusVolume((int)AudioBuses.SFX, Config.sfxVolume, Config.isSfxMuted);
			SetAudioBusVolume((int)AudioBuses.VOICE, Config.voiceVolume, Config.isVoiceMuted);
		}


		/// <summary> Applies input map configuration. </summary>
		public void ApplyInputMap()
		{
			// No custom input map was created
			if (Config.inputConfiguration == null) return;

			Array<StringName> actions = InputMap.GetActions();

			for (int i = 0; i < actions.Count; i++)
			{
				if (!Config.inputConfiguration.ContainsKey(actions[i]))
					continue;

				// Mappings are ordered in a [key, axis, button] format.
				string[] mappings = ((string)Config.inputConfiguration[actions[i]]).Split(',');
				Key key = (Key)mappings[0].ToInt();
				JoyAxis axis = (JoyAxis)mappings[1].ToInt();
				JoyButton button = (JoyButton)mappings[2].ToInt();

				InputMap.ActionEraseEvents(actions[i]);

				if (key != Key.None)
					InputMap.ActionAddEvent(actions[i], new InputEventKey()
					{
						Keycode = key
					});

				if (axis != JoyAxis.Invalid)
					InputMap.ActionAddEvent(actions[i], new InputEventJoypadMotion()
					{
						Axis = axis
					});

				if (button != JoyButton.Invalid)
					InputMap.ActionAddEvent(actions[i], new InputEventJoypadButton()
					{
						ButtonIndex = button
					});
			}
		}


		/// <summary> Applies text localization. Be sure voiceover language is set first. </summary>
		private void ApplyLocalization()
		{
			switch (Config.textLanguage)
			{
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
					TranslationServer.SetLocale(UseEnglishVoices ? "en" : "en_US");
					break;
			}
		}


		/// <summary> Changes the volume of an audio bus channel. </summary>
		public void SetAudioBusVolume(int bus, int volumePercentage, bool isMuted = default)
		{
			if (volumePercentage == 0)
				isMuted = true;

			AudioServer.SetBusMute(bus, isMuted); // Mute or unmute
			AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(volumePercentage * .01f));
		}
		#endregion

		#region Game data

		/// <summary> Longest amount of playtime that can be displayed on the file select. (99:59:59 in seconds) </summary>
		public const int MAX_PLAY_TIME = 359999;

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


		public static int ActiveSaveSlotIndex = -1;
		/// <summary> Reference to the current save being used. </summary>
		public static GameData ActiveGameData => ActiveSaveSlotIndex == -1 ? null : GameSaveSlots[ActiveSaveSlotIndex];
		public static GameData[] GameSaveSlots = new GameData[MAX_SAVE_SLOTS]; //List of all saves created.
		public const int MAX_SAVE_SLOTS = 9; //Maximum number of save slots that can be created.

		public partial class GameData : GodotObject
		{
			/// <summary> Which area was the player in last? (Used for save select) </summary>
			public WorldEnum lastPlayedWorld;
			/// <summary> Flag representation of world rings collected. </summary>
			public WorldFlagEnum worldRingsCollected;
			/// <summary> Flag representation of worlds unlocked. </summary>
			public WorldFlagEnum worldsUnlocked;

			/// <summary> Individual level data. </summary>
			public Dictionary<int, Dictionary> leveldata = new();

			/// <summary> Player level, from 1 -> 99 </summary>
			public int level;
			/// <summary> How much exp the player currently has. </summary>
			public int exp;
			/// <summary> Total playtime, in seconds. </summary>
			public float playTime;
			/// <summary> Current skill ring. </summary>
			public SkillRing skillRing = new();

			/// <summary> The player's level must be at least one, so a file with level zero is treated as empty. </summary>
			public bool IsNewFile() => level == 0;
			/// <summary> Calculates the soul gauge's level ratio, normalized from [0 -> 1] </summary>
			public float CalculateSoulGaugeLevelRatio(int levelCap = 50) => Mathf.Clamp(level, 0, levelCap) / (float)levelCap;

			/// <summary> Checks if a world is unlocked. </summary>
			public bool IsWorldUnlocked(int worldIndex) => worldsUnlocked.HasFlag(ConvertIntToWorldEnum(worldIndex));
			/// <summary> Checks if a world ring was obtained. </summary>
			public bool IsWorldRingObtained(int worldIndex) => worldRingsCollected.HasFlag(ConvertIntToWorldEnum(worldIndex));

			/// <summary> Converts (WorldEnum)worldIndex to WorldFlagEnum. World index starts at zero. </summary>
			private WorldFlagEnum ConvertIntToWorldEnum(int worldIndex)
			{
				int returnIndex = 1;
				for (int i = 0; i < worldIndex; i++)
					returnIndex *= 2;

				return (WorldFlagEnum)returnIndex;
			}


			/// <summary> Creates a dictionary based on GameData. </summary>
			public Dictionary ToDictionary()
			{
				Dictionary dictionary = new()
				{
					// WorldEnum data
					{ nameof(lastPlayedWorld), (int)lastPlayedWorld },
					{ nameof(worldRingsCollected), (int)worldRingsCollected },
					{ nameof(worldsUnlocked), (int)worldsUnlocked },
					{ nameof(leveldata), (Dictionary)leveldata },


					// Player stats
					{ nameof(level), level },
					{ nameof(exp), exp },
					{ nameof(playTime), Mathf.RoundToInt(playTime) },
					{ nameof(skillRing), skillRing.equippedSkills },
				};

				return dictionary;
			}

			/// <summary> Sets GameData based on dictionary. </summary>
			public void FromDictionary(Dictionary dictionary)
			{
				//WorldEnum data
				if (dictionary.TryGetValue(nameof(lastPlayedWorld), out Variant var))
					lastPlayedWorld = (WorldEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldRingsCollected), out var))
					worldRingsCollected = (WorldFlagEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldsUnlocked), out var))
					worldsUnlocked = (WorldFlagEnum)(int)var;

				if (dictionary.TryGetValue(nameof(leveldata), out var))
					leveldata = (Dictionary<int, Dictionary>)var;


				if (dictionary.TryGetValue(nameof(level), out var))
					level = (int)var;
				if (dictionary.TryGetValue(nameof(exp), out var))
					exp = (int)var;
				if (dictionary.TryGetValue(nameof(playTime), out var))
					playTime = (float)var;

				if (dictionary.TryGetValue(nameof(skillRing), out var))
				{
					skillRing.equippedSkills = (Array<SkillKeyEnum>)var;
					skillRing.RefreshSkillRingData(level);
				}
			}


			/// <summary> Creates a new GameData object that contains default values. </summary>
			public static GameData DefaultData()
			{
				GameData data = new()
				{
					level = 1,
					worldsUnlocked = WorldFlagEnum.LostPrologue,
					lastPlayedWorld = WorldEnum.LostPrologue,
				};
				data.skillRing.RefreshSkillRingData(data.level);
				return data;
			}
		}


		public struct LevelSaveData
		{
			/// <summary> Player's best time. </summary>
			public float time;
			/// <summary> Player's best score. </summary>
			public float score;
			/// <summary> Player's best rank. </summary>
			public int rank;

			/// <summary> Fire soul collection status. </summary>
			public int[] firesoul;
		}


		/// <summary> Saves active game data to a file. </summary>
		public void SaveGameToFile()
		{
			if (ActiveSaveSlotIndex == -1) return; //Invalid save slot

			//TODO Write save data to a file.
			string saveNumber = ActiveSaveSlotIndex.ToString("00");
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + $"save{saveNumber}.dat", FileAccess.ModeFlags.Write);

			if (FileAccess.GetOpenError() == Error.Ok)
			{
				file.StoreString(Json.Stringify(ActiveGameData.ToDictionary(), "\t"));
				file.Close();
			}
			else
			{
				//TODO Show an error message to the player? 
			}
		}


		/// <summary> Preloads game data so it can be displayed on menus. </summary>
		public void PreloadGameData()
		{
			for (int i = 0; i < GameSaveSlots.Length; i++)
			{
				GameSaveSlots[i] = new();

				string saveNumber = i.ToString("00");
				FileAccess file = FileAccess.Open(SAVE_DIRECTORY + $"save{saveNumber}.dat", FileAccess.ModeFlags.Read);
				if (FileAccess.GetOpenError() == Error.Ok)
				{
					GameSaveSlots[i].FromDictionary((Dictionary)Json.ParseString(file.GetAsText()));
					file.Close();
				}
			}

			// Debug game data
			if (DebugManager.Instance.UseDebugSave) // For testing
			{
				ActiveSaveSlotIndex = 0;
				GameSaveSlots[ActiveSaveSlotIndex] = GameData.DefaultData();
				ActiveGameData.worldsUnlocked = WorldFlagEnum.All;
			}
		}

		/// <summary> Frees game data at the given index, then creates default data in it's place. </summary>
		public static void ResetSaveData(int index)
		{
			GameSaveSlots[index].Free();
			GameSaveSlots[index] = GameData.DefaultData();
		}
		#endregion
	}
}