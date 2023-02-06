using Godot;
using System;

namespace Project.Core
{
	public partial class SaveManager : Node
	{
		public static bool saveDataInitialized; //Has the initial save data check been completed?

		public override void _EnterTree()
		{
			LoadSettings();
			LoadGame();
		}

		#region Settings
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

		public static SettingsData settings;
		public static bool UseEnglishVoices => settings.voiceLanguage == VoiceLanguage.English;

		public partial class SettingsData
		{
			//Video
			public bool useVsync;
			public bool isFullscreen;
			public Vector2 screenResolution;

			//Audio
			public bool isMasterMuted;
			public float masterVolume = .5f;
			public bool isBgmMuted;
			public float bgmVolume = 1f;
			public bool isSfxMuted;
			public float sfxVolume = 1f;
			public bool isVoiceMuted;
			public float voiceVolume = 1f;

			//Language
			public bool subtitlesEnabled = true;
			public VoiceLanguage voiceLanguage;
			public TextLanguage textLanguage;
		}

		public void LoadSettings()
		{
			//Attempt to load.
			if (settings == null) //Load Default settings
			{
				settings = new SettingsData()
				{
					textLanguage = TextLanguage.English,
					voiceLanguage = VoiceLanguage.Japanese,
				};
			}

			ApplyLocalization();

			//TODO Attempt to load control configuration. for now, default controls
			InputManager.DefaultControls();

			//TODO Load and apply settings
			//OS.VsyncEnabled = settings.useVsync;
			//OS.WindowSize = settings.screenResolution;
			//OS.WindowFullscreen = settings.isFullscreen;

			/*
			SetAudioBusVolume((int)AudioBuses.MASTER, settings.masterVolume, settings.isMasterMuted);
			SetAudioBusVolume((int)AudioBuses.BGM, settings.bgmVolume, settings.isBgmMuted);
			SetAudioBusVolume((int)AudioBuses.SFX, settings.sfxVolume, settings.isSfxMuted);
			SetAudioBusVolume((int)AudioBuses.VOICE, settings.voiceVolume, settings.isVoiceMuted);
			*/

			saveDataInitialized = true;
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

		public void SaveSettings()
		{
			//Save data portably in the same folder as the application.
		}

		private void SetAudioBusVolume(int bus, float volume, bool forceMute = default)
		{
			bool isMuted = Mathf.IsZeroApprox(volume) || forceMute;
			AudioServer.SetBusMute(bus, isMuted); //Mute or unmute

			if (isMuted) return;

			float volumeDb = (Mathf.Log(volume) / Mathf.Log(10)) * 20; //Convert ratio to db
			AudioServer.SetBusVolumeDb(bus, volumeDb);
		}

		#endregion

		#region Game data
		public static GameData ActiveGameData { get; private set; }
		public class GameData
		{
			public bool IsWorldUnlocked(int worldIndex) => worldsUnlocked.IsSet(ConvertIntToWorldEnum(worldIndex));
			public bool IsWorldRingUnlocked(int worldIndex) => worldRingsCollected.IsSet(ConvertIntToWorldEnum(worldIndex));

			/// <summary>
			/// Converts worldIndex to WorldEnum. World index starts at zero.
			/// </summary>
			private WorldEnum ConvertIntToWorldEnum(int worldIndex)
			{
				int returnIndex = 1;
				for (int i = 0; i < worldIndex; i++)
					returnIndex *= 2;

				return (WorldEnum)returnIndex;
			}

			public WorldEnum worldRingsCollected;
			public WorldEnum worldsUnlocked;

			public int exp;
			public int level;
			public float playTime;

			/// <summary> Soul gauge's level, normalized from [0 -> 1] </summary>
			public float SoulGaugeLevel => Mathf.Clamp(level, 0, 50) / 50f;
			public SkillRing skillRing;
		}

		[Flags]
		public enum WorldEnum
		{
			LostPrologue = 1,
			SandOasis = 2,
			DinosaurJungle = 4,
			EvilFoundry = 8,
			LevitatedRuin = 16,
			PirateStorm = 32,
			SkeletonDome = 64,
			NightPalace = 128
		}
		public const int WORLD_COUNT = 8;

		public void SaveGame()
		{

		}

		public void LoadGame()
		{
			//Debug game data
			ActiveGameData = new GameData()
			{
				level = 0,
				skillRing = new SkillRing(),
				worldsUnlocked = WorldEnum.LostPrologue + (int)WorldEnum.SandOasis + (int)WorldEnum.DinosaurJungle + (int)WorldEnum.LevitatedRuin
			};
		}
		#endregion

		#region Skills
		public class SkillRing
		{
			public Skills equippedSkills;

			[Flags]
			public enum Skills
			{
				//Standard skills
				None = 0,
				LandingBoost = 1, //Gives a speed boost when landing
				PearlAttractor = 2, //Makes collecting pearls easier
				SplashJump = 4, //Bounces the player when JumpDashing an obstacle
			}
		}

		#endregion
	}
}