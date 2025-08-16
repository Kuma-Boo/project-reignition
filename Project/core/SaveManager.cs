using System;
using System.Linq;
using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Core;

public partial class SaveManager : Node
{
	public static SaveManager Instance;

	[Signal] public delegate void ConfigAppliedEventHandler();

	private static string SaveDirectory;
	private static string SaveLocationFile => OS.GetExecutablePath().GetBaseDir() + "/saveLocation.txt";

	public override void _EnterTree()
	{
		Instance = this;

		CacheInitialInputMap();
		SaveDirectory = ProjectSettings.GlobalizePath(GetSaveDirectory());
		MenuData = GameData.CreateDefaultData(); // Create a default game data object for the menu
		SharedData = SharedGameData.CreateDefaultData();

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

	private string GetSaveDirectory()
	{
		FileAccess f = FileAccess.Open(SaveLocationFile, FileAccess.ModeFlags.Read);
		if (f != null && f.GetError() == Error.Ok)
		{
			string targetDirectory = f.GetAsText();
			f.Close();

			if (!string.IsNullOrWhiteSpace(targetDirectory) && DirAccess.DirExistsAbsolute(targetDirectory))
				return targetDirectory;

			// Fallback to executable path when directory is missing (only when a saveLocation file exists).
			return OS.GetExecutablePath().GetBaseDir() + "/save/";
		}

		// Fallback to appdata
		return "user://";
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

	public enum ButtonStyle
	{
		Style1, // Standard controller theme
		Style2, // White/Nintendo Wii controller theme
		Count
	}

	public enum VoiceLanguage
	{
		English,
		Japanese,
		Spanish,
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
		BrazilianPortuguese,
		Polish,
		Chinese,
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

	public enum AspectRatio
	{
		FourByThree,
		SixteenByNine,
		SixteenByTen,
		TwentyoneByNine,
		Count
	}

	public enum HudStyle
	{
		Retail,
		Reignition,
		E3,
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

	public static readonly Vector2I[] WindowSizes4x3 =
	[
		new(640, 480), // 480p, VGA
		new(800, 600), // 600p, SVGA
		new(1024, 768), // 768p, XGA
		new(1152, 864), // 864p, XGA+
		new(2048, 1536), // 1536p, QXGA
		new(3200, 2400), // 2400p, QUXGA
		new(4096, 3072), // 3072p, HXGA
		new(6400, 4800), // 4800p, HUXGA
    ];

	public static readonly Vector2I[] WindowSizes16x10 =
	[
		new(768, 480), // 480p
		new(1152, 720), // 720p
		new(1280, 800), // 800p, WXGA (steam deck)
		new(1440, 900), // 900p, WXGA+
		new(1680, 1050), // 1050p, WSXGA+
		new(1920, 1200), // 1200p, WUXGA
		new(2560, 1600), // 1600p, WQXGA
		new(3840, 2400), // 2400p, WQUXGA
    ];

	public static readonly Vector2I[] WindowSizes21x9 =
	[
		new(1120, 480), // 480p
		new(1400, 600), // 600p
		new(2560, 1080), // 1080p, WFHD
		new(2880, 1200), // 1200p, WFHD+
		new(3440, 1440), // 1440p, WQHD
		new(3840, 1600), // 1600p, WQHD+
		new(4320, 1800), // 1800p, UW4k
		new(5120, 2160), // 2160p, UW5K
		new(5760, 2400), // 2400p, UW5K+
		new(6144, 2560), // 2560p, UW6K
		new(6880, 2880), // 2880p, UW6K+
		new(7680, 3200), // 3200p, UW7K
		new(8640, 3600), // 3600p, UW10K
    ];

	public static readonly int[] FrameRates =
	[
		0,
		30,
		45,
		60,
		120,
	];

	#endregion

	public partial class ConfigData : GodotObject
	{
		// Video
		public int targetDisplay = DisplayServer.GetPrimaryScreen();
		public AspectRatio aspectRatio = AspectRatio.SixteenByNine;
		public int windowSize = 3; // Defaults to one lower than 1080p
		public bool useFullscreen = true;
		public bool useExclusiveFullscreen;
		public int framerate = 3;
		public bool useVsync;
		public int renderScale = 100;
		public RenderingServer.ViewportScaling3DMode resizeMode = RenderingServer.ViewportScaling3DMode.Bilinear;
		public int antiAliasing = 1; // Default to FXAA
		public QualitySetting bloomMode = QualitySetting.High;
		public bool useMotionBlur = true;
		public bool useScreenShake = true;
		public int screenShake = 100;
		public QualitySetting softShadowQuality = QualitySetting.Medium;
		public QualitySetting postProcessingQuality = QualitySetting.Medium;
		public QualitySetting reflectionQuality = QualitySetting.High;

		// Audio
		public bool isMasterMuted;
		public int masterVolume = 30;
		public bool isBgmMuted;
		public int bgmVolume = 50;
		public bool isSfxMuted;
		public int sfxVolume = 50;
		public bool isVoiceMuted;
		public int voiceVolume = 50;
		public bool useRetailMenuMusic;

		// Controls
		public float deadZone = .2f;
		public ControllerType controllerType = ControllerType.Automatic;
		public bool useHoldBreakMode = true;
		public bool useStompJumpButtonMode;
		public int[] partyModeDevices = [0, 0, 0, 0];
		public Dictionary inputConfiguration = [];

		// Language
		public bool isSubtitleDisabled;
		public bool isDialogDisabled;
		public TextLanguage textLanguage = AutoDetectTextLocale();
		public VoiceLanguage voiceLanguage = AutoDetectVoiceLocale();

		// Interface
		public HudStyle hudStyle = HudStyle.Retail;
		public ButtonStyle buttonStyle = ButtonStyle.Style2;
		public bool isUsingHorizontalSoulGauge;
		public bool isActionPromptsEnabled = true;

		/// <summary> Creates a dictionary based on config data. </summary>
		public Dictionary ToDictionary()
		{
			return new()
			{
				// Video
				{ nameof(targetDisplay), targetDisplay },
				{ nameof(aspectRatio), (int)aspectRatio},
				{ nameof(windowSize), windowSize },
				{ nameof(useFullscreen), useFullscreen },
				{ nameof(useExclusiveFullscreen), useExclusiveFullscreen },
				{ nameof(framerate), framerate },
				{ nameof(useVsync), useVsync },

				{ nameof(renderScale), renderScale },
				{ nameof(resizeMode), (int)resizeMode },
				{ nameof(antiAliasing), antiAliasing },
				{ nameof(bloomMode), (int)bloomMode },
				{ nameof(softShadowQuality), (int)softShadowQuality },
				{ nameof(postProcessingQuality), (int)postProcessingQuality },
				{ nameof(reflectionQuality), (int)reflectionQuality },
				{ nameof(useMotionBlur), useMotionBlur },
				{ nameof(useScreenShake), useScreenShake },
				{ nameof(screenShake), screenShake },

				// Audio
				{ nameof(isMasterMuted), isMasterMuted },
				{ nameof(masterVolume), masterVolume },
				{ nameof(isBgmMuted), isBgmMuted },
				{ nameof(bgmVolume), bgmVolume },
				{ nameof(isSfxMuted), isSfxMuted },
				{ nameof(sfxVolume), sfxVolume },
				{ nameof(isVoiceMuted), isVoiceMuted },
				{ nameof(voiceVolume), voiceVolume },
				{ nameof(useRetailMenuMusic), useRetailMenuMusic},

				// Controls
				{ nameof(deadZone), deadZone },
				{ nameof(controllerType), (int)controllerType },
				{ nameof(useHoldBreakMode), useHoldBreakMode },
				{ nameof(useStompJumpButtonMode), useStompJumpButtonMode },
				{ nameof(partyModeDevices), partyModeDevices },
				{ nameof(inputConfiguration), inputConfiguration },

				// Language
				{ nameof(isSubtitleDisabled), isSubtitleDisabled },
				{ nameof(isDialogDisabled), isDialogDisabled},
				{ nameof(voiceLanguage), (int)voiceLanguage },
				{ nameof(textLanguage), (int)textLanguage },

				// Interface
				{ nameof(hudStyle), (int)hudStyle },
				{ nameof(buttonStyle), (int)buttonStyle },
				{ nameof(isUsingHorizontalSoulGauge), (bool)isUsingHorizontalSoulGauge },
				{ nameof(isActionPromptsEnabled), isActionPromptsEnabled },
			};
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
			if (dictionary.TryGetValue(nameof(aspectRatio), out var))
				aspectRatio = (AspectRatio)(int)var;
			if (dictionary.TryGetValue(nameof(windowSize), out var))
				windowSize = (int)var;
			if (dictionary.TryGetValue(nameof(framerate), out var))
				framerate = (int)var;
			if (dictionary.TryGetValue(nameof(useVsync), out var))
				useVsync = (bool)var;

			if (dictionary.TryGetValue(nameof(renderScale), out var))
				renderScale = (int)var;
			if (dictionary.TryGetValue(nameof(resizeMode), out var))
				resizeMode = (RenderingServer.ViewportScaling3DMode)(int)var;
			if (dictionary.TryGetValue(nameof(antiAliasing), out var))
				antiAliasing = (int)var;
			if (dictionary.TryGetValue(nameof(bloomMode), out var))
				bloomMode = (QualitySetting)(int)var;
			if (dictionary.TryGetValue(nameof(softShadowQuality), out var))
				softShadowQuality = (QualitySetting)(int)var;
			if (dictionary.TryGetValue(nameof(postProcessingQuality), out var))
				postProcessingQuality = (QualitySetting)(int)var;
			if (dictionary.TryGetValue(nameof(reflectionQuality), out var))
				reflectionQuality = (QualitySetting)(int)var;
			if (dictionary.TryGetValue(nameof(useMotionBlur), out var))
				useMotionBlur = (bool)var;
			if (dictionary.TryGetValue(nameof(useScreenShake), out var))
				useScreenShake = (bool)var;
			if (dictionary.TryGetValue(nameof(screenShake), out var))
				screenShake = (int)var;

			// Audio
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
			if (dictionary.TryGetValue(nameof(useRetailMenuMusic), out var))
				useRetailMenuMusic = (bool)var;


			// Controls
			if (dictionary.TryGetValue(nameof(deadZone), out var))
				deadZone = (float)var;
			if (dictionary.TryGetValue(nameof(controllerType), out var))
				controllerType = (ControllerType)(int)var;
			if (dictionary.TryGetValue(nameof(useHoldBreakMode), out var))
				useHoldBreakMode = (bool)var;
			if (dictionary.TryGetValue(nameof(useStompJumpButtonMode), out var))
				useStompJumpButtonMode = (bool)var;
			if (dictionary.TryGetValue(nameof(partyModeDevices), out var))
				partyModeDevices = (int[])var;
			if (dictionary.TryGetValue(nameof(inputConfiguration), out var))
				inputConfiguration = (Dictionary)Json.ParseString((string)var);

			// Language
			if (dictionary.TryGetValue(nameof(isSubtitleDisabled), out var))
				isSubtitleDisabled = (bool)var;
			if (dictionary.TryGetValue(nameof(isDialogDisabled), out var))
				isDialogDisabled = (bool)var;
			if (dictionary.TryGetValue(nameof(voiceLanguage), out var))
				voiceLanguage = (VoiceLanguage)(int)var;
			if (dictionary.TryGetValue(nameof(textLanguage), out var))
				textLanguage = (TextLanguage)(int)var;

			// Interface
			if (dictionary.TryGetValue(nameof(hudStyle), out var))
				hudStyle = (HudStyle)(int)var;
			if (dictionary.TryGetValue(nameof(buttonStyle), out var))
				buttonStyle = (ButtonStyle)(int)var;
			if (dictionary.TryGetValue(nameof(isUsingHorizontalSoulGauge), out var))
				isUsingHorizontalSoulGauge = (bool)var;
			if (dictionary.TryGetValue(nameof(isActionPromptsEnabled), out var))
				isActionPromptsEnabled = (bool)var;
		}
	}

	private static TextLanguage AutoDetectTextLocale()
	{
		return OS.GetLocaleLanguage() switch
		{
			"ja" => TextLanguage.Japanese,
			"de" => TextLanguage.German,
			"it" => TextLanguage.Italian,
			"fr" => TextLanguage.French,
			"es" => TextLanguage.Spanish,
			"pt" => TextLanguage.BrazilianPortuguese,
			"pl" => TextLanguage.Polish,
			"zh" => TextLanguage.Chinese,
			_ => TextLanguage.English,
		};
	}

	private static VoiceLanguage AutoDetectVoiceLocale()
	{
		TextLanguage autoTextLocale = AutoDetectTextLocale();

		if (autoTextLocale == TextLanguage.Japanese)
			return VoiceLanguage.Japanese;

		if (autoTextLocale == TextLanguage.Spanish)
			return VoiceLanguage.Spanish;

		return VoiceLanguage.English;
	}

	/// <summary> Attempts to load config data from file. </summary>
	public static void LoadConfig()
	{
		string configFile = SaveDirectory.PathJoin(ConfigFileName);
		FileAccess file = FileAccess.Open(configFile, FileAccess.ModeFlags.Read);

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
		if (!DirAccess.DirExistsAbsolute(SaveDirectory))
			DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);

		string configFile = SaveDirectory.PathJoin(ConfigFileName);
		FileAccess file = FileAccess.Open(configFile, FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(Config.ToDictionary(), "\t"));
		file.Close();

		file = FileAccess.Open(SaveLocationFile, FileAccess.ModeFlags.Write);
		file.StoreString(SaveDirectory);
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
		{
			switch (Config.aspectRatio)
			{
				case AspectRatio.FourByThree:
					Instance.GetTree().Root.Size = WindowSizes4x3[Config.windowSize];
					break;
				case AspectRatio.SixteenByTen:
					Instance.GetTree().Root.Size = WindowSizes16x10[Config.windowSize];
					break;
				case AspectRatio.TwentyoneByNine:
					Instance.GetTree().Root.Size = WindowSizes21x9[Config.windowSize];
					break;
				default:
					Instance.GetTree().Root.Size = WindowSizes[Config.windowSize];
					break;
			}
		}

		Vector2I resolution = Instance.GetTree().Root.Size;
		float ratio = resolution.X / (float)resolution.Y;
		Instance.GetTree().Root.ContentScaleSize = new Vector2I(Mathf.RoundToInt(1920 / ratio), 1080);

		Engine.MaxFps = FrameRates[Config.framerate];
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

		RenderingServer.EnvironmentGlowSetUseBicubicUpscale(Config.bloomMode == QualitySetting.High);

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
			case TextLanguage.BrazilianPortuguese:
				TranslationServer.SetLocale("pt_BR");
				break;
			case TextLanguage.Polish:
				TranslationServer.SetLocale("pl");
				break;
			case TextLanguage.Chinese:
				TranslationServer.SetLocale("zh");
				break;
			default:
				// Prefer the retranslation for all languages except when using the voiceover
				TranslationServer.SetLocale(UseEnglishVoices ? "en" : "en_US");
				break;
		}
	}

	#endregion

	#region Input
	private static readonly Dictionary initialInputMap = [];
	private static void CacheInitialInputMap()
	{
		foreach (StringName action in InputMap.GetActions())
		{
			// Only store gameplay actions
			if (!action.ToString().StartsWith("move_") && !action.ToString().StartsWith("button_") && !action.ToString().StartsWith("sys_"))
				continue;

			initialInputMap.Add(action, GenerateInputMappingString(action));
		}
	}

	private static string GenerateInputMappingString(StringName action)
	{
		Array<InputEvent> eventList = InputMap.ActionGetEvents(action); // Refresh event list

		// Construct the mapping string
		int[] mappingList = [(int)Key.None, (int)JoyAxis.Invalid, (int)JoyButton.Invalid];
		int axisSign = 0;
		foreach (var e in eventList)
		{
			if (e is InputEventKey key)
			{
				mappingList[0] = (int)key.Keycode;
			}
			else if (e is InputEventJoypadMotion motion)
			{
				mappingList[1] = (int)motion.Axis;
				axisSign = Mathf.Sign(motion.AxisValue);
			}
			else if (e is InputEventJoypadButton button)
			{
				mappingList[2] = (int)button.ButtonIndex;
			}
		}

		return $"{mappingList[0]}, {mappingList[1]}, {mappingList[2]}, {axisSign}";
	}

	public static void SaveInputAction(StringName action)
	{
		string mappingString = GenerateInputMappingString(action);
		if (Config.inputConfiguration.ContainsKey(action))
			Config.inputConfiguration[action] = mappingString;
		else
			Config.inputConfiguration.Add(action, mappingString);

		ApplyConfig();
	}

	public static void ResetInputMap()
	{
		Config.inputConfiguration = initialInputMap.Duplicate(true);
		ApplyInputMap();
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
	#endregion

	#region Game data
	/// <summary> Longest amount of playtime that can be displayed on the file select. (99:59:59 in seconds) </summary>
	public const int MaxPlayTime = 359999;

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

			// Default to default data when running the game from the editor
			return MenuData;
		}
	}

	/// <summary> Game Data to use during the menu so things don't break. </summary>
	public static GameData MenuData { get; set; }
	/// <summary> Current skill ring. </summary>
	public readonly static SkillRing ActiveSkillRing = new();
	/// <summary> List of all saves created. </summary>
	public readonly static GameData[] GameSaveSlots = new GameData[SaveSlotCount];
	/// <summary> Maximum number of save slots that can be created. </summary>
	public const int SaveSlotCount = 9;

	/// <summary> Maximum number of preset slots
	public const int PresetCount = 9;
	/// <summary> Saves active game data to a file. </summary>
	public static void SaveGameData()
	{
		SaveSharedData();
		if (ActiveSaveSlotIndex == -1) return; // Invalid save slot

		// Write save data to a file.
		string saveFile = ActiveSaveSlotIndex.ToString("00");
		saveFile = SaveDirectory.PathJoin($"save{saveFile}.dat");
		FileAccess file = FileAccess.Open(saveFile, FileAccess.ModeFlags.Write);

		if (FileAccess.GetOpenError() == Error.Ok)
		{
			file.StoreString(Json.Stringify(ActiveGameData.ToDictionary(), "\t"));
			file.Close();
		}
	}

	/// <summary> Preloads game data so it can be displayed on menus. </summary>
	public static void LoadGameData()
	{
		LoadSharedData();

		for (int i = 0; i < GameSaveSlots.Length; i++)
		{
			GameSaveSlots[i] = GameData.CreateDefaultData();

			string saveFile = i.ToString("00");
			saveFile = SaveDirectory.PathJoin($"save{saveFile}.dat");
			FileAccess file = FileAccess.Open(saveFile, FileAccess.ModeFlags.Read);
			if (FileAccess.GetOpenError() == Error.Ok)
			{
				GameSaveSlots[i].FromDictionary((Dictionary)Json.ParseString(file.GetAsText()));
				file.Close();
			}

			if (GameSaveSlots[i].presetNames == null &&
				GameSaveSlots[i].presetSkills == null &&
				GameSaveSlots[i].presetSkillAugments == null)
			{
				for (int j = 0; j < PresetCount; j++)
				{
					GameSaveSlots[i].presetNames.Add(null);
					GameSaveSlots[i].presetSkills.Add(null);
					GameSaveSlots[i].presetSkillAugments.Add(null);
				}
			}
		}
	}

	/// <summary> Frees game data at the given index
	public static void ResetSaveData(int index, bool asEmptyFile)
	{
		GameSaveSlots[index] = GameData.CreateDefaultData();

		if (!asEmptyFile) // Set level to be 1 so files aren't read as empty
			GameSaveSlots[index].level = 1;
	}

	/// <summary> Deletes a save file at the given index
	public static void DeleteSaveData(int index)
	{
		string saveFile = index.ToString("00");
		saveFile = SaveDirectory.PathJoin($"save{saveFile}.dat");

		if (!FileAccess.FileExists(saveFile))
			return;

		OS.MoveToTrash(ProjectSettings.GlobalizePath(saveFile));
	}

	public class GameData
	{
		/// <summary> Which area was the player in last? (Used for save select) </summary>
		public WorldEnum lastPlayedWorld;

		/// <summary> List of world rings collected. </summary>
		public Array<WorldEnum> worldRingsCollected;
		/// <summary> List of worlds unlocked. </summary>
		public Array<WorldEnum> worldsUnlocked;
		/// <summary> List of stages unlocked. </summary>
		public Array<string> stagesUnlocked;
		/// <summary> List of cutscenes that can be skipped. </summary>
		public Array<string> skippableCutscenes;

		/// <summary> Player level, from 1 -> 99 </summary>
		public int level;
		/// <summary> The player's level must be at least one, so a file with level zero is treated as empty. </summary>
		public bool IsNewFile() => level == 0;

		/// <summary> How much exp the player currently has. </summary>
		public int exp;
		/// <summary> Total playtime, in seconds. </summary>
		public float playTime;

		public Array<string> presetNames;
		public Array<Array<SkillKey>> presetSkills;
		public Array<Dictionary<SkillKey, int>> presetSkillAugments;

		public Array<SkillKey> equippedSkills;
		public Dictionary<SkillKey, int> equippedAugments;
		public LevelSaveData LevelData => levelData;
		private LevelSaveData levelData = new();

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

		public void UnlockWorldRing(WorldEnum world)
		{
			if (worldRingsCollected.Contains(world))
				return;

			worldRingsCollected.Add(world);
		}

		public void UnlockAllWorlds()
		{
			for (int i = 0; i < (int)WorldEnum.Max; i++)
				UnlockWorld((WorldEnum)i);
		}

		public bool CanSkipCutscene(StringName cutsceneId) => skippableCutscenes.Contains(cutsceneId) || OS.IsDebugBuild();
		public void AllowSkippingCutscene(StringName cutsceneId)
		{
			if (!skippableCutscenes.Contains(cutsceneId))
				skippableCutscenes.Add(cutsceneId);
		}

		/// <summary> Creates a dictionary based on GameData. </summary>
		public Dictionary ToDictionary()
		{
			Array<Array<string>> presetDictionary = [];
			presetDictionary.Resize(presetSkills.Count);
			for (int i = 0; i < presetDictionary.Count; i++)
				presetDictionary[i] = SaveSkills(presetSkills[i]);

			Array<Dictionary<string, int>> augmentDictionary = [];
			augmentDictionary.Resize(presetSkillAugments.Count);
			for (int i = 0; i < augmentDictionary.Count; i++)
				augmentDictionary[i] = SaveAugments(presetSkillAugments[i]);

			return new()
			{
				// WorldEnum data
				{ nameof(lastPlayedWorld), (int)lastPlayedWorld },
				{ nameof(worldsUnlocked), worldsUnlocked },
				{ nameof(worldRingsCollected), worldRingsCollected },
				{ nameof(stagesUnlocked), stagesUnlocked },
				{ nameof(skippableCutscenes), skippableCutscenes },
				{ nameof(levelData), levelData.ToDictionary() },

				// Player stats
				{ nameof(level), level },
				{ nameof(exp), exp },
				{ nameof(playTime), Mathf.RoundToInt(playTime) },
				{ nameof(equippedSkills), SaveSkills(equippedSkills) },
				{ nameof(equippedAugments), SaveAugments(equippedAugments) },
				{ nameof(presetNames), presetNames},
				{ nameof(presetSkills), presetDictionary},
				{ nameof(presetSkillAugments), augmentDictionary},
			};
		}

		/// <summary> Sets GameData based on dictionary. </summary>
		public void FromDictionary(Dictionary dictionary)
		{
			// WorldEnum data
			if (dictionary.TryGetValue(nameof(lastPlayedWorld), out Variant var))
				lastPlayedWorld = (WorldEnum)(int)var;

			worldsUnlocked.Clear();
			if (dictionary.TryGetValue(nameof(worldsUnlocked), out var) && var.VariantType == Variant.Type.Array)
			{
				Array<int> worlds = (Array<int>)var;
				for (int i = 0; i < worlds.Count; i++)
					worldsUnlocked.Add((WorldEnum)worlds[i]);
			}
			else
			{
				// Update save data from old format (unlock everything to prevent softlocks)
				for (int i = 0; i < (int)WorldEnum.Max; i++)
					worldsUnlocked.Add((WorldEnum)i);
			}

			worldRingsCollected.Clear();
			if (dictionary.TryGetValue(nameof(worldRingsCollected), out var) && var.VariantType == Variant.Type.Array)
			{
				Array<int> worlds = (Array<int>)var;
				for (int i = 0; i < worlds.Count; i++)
					worldRingsCollected.Add((WorldEnum)worlds[i]);
			}

			if (dictionary.TryGetValue(nameof(stagesUnlocked), out var) && var.VariantType == Variant.Type.Array)
				stagesUnlocked = (Array<string>)var;

			if (dictionary.TryGetValue(nameof(skippableCutscenes), out var) && var.VariantType == Variant.Type.Array)
				skippableCutscenes = (Array<string>)var;

			if (dictionary.TryGetValue(nameof(levelData), out var))
				levelData.FromDictionary((Dictionary<StringName, Dictionary>)var);

			if (dictionary.TryGetValue(nameof(level), out var))
				level = (int)var;
			if (dictionary.TryGetValue(nameof(exp), out var))
				exp = (int)var;
			if (dictionary.TryGetValue(nameof(playTime), out var))
				playTime = (float)var;

			// Load Skill Ring
			if (dictionary.TryGetValue(nameof(equippedSkills), out var))
				equippedSkills = LoadSkills((Array<string>)var);

			if (dictionary.TryGetValue(nameof(equippedAugments), out var))
				equippedAugments = LoadAugments((Dictionary<string, int>)var);

			// Load Presets
			if (dictionary.TryGetValue(nameof(presetNames), out var))
				presetNames = (Array<string>)var;

			if (dictionary.TryGetValue(nameof(presetSkills), out var))
			{
				Array<Array<string>> presets = (Array<Array<string>>)var;
				presetSkills.Clear();
				presetSkills.Resize(presets.Count);
				for (int i = 0; i < presetSkills.Count; i++)
					presetSkills[i] = LoadSkills(presets[i]);
			}

			if (dictionary.TryGetValue(nameof(presetSkillAugments), out var))
			{
				Array<Dictionary<string, int>> presetAugments = (Array<Dictionary<string, int>>)var;
				presetSkillAugments.Clear();
				presetSkillAugments.Resize(presetAugments.Count);
				for (int i = 0; i < presetSkillAugments.Count; i++)
					presetSkillAugments[i] = LoadAugments(presetAugments[i]);
			}
		}

		/// <summary> Converts an array of SkillKeys to an array of strings for index-agnostic saving. </summary>
		private Array<string> SaveSkills(Array<SkillKey> skillArray)
		{
			Array<string> stringArray = [];

			for (int i = 0; i < skillArray.Count; i++)
			{
				SkillKey key = skillArray[i];
				stringArray.Add(key.ToString());
			}

			return stringArray;
		}

		private Dictionary<string, int> SaveAugments(Dictionary<SkillKey, int> augmentDictionary)
		{
			Dictionary<string, int> stringDictionary = [];

			for (int i = 0; i < augmentDictionary.Keys.Count; i++)
			{
				SkillKey key = augmentDictionary.Keys.ToArray()[i];
				stringDictionary.Add(key.ToString(), augmentDictionary[key]);
			}

			return stringDictionary;
		}

		private Array<SkillKey> LoadSkills(Array<string> stringArray)
		{
			Array<SkillKey> skills = [];

			for (int i = 0; i < stringArray.Count; i++)
			{
				if (Enum.TryParse(stringArray[i], out SkillKey key))
					skills.Add(key);
			}

			return skills;
		}

		private Dictionary<SkillKey, int> LoadAugments(Dictionary<string, int> stringDictionary)
		{
			string[] augmentKeys = [.. stringDictionary.Keys];
			Dictionary<SkillKey, int> augmentDictionary = [];

			for (int i = 0; i < augmentKeys.Length; i++)
			{
				if (Enum.TryParse(augmentKeys[i], out SkillKey key))
					augmentDictionary.Add(key, stringDictionary[augmentKeys[i]]);
			}
			return augmentDictionary;
		}

		/// <summary> Creates a new GameData object that contains default values. </summary>
		public static GameData CreateDefaultData()
		{
			// Set up default game data/menu game data
			GameData data = new()
			{
				worldRingsCollected = [],
				worldsUnlocked = [],
				stagesUnlocked = [],
				skippableCutscenes = [],
				presetNames = [],
				presetSkills = [],
				presetSkillAugments = [],
				equippedSkills = [],
				equippedAugments = [],
				level = 0,
				lastPlayedWorld = WorldEnum.LostPrologue,
				levelData = new()
			};

			// TODO Replace this with the tutorial key
			data.UnlockStage("so_a1_main");
			data.UnlockWorld(WorldEnum.LostPrologue);
			data.UnlockWorld(WorldEnum.SandOasis); // Lock this in the final build

			for (int i = 0; i < PresetCount; i++)
			{
				data.presetNames.Add(string.Empty);
				data.presetSkills.Add([]);
				data.presetSkillAugments.Add([]);
			}

			return data;
		}
	}
	#endregion

	#region Shared Game Data
	public static SharedGameData SharedData;
	private const string SharedFileName = "shared.sav";

	public class SharedGameData
	{
		/// <summary> Total amount of time the game has been on, in seconds. </summary>
		public float PlayTime { get; set; }
		/// <summary> Total amount of distance ran. </summary>
		public float RunDistance { get; set; }
		/// <summary> Total amount of distance grinded. </summary>
		public float GrindDistance { get; set; }
		/// <summary> Total number of enemies busted. </summary>
		public int EnemyCount { get; set; }
		/// <summary> Total number of rings collected. </summary>
		public int RingCount { get; set; }
		/// <summary> Total number of rings collected. </summary>
		public int RingChainCount { get; set; }
		/// <summary> Total number of times SpeedBreak was activated. </summary>
		public int SpeedBreakActivationCount { get; set; }
		/// <summary> Total number of seconds TimeBreak was active. </summary>
		public float TimeBreakTime { get; set; }

		// Skills
		public int MinimalSkillCount { get; set; }
		public int FireOnlyCount { get; set; }
		public int WindOnlyCount { get; set; }
		public int DarkOnlyCount { get; set; }

		/// <summary> Dictionaries for each individual level's data. </summary>
		public LevelSaveData LevelData => levelData;
		private LevelSaveData levelData = new();
		/// <summary> List of big cameos unlocked. </summary>
		public Array<string> bigCameos = [];
		/// <summary> List of achievements unlocked. </summary>
		public Array<string> achievements = [];

		/// <summary> Creates a dictionary based on GameData. </summary>
		public Dictionary ToDictionary()
		{
			return new()
			{
				{ nameof(PlayTime), Mathf.RoundToInt(PlayTime) },
				{ nameof(RunDistance), RunDistance },
				{ nameof(GrindDistance), GrindDistance },
				{ nameof(EnemyCount), EnemyCount },
				{ nameof(RingCount), RingCount },
				{ nameof(RingChainCount), RingChainCount },
				{ nameof(SpeedBreakActivationCount), SpeedBreakActivationCount },
				{ nameof(TimeBreakTime), TimeBreakTime },

				{ nameof(MinimalSkillCount), MinimalSkillCount },
				{ nameof(FireOnlyCount), FireOnlyCount },
				{ nameof(WindOnlyCount), WindOnlyCount },
				{ nameof(DarkOnlyCount), DarkOnlyCount },

				{ nameof(LevelData), LevelData.ToDictionary() },
				{ nameof(bigCameos), bigCameos },
				{ nameof(achievements), achievements }
			};
		}

		/// <summary> Sets GameData based on dictionary. </summary>
		public void FromDictionary(Dictionary dictionary)
		{
			if (dictionary.TryGetValue(nameof(PlayTime), out Variant var))
				PlayTime = (float)var;
			if (dictionary.TryGetValue(nameof(RunDistance), out var))
				RunDistance = (float)var;
			if (dictionary.TryGetValue(nameof(GrindDistance), out var))
				GrindDistance = (float)var;
			if (dictionary.TryGetValue(nameof(EnemyCount), out var))
				EnemyCount = (int)var;
			if (dictionary.TryGetValue(nameof(RingCount), out var))
				RingCount = (int)var;
			if (dictionary.TryGetValue(nameof(RingChainCount), out var))
				RingChainCount = (int)var;
			if (dictionary.TryGetValue(nameof(SpeedBreakActivationCount), out var))
				SpeedBreakActivationCount = (int)var;
			if (dictionary.TryGetValue(nameof(TimeBreakTime), out var))
				TimeBreakTime = (float)var;

			if (dictionary.TryGetValue(nameof(MinimalSkillCount), out var))
				MinimalSkillCount = (int)var;
			if (dictionary.TryGetValue(nameof(FireOnlyCount), out var))
				FireOnlyCount = (int)var;
			if (dictionary.TryGetValue(nameof(WindOnlyCount), out var))
				WindOnlyCount = (int)var;
			if (dictionary.TryGetValue(nameof(DarkOnlyCount), out var))
				DarkOnlyCount = (int)var;

			if (dictionary.TryGetValue(nameof(LevelData), out var))
				LevelData.FromDictionary((Dictionary<StringName, Dictionary>)var);
			if (dictionary.TryGetValue(nameof(bigCameos), out var))
				bigCameos = (Array<string>)var;
			if (dictionary.TryGetValue(nameof(achievements), out var))
				achievements = (Array<string>)var;


			if (dictionary.TryGetValue(nameof(DarkOnlyCount), out var))
				DarkOnlyCount = (int)var;
		}

		public static SharedGameData CreateDefaultData()
		{
			SharedGameData data = new();
			// Prevent shared LevelData from infinitely setting values 
			data.LevelData.IsSharedLevelSaveData = true;
			return data;
		}
	}

	/// <summary> Attempts to load config data from file. </summary>
	public static void LoadSharedData()
	{
		string dataFile = SaveDirectory.PathJoin(SharedFileName);
		FileAccess file = FileAccess.Open(dataFile, FileAccess.ModeFlags.Read);

		try
		{
			if (file.GetError() == Error.Ok)
			{
				// Attempt to load.
				Dictionary d = (Dictionary)Json.ParseString(file.GetAsText());
				SharedData.FromDictionary(d);
				file.Close();
			}
		}
		catch // Load Default settings
		{
			SharedData = SharedGameData.CreateDefaultData();
		}
	}

	/// <summary> Attempts to save shared data to file. </summary>
	public static void SaveSharedData()
	{
		if (!DirAccess.DirExistsAbsolute(SaveDirectory))
			DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);

		string dataFile = SaveDirectory.PathJoin(SharedFileName);
		FileAccess file = FileAccess.Open(dataFile, FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(SharedData.ToDictionary(), "\t"));
		file.Close();

		file = FileAccess.Open(SaveLocationFile, FileAccess.ModeFlags.Write);
		file.StoreString(SaveDirectory);
		file.Close();
	}
	#endregion

