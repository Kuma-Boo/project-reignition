using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class Options : Menu
	{
		[Export]
		public ShaderMaterial menuOverlay;
		[Export]
		public SubViewport menuViewport;
		[Export]
		public Control cursor;

		private Callable ApplyTextureCallable => new(this, MethodName.ApplyTexture);
		public static readonly StringName MENU_PARAMETER = "menu_texture";

		private Submenus currentSubmenu = Submenus.Options;
		private enum Submenus
		{
			Options, // Main menu
			Video, // Menu for configuring video settings
			Audio,  // Menu for configuring audio volume
			Language, // Menu for localization and language
			Control, // Menu for configuring general control settings
			Mapping, // Menu for configuring input mappings
			Test // Menu for testing controls
		}
		private bool IsMovementPage => VerticalSelection <= 3;

		protected override void SetUp()
		{
			bgm.Play();

			SetUpControlOptions();
			UpdateLabels();
			if (!RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}

		public override void _ExitTree()
		{
			if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable))
				RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, ApplyTextureCallable);
		}


		public void ApplyTexture() => menuOverlay.SetShaderParameter(MENU_PARAMETER, menuViewport.GetTexture());


		protected override void ProcessMenu()
		{
			UpdateCursor();
			if (Input.IsActionJustPressed("button_pause"))
				Select();
			else
				base.ProcessMenu();
		}


		protected override void Confirm()
		{
			switch (currentSubmenu)
			{
				case Submenus.Options:
					currentSubmenu = (Submenus)VerticalSelection + 1;
					animator.Play("flip-left");
					VerticalSelection = 0;
					break;
				case Submenus.Video:
					SlideVideoOption(1);
					break;
				case Submenus.Audio:
					ConfirmAudioOption();
					break;
				case Submenus.Language:
					SlideLanguageOption(1);
					break;
				case Submenus.Control:
					ConfirmControlOption();
					break;
				case Submenus.Mapping:
					if (controlMappingOptions[VerticalSelection].IsListeningForInputs) // Listening for inputs
						return;

					controlMappingOptions[VerticalSelection].CallDeferred(ControlOption.MethodName.StartListening);
					break;
				case Submenus.Test:
					return;
			}

			UpdateLabels();
			SaveManager.Instance.ApplyConfig();
		}


		protected override void Cancel()
		{
			switch (currentSubmenu)
			{
				case Submenus.Options:
					DisableProcessing();
					FadeBGM(.5f);
					SaveManager.Instance.SaveConfig();
					menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
					TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(SaveManager.Instance, SaveManager.MethodName.SaveConfig), (uint)ConnectFlags.OneShot);
					TransitionManager.QueueSceneChange(TransitionManager.MENU_SCENE_PATH);
					TransitionManager.StartTransition(new()
					{
						color = Colors.Black,
						inSpeed = .5f,
					});
					break;
				case Submenus.Mapping:
					if (controlMappingOptions[VerticalSelection].IsListeningForInputs) return;

					VerticalSelection = 1;
					currentSubmenu = Submenus.Control;
					animator.Play("flip-right");
					break;
				case Submenus.Test:
					return;
				default:
					VerticalSelection = (int)currentSubmenu - 1;
					currentSubmenu = Submenus.Options;
					animator.Play("flip-right");
					break;
			}
		}


		private void Select()
		{
			if (currentSubmenu == Submenus.Test) // Cancel test
			{
				animator.Play("test_end");
				currentSubmenu = Submenus.Control;
			}
			else
				Confirm();
		}


		protected override void UpdateSelection()
		{
			if (Mathf.IsZeroApprox(Input.GetAxis("move_up", "move_down")))
			{
				UpdateHorizontalSelection();
				return;
			}

			StartSelectionTimer();

			int targetSelection = VerticalSelection + Mathf.Sign(Input.GetAxis("move_up", "move_down"));
			switch (currentSubmenu)
			{
				case Submenus.Options:
					VerticalSelection = WrapSelection(targetSelection, 4);
					break;
				case Submenus.Video:
					VerticalSelection = WrapSelection(targetSelection, 2);
					break;
				case Submenus.Audio:
					VerticalSelection = WrapSelection(targetSelection, 4);
					break;
				case Submenus.Language:
					VerticalSelection = WrapSelection(targetSelection, 3);
					break;
				case Submenus.Control:
					VerticalSelection = WrapSelection(targetSelection, 3);
					break;
				case Submenus.Mapping:
					if (controlMappingOptions[VerticalSelection].IsListeningForInputs) // Listening for inputs
						return;

					bool movementPage = IsMovementPage;
					VerticalSelection = WrapSelection(targetSelection, controlMappingOptions.Length);
					if (IsMovementPage != movementPage)
					{
						animator.Play(IsMovementPage ? "flip-right" : "flip-left");
						return;
					}

					break;
				case Submenus.Test:
					return;
			}

			animator.Play("select");
			animator.Seek(0, true);
		}


		private void UpdateCursor()
		{
			int offset = VerticalSelection;
			if (currentSubmenu == Submenus.Mapping && !IsMovementPage)
				offset -= 4;

			cursor.Position = new(0, 300 + offset * 96);
		}


		/// <summary> Changes the visible submenu. Called from the page flip animation. </summary>
		private void UpdateSubmenuVisibility()
		{
			UpdateCursor();
			animator.Play(currentSubmenu.ToString().ToLower());
			animator.Advance(0.0);

			if (currentSubmenu == Submenus.Mapping)
				animator.Play(IsMovementPage ? "mapping_movement" : "mapping_action");
		}


		[Export]
		private Label[] videoLabels;
		[Export]
		private Label[] audioLabels;
		[Export]
		private Label[] languageLabels;
		[Export]
		private Label[] controlLabels;
		private string ON_STRING = "option_on";
		private string OFF_STRING = "option_off";
		private string MUTE_STRING = "option_mute";
		private void UpdateLabels()
		{
			Vector2I resolution = SaveManager.SCREEN_RESOLUTIONS[SaveManager.Config.screenResolution];
			videoLabels[0].Text = SaveManager.Config.useVsync ? ON_STRING : OFF_STRING;
			videoLabels[1].Text = $"{resolution.X}:{resolution.Y}";

			audioLabels[0].Text = SaveManager.Config.isMasterMuted ? MUTE_STRING : $"{SaveManager.Config.masterVolume}%";
			audioLabels[1].Text = SaveManager.Config.isBgmMuted ? MUTE_STRING : $"{SaveManager.Config.bgmVolume}%";
			audioLabels[2].Text = SaveManager.Config.isSfxMuted ? MUTE_STRING : $"{SaveManager.Config.sfxVolume}%";
			audioLabels[3].Text = SaveManager.Config.isVoiceMuted ? MUTE_STRING : $"{SaveManager.Config.voiceVolume}%";

			languageLabels[0].Text = SaveManager.Config.subtitlesEnabled ? ON_STRING : OFF_STRING;
			languageLabels[2].Text = SaveManager.Config.voiceLanguage == SaveManager.VoiceLanguage.English ? "lang_en" : "lang_ja";

			switch (SaveManager.Config.controllerType)
			{
				case SaveManager.ControllerType.PlayStation:
					controlLabels[0].Text = "option_controller_ps";
					break;
				case SaveManager.ControllerType.Xbox:
					controlLabels[0].Text = "option_controller_xbox";
					break;
			}
		}


		[Export]
		private ControlOption[] controlMappingOptions;
		private void SetUpControlOptions()
		{
			foreach (ControlOption controlOption in controlMappingOptions)
				controlOption.Connect(ControlOption.SignalName.SwapMapping, new(this, MethodName.RedrawControlOptions));
		}


		private void RedrawControlOptions(StringName id, InputEvent e)
		{
			foreach (ControlOption controlOption in controlMappingOptions)
			{
				if (controlOption.inputID == id)
					controlOption.ReceiveInput(e, true);
			}
		}


		private void UpdateHorizontalSelection()
		{
			if (Mathf.IsZeroApprox(Input.GetAxis("move_left", "move_right"))) return;

			int direction = Mathf.Sign(Input.GetAxis("move_left", "move_right"));
			bool settingUpdated = false;

			switch (currentSubmenu)
			{
				case Submenus.Video:
					settingUpdated = SlideVideoOption(direction);
					break;
				case Submenus.Audio:
					settingUpdated = SlideAudioOption(direction);
					break;
				case Submenus.Language:
					settingUpdated = SlideLanguageOption(direction);
					break;
				case Submenus.Control:
					settingUpdated = SlideControlOption(direction);
					break;
			}

			if (!settingUpdated) return;

			StartSelectionTimer();
			UpdateLabels();
			SaveManager.Instance.ApplyConfig();
		}


		private bool SlideVideoOption(int direction)
		{
			if (VerticalSelection == 0)
				SaveManager.Config.useVsync = !SaveManager.Config.useVsync;
			else
			{
				SaveManager.Config.screenResolution += direction;
				if (SaveManager.Config.screenResolution < 0)
					SaveManager.Config.screenResolution = SaveManager.SCREEN_RESOLUTIONS.Length - 1;
				else if (SaveManager.Config.screenResolution >= SaveManager.SCREEN_RESOLUTIONS.Length)
					SaveManager.Config.screenResolution = 0;
			}

			return true;
		}


		private bool SlideAudioOption(int direction)
		{
			if (VerticalSelection == 0)
			{
				if (!IsSlideVolumeValid(SaveManager.Config.masterVolume, direction))
					return false;

				SaveManager.Config.isMasterMuted = false;
				SaveManager.Config.masterVolume = SlideVolume(SaveManager.Config.masterVolume, direction);
			}
			else if (VerticalSelection == 1)
			{
				if (!IsSlideVolumeValid(SaveManager.Config.bgmVolume, direction))
					return false;

				SaveManager.Config.isBgmMuted = false;
				SaveManager.Config.bgmVolume = SlideVolume(SaveManager.Config.bgmVolume, direction);
			}
			else if (VerticalSelection == 2)
			{
				if (!IsSlideVolumeValid(SaveManager.Config.sfxVolume, direction))
					return false;

				SaveManager.Config.isSfxMuted = false;
				SaveManager.Config.sfxVolume = SlideVolume(SaveManager.Config.sfxVolume, direction);
			}
			else if (VerticalSelection == 3)
			{
				if (!IsSlideVolumeValid(SaveManager.Config.voiceVolume, direction))
					return false;

				SaveManager.Config.isVoiceMuted = false;
				SaveManager.Config.voiceVolume = SlideVolume(SaveManager.Config.voiceVolume, direction);
			}

			return true;
		}

		private int SlideVolume(int current, int direction) => Mathf.Clamp(current + direction * 5, 0, 100);
		private bool IsSlideVolumeValid(int current, int direction) => (current > 0 && direction == -1) || (current < 100 && direction == 1);


		private bool SlideLanguageOption(int direction)
		{
			if (VerticalSelection == 0)
			{
				SaveManager.Config.subtitlesEnabled = !SaveManager.Config.subtitlesEnabled;
				return true;
			}

			if (VerticalSelection == 1)
			{
				int lang = WrapSelection((int)SaveManager.Config.textLanguage + direction, (int)SaveManager.TextLanguage.Count);
				SaveManager.Config.textLanguage = (SaveManager.TextLanguage)lang;
				return true;
			}

			if (VerticalSelection == 2)
			{
				int lang = WrapSelection((int)SaveManager.Config.voiceLanguage + direction, (int)SaveManager.VoiceLanguage.Count);
				SaveManager.Config.voiceLanguage = (SaveManager.VoiceLanguage)lang;
				return true;
			}

			return false;
		}


		private bool SlideControlOption(int direction)
		{
			if (VerticalSelection == 0)
			{
				int type = WrapSelection((int)SaveManager.Config.controllerType + direction, (int)SaveManager.ControllerType.Count);
				SaveManager.Config.controllerType = (SaveManager.ControllerType)type;
				return true;
			}

			return false;
		}


		private void ConfirmAudioOption()
		{
			if (VerticalSelection == 0)
				SaveManager.Config.isMasterMuted = !SaveManager.Config.isMasterMuted;
			else if (VerticalSelection == 1)
				SaveManager.Config.isBgmMuted = !SaveManager.Config.isBgmMuted;
			else if (VerticalSelection == 2)
				SaveManager.Config.isSfxMuted = !SaveManager.Config.isSfxMuted;
			else
				SaveManager.Config.isVoiceMuted = !SaveManager.Config.isVoiceMuted;
		}


		private void ConfirmControlOption()
		{
			if (VerticalSelection == 0)
				SlideControlOption(1);
			else if (VerticalSelection == 1)
			{
				currentSubmenu = Submenus.Mapping;
				animator.Play("flip-left");
				VerticalSelection = 0;
			}
			else
			{
				currentSubmenu = Submenus.Test;
				animator.Play("flip-left");
			}
		}
	}
}
