using Godot;
using Godot.Collections;
using Project.Gameplay;
using System;
using System.Security.Cryptography;

namespace Project.Core
{
	public partial class SaveManager : Node
	{
		private const string SAVE_DIRECTORY = "user://";

		public override void _EnterTree()
		{
			LoadConfig();
			LoadGameData();

			if (OS.IsDebugBuild()) // Editor build, use custom configuration
			{
				// Default debug settings for testing from the editor.
				Config.windowSize = 3;
				Config.isMasterMuted = AudioServer.IsBusMute((int)AudioBuses.MASTER);
				Config.isBgmMuted = AudioServer.IsBusMute((int)AudioBuses.BGM);
				Config.isSfxMuted = AudioServer.IsBusMute((int)AudioBuses.SFX);
				Config.isVoiceMuted = AudioServer.IsBusMute((int)AudioBuses.VOICE);

				Config.masterVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.MASTER)) * 100);
				Config.bgmVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.BGM)) * 100);
				Config.sfxVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.SFX)) * 100);
				Config.voiceVolume = Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)AudioBuses.VOICE)) * 100);
				ApplyConfig();
			}
		}


		#region Config
		public static ConfigData Config = new();
		public static bool UseEnglishVoices => Config.voiceLanguage == VoiceLanguage.English;
		private const string CONFIG_FILE_NAME = "config.cfg";

		#region Enums
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
			English, // English script (Uses Windii's retranslation when voiceover is set to Japanese)
			Japanese,
			German,
			Italian,
			French,
			Spanish,
			Count
		}
		public enum QualitySetting
		{
			DISABLED,
			LOW,
			MEDIUM,
			HIGH,
			COUNT
		}
		private enum AudioBuses
		{
			MASTER,
			BGM,
			SFX,
			VOICE,
			COUNT
		}
		public static readonly Vector2I[] WINDOW_SIZES =
		{
			new(640, 360), // 360p
			new(854, 480), // 480p
			new(1280, 720), // 720p
			new(1600, 900), // 900p
			new(1920, 1080), // 1080p
			new(2560, 1440), // 1440p
			new(3840, 2160), // 4K
		};
		#endregion

		public partial class ConfigData : GodotObject
		{
			// Video
			public int targetDisplay = DisplayServer.GetPrimaryScreen();
			public int windowSize = 4; // Defaults to 1080p
			public bool useFullscreen = true;
			public bool useExclusiveFullscreen;
			public bool useVsync;
			public int renderScale = 100;
			public RenderingServer.ViewportScaling3DMode resizeMode = RenderingServer.ViewportScaling3DMode.Bilinear;
			public int antiAliasing = 1; // Default to FXAA
			public bool useHDBloom = true;
			public QualitySetting softShadowQuality = QualitySetting.MEDIUM;
			public QualitySetting postProcessingQuality = QualitySetting.MEDIUM;
			public QualitySetting reflectionQuality = QualitySetting.HIGH;

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
			public float deadZone = .5f;
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
					{ nameof(targetDisplay), targetDisplay },
					{ nameof(windowSize), windowSize },
					{ nameof(useFullscreen), useFullscreen },
					{ nameof(useExclusiveFullscreen), useExclusiveFullscreen },
					{ nameof(useVsync), useVsync },

					{ nameof(renderScale), renderScale },
					{ nameof(resizeMode), (int)resizeMode },
					{ nameof(antiAliasing), antiAliasing },
					{ nameof(useHDBloom), useHDBloom },
					{ nameof(softShadowQuality), (int)softShadowQuality },
					{ nameof(postProcessingQuality), (int)postProcessingQuality },
					{ nameof(reflectionQuality), (int)reflectionQuality },


					// Audio
					{ nameof(isMasterMuted), isMasterMuted },
					{ nameof(masterVolume), masterVolume },
					{ nameof(isBgmMuted), isBgmMuted },
					{ nameof(bgmVolume), bgmVolume },
					{ nameof(isSfxMuted), isSfxMuted },
					{ nameof(sfxVolume), sfxVolume },
					{ nameof(isVoiceMuted), isVoiceMuted },
					{ nameof(voiceVolume), voiceVolume },

					{ nameof(deadZone), deadZone },
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
				if (dictionary.TryGetValue(nameof(targetDisplay), out Variant var))
					targetDisplay = (int)var;
				if (dictionary.TryGetValue(nameof(useFullscreen), out var))
					useFullscreen = (bool)var;
				if (dictionary.TryGetValue(nameof(useExclusiveFullscreen), out var))
					useExclusiveFullscreen = (bool)var;
				if (dictionary.TryGetValue(nameof(windowSize), out var))
					windowSize = (int)var;
				if (dictionary.TryGetValue(nameof(useVsync), out var))
					useVsync = (bool)var;

				if (dictionary.TryGetValue(nameof(renderScale), out var))
					renderScale = (int)var;
				if (dictionary.TryGetValue(nameof(resizeMode), out var))
					resizeMode = (RenderingServer.ViewportScaling3DMode)(int)var;
				if (dictionary.TryGetValue(nameof(antiAliasing), out var))
					antiAliasing = (int)var;
				if (dictionary.TryGetValue(nameof(useHDBloom), out var))
					useHDBloom = (bool)var;
				if (dictionary.TryGetValue(nameof(softShadowQuality), out var))
					softShadowQuality = (QualitySetting)(int)var;
				if (dictionary.TryGetValue(nameof(postProcessingQuality), out var))
					postProcessingQuality = (QualitySetting)(int)var;
				if (dictionary.TryGetValue(nameof(reflectionQuality), out var))
					reflectionQuality = (QualitySetting)(int)var;

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


				if (dictionary.TryGetValue(nameof(deadZone), out var))
					deadZone = (float)var;
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
		public static void LoadConfig()
		{
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE_NAME, FileAccess.ModeFlags.Read);

			try
			{
				if (file.GetError() == Error.Ok)
				{
					// Attempt to load.
					Dictionary d = (Dictionary)Json.ParseString(file.GetAsText());
					Config.FromDictionary(d);
					file.Close();
				}
			}
			catch // Load Default settings
			{
				Config = new();
			}

			ApplyConfig();
		}


		/// <summary> Attempts to save config data to file. </summary>
		public static void SaveConfig()
		{
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + CONFIG_FILE_NAME, FileAccess.ModeFlags.Write);
			file.StoreString(Json.Stringify(Config.ToDictionary(), "\t"));
			file.Close();
		}


		/// <summary> Applies active configuration data. </summary>
		public static void ApplyConfig()
		{
			ApplyInputMap();
			ApplyLocalization();

			// Display settings
			DisplayServer.WindowMode targetMode = DisplayServer.WindowMode.Windowed;
			if (Config.useFullscreen)
				targetMode = Config.useExclusiveFullscreen ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Fullscreen;
			if (DisplayServer.WindowGetMode() != targetMode)
				DisplayServer.WindowSetMode(targetMode);
			if (!Config.useFullscreen)
				DisplayServer.WindowSetSize(WINDOW_SIZES[Config.windowSize]);

			DisplayServer.VSyncMode targetVSyncMode = Config.useVsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
			if (DisplayServer.WindowGetVsyncMode() != targetVSyncMode)
				DisplayServer.WindowSetVsyncMode(targetVSyncMode);

			Config.targetDisplay = Mathf.Clamp(Config.targetDisplay, 0, DisplayServer.GetScreenCount());
			if (Config.targetDisplay != DisplayServer.WindowGetCurrentScreen())
				DisplayServer.WindowSetCurrentScreen(Config.targetDisplay);

			// Quality settings
			Rid viewportRid = Runtime.Instance.GetViewport().GetViewportRid();

			// Update rendering mode/scale
			RenderingServer.ViewportSetScaling3DScale(viewportRid, Config.renderScale * .01f);
			RenderingServer.ViewportSetScaling3DMode(viewportRid, Config.resizeMode);

			// Update anti-aliasing
			RenderingServer.ViewportScreenSpaceAA targetSSAA = RenderingServer.ViewportScreenSpaceAA.Disabled;
			RenderingServer.ViewportMsaa targetMSAA = RenderingServer.ViewportMsaa.Disabled;
			if (Config.antiAliasing == 1) // Use FXAA
				targetSSAA = RenderingServer.ViewportScreenSpaceAA.Fxaa;
			else if (Config.antiAliasing == 2) // Use MSAA
				targetMSAA = RenderingServer.ViewportMsaa.Msaa2X;
			else if (Config.antiAliasing == 3)
				targetMSAA = RenderingServer.ViewportMsaa.Msaa4X;
			else if (Config.antiAliasing == 4)
				targetMSAA = RenderingServer.ViewportMsaa.Msaa8X;

			RenderingServer.ViewportSetScreenSpaceAA(viewportRid, targetSSAA);
			RenderingServer.ViewportSetMsaa3D(viewportRid, targetMSAA);

			RenderingServer.EnvironmentGlowSetUseBicubicUpscale(Config.useHDBloom);

			int targetShadowAtlasSize = 2048;
			bool use16BitShadowAtlas = Config.softShadowQuality == QualitySetting.HIGH;
			RenderingServer.ShadowQuality targetSoftShadowQuality = RenderingServer.ShadowQuality.Hard;
			switch (Config.softShadowQuality)
			{
				case QualitySetting.LOW:
					targetSoftShadowQuality = RenderingServer.ShadowQuality.SoftLow;
					break;
				case QualitySetting.MEDIUM:
					targetShadowAtlasSize = 4096;
					targetSoftShadowQuality = RenderingServer.ShadowQuality.SoftMedium;
					break;
				case QualitySetting.HIGH:
					targetShadowAtlasSize = 8192;
					targetSoftShadowQuality = RenderingServer.ShadowQuality.SoftHigh;
					break;
			}

			RenderingServer.DirectionalShadowAtlasSetSize(targetShadowAtlasSize, use16BitShadowAtlas);
			RenderingServer.ViewportSetPositionalShadowAtlasSize(viewportRid, targetShadowAtlasSize, use16BitShadowAtlas);
			RenderingServer.DirectionalSoftShadowFilterSetQuality(targetSoftShadowQuality);
			RenderingServer.PositionalSoftShadowFilterSetQuality(targetSoftShadowQuality);

			switch (Config.postProcessingQuality)
			{
				case QualitySetting.LOW:
					RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Low, true, .5f, 2, 50, 300);
					RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.Low, true, .5f, 2, 50, 300);
					break;
				case QualitySetting.MEDIUM:
					RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Medium, true, .5f, 2, 50, 300);
					RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.Medium, true, .5f, 2, 50, 300);
					break;
				case QualitySetting.HIGH:
					RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High, false, .5f, 2, 50, 300);
					RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.High, false, .5f, 2, 50, 300);
					break;
			}


			SetAudioBusVolume((int)AudioBuses.MASTER, Config.masterVolume, Config.isMasterMuted);
			SetAudioBusVolume((int)AudioBuses.BGM, Config.bgmVolume, Config.isBgmMuted);
			SetAudioBusVolume((int)AudioBuses.SFX, Config.sfxVolume, Config.isSfxMuted);
			SetAudioBusVolume((int)AudioBuses.VOICE, Config.voiceVolume, Config.isVoiceMuted);
		}


		/// <summary> Applies input map configuration. </summary>
		public static void ApplyInputMap()
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
				int axisSign = mappings[3].ToInt();

				InputMap.ActionEraseEvents(actions[i]);
				InputMap.ActionSetDeadzone(actions[i], Config.deadZone);

				if (key != Key.None)
					InputMap.ActionAddEvent(actions[i], new InputEventKey()
					{
						Keycode = key
					});

				if (axis != JoyAxis.Invalid)
					InputMap.ActionAddEvent(actions[i], new InputEventJoypadMotion()
					{
						Axis = axis,
						AxisValue = axisSign
					});

				if (button != JoyButton.Invalid)
					InputMap.ActionAddEvent(actions[i], new InputEventJoypadButton()
					{
						ButtonIndex = button
					});
			}
		}


		/// <summary> Applies text localization. Be sure voiceover language is set first. </summary>
		private static void ApplyLocalization()
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
		public static void SetAudioBusVolume(int bus, int volumePercentage, bool isMuted = default)
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
		public static GameData[] GameSaveSlots = new GameData[MAX_SAVE_SLOTS]; // List of all saves created.
		public const int MAX_SAVE_SLOTS = 9; // Maximum number of save slots that can be created.

		public partial class GameData : GodotObject
		{
			/// <summary> Which area was the player in last? (Used for save select) </summary>
			public WorldEnum lastPlayedWorld;
			/// <summary> Flag representation of world rings collected. </summary>
			public WorldFlagEnum worldRingsCollected;
			/// <summary> Flag representation of worlds unlocked. </summary>
			public WorldFlagEnum worldsUnlocked;


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


			#region Level Data
			/// <summary> Dictionaries for each individual level's data. </summary>
			public Dictionary<StringName, Dictionary> levelData = new();

			private readonly StringName FIRE_SOUL_KEY = "fire_soul";
			/// <summary> Returns whether a particular fire soul has been collected or not. </summary>
			public bool IsFireSoulCollected(StringName levelID, int index)
			{
				StringName key = FIRE_SOUL_KEY + index.ToString();
				if (GetLevelData(levelID).TryGetValue(key, out Variant collected))
					return (bool)collected;

				return false;
			}


			/// <summary> Sets the save value for whether a particular fire soul is collected or not. </summary>
			public void SetFireSoulCollected(StringName levelID, int index, bool collected)
			{
				StringName key = FIRE_SOUL_KEY + index.ToString();
				if (GetLevelData(levelID).ContainsKey(key))
				{
					GetLevelData(levelID)[key] = collected;
					return;
				}

				GetLevelData(levelID).Add(key, collected);
			}


			private readonly StringName RANK_KEY = "rank";
			/// <summary> Gets the save value for the player's best rank. </summary>
			public int GetRank(StringName levelID)
			{
				if (GetLevelData(levelID).TryGetValue(RANK_KEY, out Variant rank))
					return Mathf.Clamp((int)rank, 0, 3);

				return 0; // No ranked
			}

			/// <summary> Sets the save value for the player's best rank. Ignores lower ranks. </summary>
			public void SetRank(StringName levelID, int rank)
			{
				// Discard lower ranks
				if (rank <= ActiveGameData.GetRank(levelID)) return;

				if (GetLevelData(levelID).ContainsKey(RANK_KEY))
				{
					GetLevelData(levelID)[RANK_KEY] = rank;
					return;
				}

				GetLevelData(levelID).Add(RANK_KEY, rank);
			}


			private readonly StringName SCORE_KEY = "high_score";
			/// <summary> Gets the save value for the player's high score. </summary>
			public int GetHighScore(StringName levelID)
			{
				if (GetLevelData(levelID).TryGetValue(SCORE_KEY, out Variant score))
					return (int)score;

				return 0; // No score recorded
			}

			/// <summary> Sets the save value for the player's high score. Ignores lower scores. </summary>
			public void SetHighScore(StringName levelID, int score)
			{
				// Discard lower scores
				if (score <= ActiveGameData.GetHighScore(levelID)) return;

				if (GetLevelData(levelID).ContainsKey(SCORE_KEY))
				{
					GetLevelData(levelID)[SCORE_KEY] = score;
					return;
				}

				GetLevelData(levelID).Add(SCORE_KEY, score);
			}


			private readonly StringName TIME_KEY = "best_time";
			/// <summary> Gets the save value for the player's best rank. </summary>
			public float GetBestTime(StringName levelID)
			{
				if (GetLevelData(levelID).TryGetValue(TIME_KEY, out Variant time))
					return (float)time;

				return 0; // No time recorded
			}

			/// <summary> Sets the value for the player's best time. Ignores slower times. </summary>
			public void SetBestTime(StringName levelID, float time)
			{
				// Discard lower scores
				if (!Mathf.IsZeroApprox(ActiveGameData.GetBestTime(levelID)) && time > ActiveGameData.GetBestTime(levelID)) return;

				if (GetLevelData(levelID).ContainsKey(TIME_KEY))
				{
					GetLevelData(levelID)[TIME_KEY] = time;
					return;
				}

				GetLevelData(levelID).Add(TIME_KEY, time);
			}


			/// <summary> Returns the dictionary of a particular level. </summary>
			public Dictionary GetLevelData(StringName levelID)
			{
				if (!levelData.ContainsKey(levelID)) // Create new level data if it's missing
					levelData.Add(levelID, new());

				return levelData[levelID];
			}
			#endregion


			/// <summary> Creates a dictionary based on GameData. </summary>
			public Dictionary ToDictionary()
			{
				Dictionary dictionary = new()
				{
					// WorldEnum data
					{ nameof(lastPlayedWorld), (int)lastPlayedWorld },
					{ nameof(worldRingsCollected), (int)worldRingsCollected },
					{ nameof(worldsUnlocked), (int)worldsUnlocked },
					{ nameof(levelData), (Dictionary)levelData },


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
				// WorldEnum data
				if (dictionary.TryGetValue(nameof(lastPlayedWorld), out Variant var))
					lastPlayedWorld = (WorldEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldRingsCollected), out var))
					worldRingsCollected = (WorldFlagEnum)(int)var;
				if (dictionary.TryGetValue(nameof(worldsUnlocked), out var))
					worldsUnlocked = (WorldFlagEnum)(int)var;

				if (dictionary.TryGetValue(nameof(levelData), out var))
					levelData = (Dictionary<StringName, Dictionary>)var;


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

				if (DebugManager.Instance.UseDemoSave) // Unlock all worlds in the demo
					data.worldsUnlocked = WorldFlagEnum.All;
				data.skillRing.RefreshSkillRingData(data.level);
				return data;
			}
		}


		/// <summary> Saves active game data to a file. </summary>
		public static void SaveGameData()
		{
			if (ActiveSaveSlotIndex == -1) return; // Invalid save slot

			// TODO Write save data to a file.
			string saveNumber = ActiveSaveSlotIndex.ToString("00");
			FileAccess file = FileAccess.Open(SAVE_DIRECTORY + $"save{saveNumber}.dat", FileAccess.ModeFlags.Write);

			if (FileAccess.GetOpenError() == Error.Ok)
			{
				file.StoreString(Json.Stringify(ActiveGameData.ToDictionary(), "\t"));
				file.Close();
			}
			else
			{
				// TODO Show an error message to the player? 
			}
		}


		/// <summary> Preloads game data so it can be displayed on menus. </summary>
		public static void LoadGameData()
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
			if (OS.IsDebugBuild()) // For testing
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