	public class LevelSaveData
	{
		/// <summary> Dictionaries for each individual level's data. </summary>
		private Dictionary<StringName, Dictionary> data = [];

		/// <summary> Should this LevelSaveData update SharedData.LevelSaveData? </summary>
		public bool IsSharedLevelSaveData { get; set; }

		/// <summary> Total number of fire souls the player collected. </summary>
		public int FireSoulCount { get; private set; }
		/// <summary> Total number of gold medals the player has collected. </summary>
		public int GoldMedalCount { get; private set; }
		/// <summary> Total number of silver medals the player has collected. </summary>
		public int SilverMedalCount { get; private set; }
		/// <summary> Total number of bronze medals the player has collected. </summary>
		public int BronzeMedalCount { get; private set; }

		private void UpdateMedals(int rank, int oldRank = 0)
		{
			if (rank >= 3 && oldRank < 3)
				GoldMedalCount++;
			if (rank >= 2 && oldRank < 2)
				SilverMedalCount++;
			if (rank >= 1 && oldRank < 1)
				BronzeMedalCount++;
		}

		private void IncrementFireSoulCounter()
		{
			FireSoulCount++;
			// TODO Check soul collector achievement
		}

		private readonly string FireSoulKey = "fire_soul";
		/// <summary> Returns whether a particular fire soul has been collected or not. </summary>
		public bool IsFireSoulCollected(StringName levelID, int index)
		{
			StringName key = FireSoulKey + index.ToString();
			if (GetLevelData(levelID).TryGetValue(key, out Variant collected))
				return (bool)collected;

			return false;
		}

