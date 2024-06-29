using System.Linq;
using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Core;

public partial class SaveManager : Node
{
	public static SaveManager Instance;

	private const string SaveDirectory = "user://";

	[Signal]
	public delegate void ConfigAppliedEventHandler();

	public override void _EnterTree()
	{
		Instance = this;

		LoadConfig();
		LoadGameData();

		if (OS.IsDebugBuild()) // Editor build, use custom configuration
		{
			// Default debug settings for testing from the editor.
			Config.isMasterMuted = AudioServer.IsBusMute((int)SoundManager.AudioBuses.Master);
			Config.isBgmMuted = AudioServer.IsBusMute((int)SoundManager.AudioBuses.Bgm);
			Config.isSfxMuted = AudioServer.IsBusMute((int)SoundManager.AudioBuses.Sfx);
			Config.isVoiceMuted = AudioServer.IsBusMute((int)SoundManager.AudioBuses.Voice);

			Config.masterVolume =
				Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)SoundManager.AudioBuses.Master)) * 100);
			Config.bgmVolume =
				Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)SoundManager.AudioBuses.Bgm)) * 100);
			Config.sfxVolume =
				Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)SoundManager.AudioBuses.Sfx)) * 100);
			Config.voiceVolume =
				Mathf.RoundToInt(Mathf.DbToLinear(AudioServer.GetBusVolumeDb((int)SoundManager.AudioBuses.Voice)) * 100);
			ApplyConfig();
		}
	}

	#region Config
	public static ConfigData Config = new();
	public static bool UseEnglishVoices => Config.voiceLanguage == VoiceLanguage.English;
	private const string ConfigFileName = "config.cfg";

	#region Config Enums
	public enum ControllerType
	{
		Automatic, // Automatically try to detect controller type
		PlayStation, // Use PlayStation button prompts
		Xbox, // Use XBox button prompts
		Nintendo, // Use Nintendo button prompts
		Steam, // Use Steam Deck button prompts
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
		Disabled,
		Low,
		Medium,
		High,
		Count
	}

	public static readonly Vector2I[] WindowSizes =
	[
		new(640, 360), // 360p
		new(854, 480), // 480p
		new(1280, 720), // 720p
		new(1600, 900), // 900p
		new(1920, 1080), // 1080p
		new(2560, 1440), // 1440p
		new(3840, 2160), // 4K
	];

	#endregion

	public partial class ConfigData : GodotObject
	{
		// Video
		public int targetDisplay = DisplayServer.GetPrimaryScreen();
		public int windowSize = 3; // Defaults to one lower than 1080p
		public bool useFullscreen = true;
		public bool useExclusiveFullscreen;
		public bool useVsync;
		public int renderScale = 100;
		public RenderingServer.ViewportScaling3DMode resizeMode = RenderingServer.ViewportScaling3DMode.Bilinear;
		public int antiAliasing = 1; // Default to FXAA
		public bool useHDBloom = true;
		public QualitySetting softShadowQuality = QualitySetting.Medium;
		public QualitySetting postProcessingQuality = QualitySetting.Medium;
		public QualitySetting reflectionQuality = QualitySetting.High;

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
		FileAccess file = FileAccess.Open(SaveDirectory + ConfigFileName, FileAccess.ModeFlags.Read);

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
		FileAccess file = FileAccess.Open(SaveDirectory + ConfigFileName, FileAccess.ModeFlags.Write);
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
		{
			targetMode = Config.useExclusiveFullscreen
				? DisplayServer.WindowMode.ExclusiveFullscreen
				: DisplayServer.WindowMode.Fullscreen;
		}

		if (DisplayServer.WindowGetMode() != targetMode)
			DisplayServer.WindowSetMode(targetMode);
		if (!Config.useFullscreen)
			DisplayServer.WindowSetSize(WindowSizes[Config.windowSize]);

		DisplayServer.VSyncMode targetVSyncMode =
			Config.useVsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
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

		int targetShadowAtlasSize = 4096;
		bool use16BitShadowAtlas = Config.softShadowQuality == QualitySetting.High;
		RenderingServer.ShadowQuality targetSoftShadowQuality = RenderingServer.ShadowQuality.Hard;
		switch (Config.softShadowQuality)
		{
			case QualitySetting.Low:
				targetSoftShadowQuality = RenderingServer.ShadowQuality.SoftLow;
				break;
			case QualitySetting.Medium:
				targetShadowAtlasSize = 4096;
				targetSoftShadowQuality = RenderingServer.ShadowQuality.SoftMedium;
				break;
			case QualitySetting.High:
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
			case QualitySetting.Low:
				RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Low, true, .5f, 2, 50,
					300);
				RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.Low, true, .5f, 2, 50,
					300);
				break;
			case QualitySetting.Medium:
				RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Medium, true, .5f, 2,
					50, 300);
				RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.Medium, true, .5f, 2,
					50, 300);
				break;
			case QualitySetting.High:
				RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High, false, .5f, 2,
					50, 300);
				RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.High, false, .5f, 2,
					50, 300);
				break;
		}

		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.Master, Config.masterVolume, Config.isMasterMuted);
		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.Bgm, Config.bgmVolume, Config.isBgmMuted);
		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.Sfx, Config.sfxVolume, Config.isSfxMuted);
		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.Voice, Config.voiceVolume, Config.isVoiceMuted);

		Instance.EmitSignal(SignalName.ConfigApplied);
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
			{
				InputMap.ActionAddEvent(actions[i], new InputEventKey()
				{
					Keycode = key
				});
			}

			if (axis != JoyAxis.Invalid)
			{
				InputMap.ActionAddEvent(actions[i], new InputEventJoypadMotion()
				{
					Axis = axis,
					AxisValue = axisSign
				});
			}

			if (button != JoyButton.Invalid)
			{
				InputMap.ActionAddEvent(actions[i], new InputEventJoypadButton()
				{
					ButtonIndex = button
				});
			}
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

	#endregion

	#region Game data
	/// <summary> Longest amount of playtime that can be displayed on the file select. (99:59:59 in seconds) </summary>
	public const int MAX_PLAY_TIME = 359999;

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
	public static GameData ActiveGameData
	{
		get
		{
			if (ActiveSaveSlotIndex != -1)
				return GameSaveSlots[ActiveSaveSlotIndex];

			// Default to save slot 1 when running the game from the editor
			return OS.IsDebugBuild() ? GameSaveSlots[0] : null;
		}
	}

	/// <summary> Current skill ring. </summary>
	public static SkillRing ActiveSkillRing = new();
	public static GameData[] GameSaveSlots = new GameData[MAX_SAVE_SLOTS]; // List of all saves created.
	public const int MAX_SAVE_SLOTS = 9; // Maximum number of save slots that can be created.

	/// <summary> Saves active game data to a file. </summary>
	public static void SaveGameData()
	{
		if (ActiveSaveSlotIndex == -1) return; // Invalid save slot

		// TODO Write save data to a file.
		string saveNumber = ActiveSaveSlotIndex.ToString("00");
		FileAccess file = FileAccess.Open(SaveDirectory + $"save{saveNumber}.dat", FileAccess.ModeFlags.Write);

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
			FileAccess file = FileAccess.Open(SaveDirectory + $"save{saveNumber}.dat", FileAccess.ModeFlags.Read);
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
			ActiveGameData.UnlockAllWorlds();
			ActiveSkillRing.LoadFromActiveData();
		}
	}

	/// <summary> Frees game data at the given index, then creates default data in it's place. </summary>
	public static void ResetSaveData(int index)
	{
		GameSaveSlots[index].Free();
		GameSaveSlots[index] = GameData.DefaultData();
	}

	public partial class GameData : GodotObject
	{
		/// <summary> Which area was the player in last? (Used for save select) </summary>
		public WorldEnum lastPlayedWorld;

		/// <summary> List of world rings collected. </summary>
		public Array<WorldEnum> worldRingsCollected = [];
		/// <summary> List of worlds unlocked. </summary>
		public Array<WorldEnum> worldsUnlocked = [];
		/// <summary> List of worlds unlocked. </summary>
		public Array<string> stagesUnlocked = [];

		/// <summary> Player level, from 1 -> 99 </summary>
		public int level;
		/// <summary> The player's level must be at least one, so a file with level zero is treated as empty. </summary>
		public bool IsNewFile() => level == 0;

		/// <summary> How much exp the player currently has. </summary>
		public int exp;
		/// <summary> Total playtime, in seconds. </summary>
		public float playTime;

		public Array<SkillKey> equippedSkills = [];
		/// <summary> Total number of fire souls the player collected. </summary>
		public int FireSoulCount { get; private set; }
		/// <summary> Total number of gold medals the player has collected. </summary>
		public int GoldMedalCount { get; private set; }
		/// <summary> Total number of silver medals the player has collected. </summary>
		public int SilverMedalCount { get; private set; }
		/// <summary> Total number of bronze medals the player has collected. </summary>
		public int BronzeMedalCount { get; private set; }

		/// <summary> Calculates the player's soul gauge size based on the player's level. </summary>
		public int CalculateMaxSoulPower()
		{
			int maxSoulPower = 100; // Starting soul gauge size
			maxSoulPower += Mathf.FloorToInt(CalculateSoulGaugeLevelRatio() * 5f) * 20; // Soul Gauge size increases by 20 every 5 levels, so it caps at 300
			return maxSoulPower;
		}

		/// <summary> Current ratio (0 -> 1) compared to the soul gauge level cap (50). </summary>
		public float CalculateSoulGaugeLevelRatio() => Mathf.Clamp(level, 0, 50) / (float)50;

		/// <summary> Checks if a stage has been unlocked. </summary>
		public bool IsStageUnlocked(string levelID) => stagesUnlocked.Contains(levelID);
		/// <summary> Unlocks a stage. </summary>
		public void UnlockStage(string levelID)
		{
			if (stagesUnlocked.Contains(levelID))
				return;

			stagesUnlocked.Add(levelID);
		}

		/// <summary> Checks if a world is unlocked. </summary>
		public bool IsWorldUnlocked(WorldEnum world) => worldsUnlocked.Contains(world);
		/// <summary> Checks if a world ring was obtained. </summary>
		public bool IsWorldRingObtained(WorldEnum world) => worldRingsCollected.Contains(world);
		public void UnlockWorld(WorldEnum world)
		{
			if (worldsUnlocked.Contains(world))
				return;

			worldsUnlocked.Add(world);
		}

		public void UnlockAllWorlds()
		{
			for (int i = 0; i < (int)WorldEnum.Max; i++)
				UnlockWorld((WorldEnum)i);
		}

		#region Level Data
		/// <summary> Dictionaries for each individual level's data. </summary>
		public Dictionary<StringName, Dictionary> levelData = [];

		private readonly StringName FireSoulKey = "fire_soul";
		/// <summary> Returns whether a particular fire soul has been collected or not. </summary>
		public bool IsFireSoulCollected(StringName levelID, int index)
		{
			StringName key = FireSoulKey + index.ToString();
			if (GetLevelData(levelID).TryGetValue(key, out Variant collected))
				return (bool)collected;

			return false;
		}

		/// <summary> Sets the save value for whether a particular fire soul is collected or not. </summary>
		public void SetFireSoulCollected(StringName levelID, int index, bool collected)
		{
			StringName key = FireSoulKey + index.ToString();
			if (GetLevelData(levelID).ContainsKey(key))
			{
				GetLevelData(levelID)[key] = collected;
				return;
			}

			FireSoulCount++;
			GetLevelData(levelID).Add(key, collected);
		}

		private readonly StringName RankKey = "rank";
		/// <summary> Gets the save value for the player's best rank. </summary>
		public int GetRank(StringName levelID)
		{
			if (GetLevelData(levelID).TryGetValue(RankKey, out Variant rank))
				return (int)rank;

			return -1; // No recorded rank; Return -1 to avoid getting confused with "no medal"
		}

		/// <summary> Gets the save value for the player's best rank, clamped so unplayed stages count as 0. </summary>
		public int GetRankClamped(StringName levelID) => Mathf.Clamp(GetRank(levelID), 0, 3);

		/// <summary> Sets the save value for the player's best rank. Ignores lower ranks. </summary>
		public void SetRank(StringName levelID, int rank)
		{
			// Discard lower ranks
			if (rank <= ActiveGameData.GetRank(levelID)) return;

			if (GetLevelData(levelID).ContainsKey(RankKey))
			{
				UpdateMedals(rank, (int)GetLevelData(levelID)[RankKey]);
				GetLevelData(levelID)[RankKey] = rank;
				return;
			}

			UpdateMedals(rank);
			GetLevelData(levelID).Add(RankKey, rank);
		}

		private readonly StringName ScoreKey = "high_score";
		/// <summary> Gets the save value for the player's high score. </summary>
		public int GetHighScore(StringName levelID)
		{
			if (GetLevelData(levelID).TryGetValue(ScoreKey, out Variant score))
				return (int)score;

			return 0; // No score recorded
		}

		/// <summary> Sets the save value for the player's high score. Ignores lower scores. </summary>
		public void SetHighScore(StringName levelID, int score)
		{
			// Discard lower scores
			if (score <= ActiveGameData.GetHighScore(levelID)) return;

			if (GetLevelData(levelID).ContainsKey(ScoreKey))
			{
				GetLevelData(levelID)[ScoreKey] = score;
				return;
			}

			GetLevelData(levelID).Add(ScoreKey, score);
		}

		private readonly StringName TimeKey = "best_time";
		/// <summary> Gets the save value for the player's best rank. </summary>
		public float GetBestTime(StringName levelID)
		{
			if (GetLevelData(levelID).TryGetValue(TimeKey, out Variant time))
				return (float)time;

			return 0; // No time recorded
		}

		/// <summary> Sets the value for the player's best time. Ignores slower times. </summary>
		public void SetBestTime(StringName levelID, float time)
		{
			// Discard lower scores
			if (!Mathf.IsZeroApprox(ActiveGameData.GetBestTime(levelID)) &&
				time > ActiveGameData.GetBestTime(levelID))
			{
				return;
			}

			if (GetLevelData(levelID).ContainsKey(TimeKey))
			{
				GetLevelData(levelID)[TimeKey] = time;
				return;
			}

			GetLevelData(levelID).Add(TimeKey, time);
		}

		private readonly StringName StatusKey = "clear_status";
		/// <summary> Returns the clear state of a level. </summary>
		public LevelStatus GetClearStatus(StringName levelID)
		{
			if (GetLevelData(levelID).TryGetValue(StatusKey, out Variant status))
				return (LevelStatus)(int)status;

			return LevelStatus.New;
		}

		/// <summary> Sets the clear state of a level. </summary>
		public void SetClearStatus(StringName levelID, LevelStatus clearStatus)
		{
			// Return early if the level has already been cleared
			if (ActiveGameData.GetClearStatus(levelID) == LevelStatus.Cleared)
				return;

			if (GetLevelData(levelID).ContainsKey(StatusKey))
			{
				GetLevelData(levelID)[StatusKey] = (int)clearStatus;
				return;
			}

			GetLevelData(levelID).Add(StatusKey, (int)clearStatus);
		}

		public enum LevelStatus
		{
			New, // Player has never touched the level
			Attempted, // Player played the level, but never cleared it
			Cleared, // Player has cleared the level
		}

		/// <summary> Returns the dictionary of a particular level. </summary>
		public Dictionary GetLevelData(StringName levelID)
		{
			if (!levelData.ContainsKey(levelID)) // Create new level data if it's missing
				levelData.Add(levelID, []);

			return levelData[levelID];
		}
		#endregion

		/// <summary> Creates a dictionary based on GameData. </summary>
		public Dictionary ToDictionary()
		{
			return new()
			{
				// WorldEnum data
				{ nameof(lastPlayedWorld), (int)lastPlayedWorld },
				{ nameof(worldsUnlocked), worldsUnlocked },
				{ nameof(worldRingsCollected), worldRingsCollected },
				{ nameof(stagesUnlocked), stagesUnlocked },
				{ nameof(levelData), (Dictionary)levelData },

				// Player stats
				{ nameof(level), level },
				{ nameof(exp), exp },
				{ nameof(playTime), Mathf.RoundToInt(playTime) },
				{ nameof(equippedSkills), equippedSkills },
			};
		}

		/// <summary> Sets GameData based on dictionary. </summary>
		public void FromDictionary(Dictionary dictionary)
		{
			// WorldEnum data
			if (dictionary.TryGetValue(nameof(lastPlayedWorld), out Variant var))
				lastPlayedWorld = (WorldEnum)(int)var;
			if (dictionary.TryGetValue(nameof(worldsUnlocked), out var))
			{
				Array<int> worlds = (Array<int>)var;
				for (int i = 0; i < worlds.Count; i++)
					worldsUnlocked.Add((WorldEnum)worlds[i]);
			}
			if (dictionary.TryGetValue(nameof(worldRingsCollected), out var))
			{
				Array<int> worlds = (Array<int>)var;
				for (int i = 0; i < worlds.Count; i++)
					worldRingsCollected.Add((WorldEnum)worlds[i]);
			}
			if (dictionary.TryGetValue(nameof(worldsUnlocked), out var))
				stagesUnlocked = (Array<string>)var;

			if (dictionary.TryGetValue(nameof(levelData), out var))
				levelData = (Dictionary<StringName, Dictionary>)var;

			if (dictionary.TryGetValue(nameof(level), out var))
				level = (int)var;
			if (dictionary.TryGetValue(nameof(exp), out var))
				exp = (int)var;
			if (dictionary.TryGetValue(nameof(playTime), out var))
				playTime = (float)var;

			if (dictionary.TryGetValue(nameof(equippedSkills), out var))
			{
				Array<int> skills = (Array<int>)var;
				for (int i = 0; i < skills.Count; i++)
					equippedSkills.Add((SkillKey)skills[i]);
			}

			// Update runtime data based on save data
			StringName[] keys = levelData.Keys.ToArray();
			for (int i = 0; i < keys.Length; i++)
			{
				UpdateMedals(GetRank(keys[i]));

				for (int j = 1; j < 4; j++) // Check fire souls
				{
					if (IsFireSoulCollected(keys[i], j))
						FireSoulCount++;
				}
			}
		}

		private void UpdateMedals(int rank, int oldRank = 0)
		{
			if (rank >= 3 && oldRank < 3)
				GoldMedalCount++;
			if (rank >= 2 && oldRank < 2)
				SilverMedalCount++;
			if (rank >= 1 && oldRank < 1)
				BronzeMedalCount++;
		}

		/// <summary> Creates a new GameData object that contains default values. </summary>
		public static GameData DefaultData()
		{
			GameData data = new()
			{
				level = 1,
				lastPlayedWorld = WorldEnum.LostPrologue,
			};

			// TODO Replace this with the tutorial key
			data.UnlockStage("so_a1_main");
			data.UnlockWorld(WorldEnum.SandOasis);

			if (DebugManager.Instance.UseDemoSave) // Unlock all worlds in the demo
				data.UnlockAllWorlds();

			return data;
		}
	}
	#endregion
}