		/// <summary> Sets the save value for whether a particular fire soul is collected or not. </summary>
		public void SetFireSoulCollected(StringName levelID, int index)
		{
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetFireSoulCollected(levelID, index);

			StringName key = FireSoulKey + index.ToString();
			if (GetLevelData(levelID).ContainsKey(key))
			{
				GetLevelData(levelID)[key] = true;
				return;
			}

			IncrementFireSoulCounter();
			GetLevelData(levelID).Add(key, true);
		}

		private readonly string RankKey = "rank";
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
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetRank(levelID, rank);

			// Discard lower ranks
			if (rank <= GetRank(levelID)) return;

			if (GetLevelData(levelID).ContainsKey(RankKey))
			{
				UpdateMedals(rank, (int)GetLevelData(levelID)[RankKey]);
				GetLevelData(levelID)[RankKey] = rank;
				return;
			}

			UpdateMedals(rank);
			GetLevelData(levelID).Add(RankKey, rank);
		}

		private readonly string SkillessGoldKey = "skilless_gold";
		public bool GetSkillessGold(StringName levelId)
		{
			if (GetLevelData(levelId).TryGetValue(SkillessGoldKey, out Variant passed))
				return (bool)passed;

			return false;
		}

		public void SetSkillessGold(StringName levelId, bool passed)
		{
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetSkillessGold(levelId, passed);

			if (GetSkillessGold(levelId))
				return;

			if (GetLevelData(levelId).ContainsKey(SkillessGoldKey))
				GetLevelData(levelId)[SkillessGoldKey] = true;

			GetLevelData(levelId).Add(SkillessGoldKey, passed);
		}

		private readonly string ScoreKey = "high_score";
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
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetHighScore(levelID, score);

			// Discard lower scores
			if (score <= GetHighScore(levelID)) return;

			if (GetLevelData(levelID).ContainsKey(ScoreKey))
			{
				GetLevelData(levelID)[ScoreKey] = score;
				return;
			}

			GetLevelData(levelID).Add(ScoreKey, score);
		}

		private readonly string TimeKey = "best_time";
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
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetBestTime(levelID, time);

			// Discard lower scores
			if (!Mathf.IsZeroApprox(GetBestTime(levelID)) &&
				time > GetBestTime(levelID))
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

		private readonly string StatusKey = "clear_status";
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
			if (!IsSharedLevelSaveData)
				SharedData.LevelData.SetClearStatus(levelID, clearStatus);

			// Return early if the level has already been cleared
			if (GetClearStatus(levelID) == LevelStatus.Cleared)
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
			if (!data.ContainsKey(levelID)) // Create new level data if it's missing
				data.Add(levelID, []);

			return data[levelID];
		}

		public Dictionary ToDictionary() => (Dictionary)data;

		public void FromDictionary(Dictionary<StringName, Dictionary> newData)
		{
			data = newData;

			// Reset counters before re-counting
			GoldMedalCount = 0;
			SilverMedalCount = 0;
			BronzeMedalCount = 0;
			FireSoulCount = 0;

			// Update runtime data based on save data
			StringName[] keys = data.Keys.ToArray();
			for (int i = 0; i < keys.Length; i++)
			{
				UpdateMedals(GetRank(keys[i]));

				for (int j = 1; j < 4; j++) // Check fire souls
				{
					if (!IsFireSoulCollected(keys[i], j))
						continue;

					IncrementFireSoulCounter();
				}
			}

			if (IsSharedLevelSaveData)
				return;

			// Retro-actively update SharedData as needed
			for (int i = 0; i < keys.Length; i++)
			{
				SharedData.LevelData.SetRank(keys[i], GetRank(keys[i]));
				SharedData.LevelData.SetClearStatus(keys[i], GetClearStatus(keys[i]));
				SharedData.LevelData.SetBestTime(keys[i], GetBestTime(keys[i]));
				SharedData.LevelData.SetHighScore(keys[i], GetHighScore(keys[i]));

				for (int j = 1; j < 4; j++) // Check fire souls
				{
					if (!SharedData.LevelData.IsFireSoulCollected(keys[i], j))
						SharedData.LevelData.SetFireSoulCollected(keys[i], j);
				}
			}
		}
	}
}