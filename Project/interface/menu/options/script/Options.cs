using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class Options : Menu
{
	[Export]
	private Control cursor;
	[Export]
	private AnimationPlayer cursorAnimator;
	[Export]
	private Control contentContainer;

	private int maxSelection;
	private int scrollOffset;
	private void CalculateMaxSelection()
	{
		// Recalculate max selection
		switch (currentSubmenu)
		{
			case Submenus.Options:
				maxSelection = 4;
				break;
			case Submenus.Video:
				maxSelection = videoLabels.Length;
				break;
			case Submenus.Audio:
				maxSelection = 4;
				break;
			case Submenus.Language:
				maxSelection = 3;
				break;
			case Submenus.Control:
				maxSelection = 4;
				break;
			case Submenus.Mapping:
				maxSelection = controlMappingOptions.Length;
				break;
			case Submenus.Test:
				maxSelection = 0;
				break;
		}
	}

	private Callable FullscreenToggleCallable => new(this, MethodName.ToggleFullscreen);
	public static readonly StringName MENU_PARAMETER = "menu_texture";

	private Submenus currentSubmenu = Submenus.Options;
	private enum Submenus
	{
		Options, // Main menu
		Video, // Menu for configuring video settings
		Audio,  // Menu for configuring audio volume
		Language, // Menu for localization and language
		Control, // Menu for configuring general control settings
		Mapping, // Control submenu for configuring input mappings
		Test // Control submenu for testing controls
	}

	protected override void SetUp()
	{
		bgm.Play();

		SetUpControlOptions();
		UpdateLabels();
		CalculateMaxSelection();
		DebugManager.Instance.Connect(DebugManager.SignalName.FullscreenToggled, FullscreenToggleCallable);
	}

	public override void _ExitTree() => DebugManager.Instance.Disconnect(DebugManager.SignalName.FullscreenToggled, FullscreenToggleCallable);

	private void ToggleFullscreen()
	{
		UpdateLabels();
		DisableProcessing();
		GetTree().CreateTimer(0.1, true, false, true).Connect(SceneTreeTimer.SignalName.Timeout, new(this, MethodName.EnableProcessing));
	}

	private void FlipBook(Submenus submenu, bool flipRight, int selection)
	{
		currentSubmenu = submenu;
		animator.Play(flipRight ? "flip-right" : "flip-left");
		animator.Seek(0.0, true);
		VerticalSelection = selection;
	}


	protected override void ProcessMenu()
	{
		UpdateScrolling();
		UpdateCursor();

		if (Input.IsActionJustPressed("button_pause"))
			Select();
		else
			base.ProcessMenu();

		if (isPlayerLocked)
			CallDeferred(MethodName.UpdatePlayerPosition);
	}

	[Export]
	private Node3D lockNode;
	private bool isPlayerLocked;
	private void LockPlayer() => isPlayerLocked = true;
	private void UnlockPlayer() => isPlayerLocked = false;
	private void UpdatePlayerPosition()
	{
		CharacterController character = CharacterController.instance;

		Vector3 lockPosition = character.GlobalPosition;
		lockPosition.X = Mathf.Clamp(lockPosition.X, lockNode.GlobalPosition.X - 1.2f, lockNode.GlobalPosition.X + 1.2f);
		lockPosition.Z = lockNode.GlobalPosition.Z;
		character.GlobalPosition = lockPosition;
	}


	protected override void Confirm()
	{
		switch (currentSubmenu)
		{
			case Submenus.Options:
				ConfirmSFX();
				FlipBook((Submenus)VerticalSelection + 1, false, 0);
				break;
			case Submenus.Video:
				ConfirmVideoOption();
				break;
			case Submenus.Audio:
				ConfirmAudioOption();
				break;
			case Submenus.Language:
				if (SlideLanguageOption(1))
					ConfirmSFX();
				break;
			case Submenus.Control:
				ConfirmControlOption();
				break;
			case Submenus.Mapping:
				if (!controlMappingOptions[VerticalSelection].IsReady)
					return;

				ConfirmSFX();
				controlMappingOptions[VerticalSelection].CallDeferred(ControlOption.MethodName.StartListening);
				break;
			case Submenus.Test:
				return;
		}

		UpdateLabels();
		SaveManager.ApplyConfig();
	}


	protected override void Cancel()
	{
		switch (currentSubmenu)
		{
			case Submenus.Options:
				CancelSFX();
				DisableProcessing();
				FadeBGM(.5f);
				SaveManager.SaveConfig();
				menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
				TransitionManager.QueueSceneChange(TransitionManager.MENU_SCENE_PATH);
				TransitionManager.StartTransition(new()
				{
					color = Colors.Black,
					inSpeed = .5f,
				});
				break;
			case Submenus.Mapping:
				if (!controlMappingOptions[VerticalSelection].IsReady) return;

				CancelSFX();
				FlipBook(Submenus.Control, true, 2);
				break;
			case Submenus.Test:
				return;
			default:
				CancelSFX();
				FlipBook(Submenus.Options, true, (int)currentSubmenu - 1);
				break;
		}
	}


	private void Select()
	{
		if (currentSubmenu == Submenus.Test) // Cancel test
		{
			UnlockPlayer();
			animator.Play("test_end");
			currentSubmenu = Submenus.Control;
			CharacterController.instance.Skills.DisableBreakSkills();
		}
		else
		{
			Confirm();
		}
	}


	protected override void UpdateSelection()
	{
		if (currentSubmenu == Submenus.Test)
			return;

		if (currentSubmenu == Submenus.Mapping && !controlMappingOptions[VerticalSelection].IsReady) // Listening for inputs
			return;

		if (Mathf.IsZeroApprox(Input.GetAxis("move_up", "move_down")))
		{
			UpdateHorizontalSelection();
			return;
		}

		StartSelectionTimer();
		int targetSelection = VerticalSelection + Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		VerticalSelection = WrapSelection(targetSelection, maxSelection);

		animator.Play("select");
		animator.Seek(0, true);
		cursorAnimator.Play("show");
		cursorAnimator.AnimationSetNext("show", "loop");
		cursorAnimator.Seek(0, true);
	}


	[Export]
	private Control scrollBar;
	private Vector2 scrollBarVelocity;
	private const float scrollBarSmoothing = .2f;
	private void UpdateScrolling(bool snap = false)
	{
		// Reset scroll
		if (maxSelection < 8)
			scrollOffset = 0;

		if (!scrollBar.IsVisibleInTree()) return;

		if (VerticalSelection > scrollOffset + 7)
			scrollOffset = VerticalSelection - 7;
		else if (VerticalSelection < scrollOffset)
			scrollOffset = VerticalSelection;

		float scrollRatio = (float)VerticalSelection / (maxSelection - 1);
		Vector2 targetPosition = Vector2.Down * 880 * scrollRatio;

		if (snap)
		{
			scrollBarVelocity = Vector2.Zero;
			scrollBar.Position = targetPosition;
		}
		else
			scrollBar.Position = ExtensionMethods.SmoothDamp(scrollBar.Position, targetPosition, ref scrollBarVelocity, scrollBarSmoothing);
	}


	private void UpdateCursor()
	{
		int offset = VerticalSelection - scrollOffset;
		contentContainer.Position = Vector2.Up * scrollOffset * 60;
		cursor.Position = new(cursor.Position.X, 300 + offset * 60);
	}


	/// <summary> Changes the visible submenu. Called from the page flip animation. </summary>
	private void UpdateSubmenuVisibility()
	{
		CalculateMaxSelection();
		animator.Play(currentSubmenu.ToString().ToLower());
		animator.Advance(0.0);

		CallDeferred(MethodName.UpdateScrolling, true);
		CallDeferred(MethodName.UpdateCursor);
	}


	[Export]
	private Label[] videoLabels;
	[Export]
	private Label[] audioLabels;
	[Export]
	private Label[] languageLabels;
	[Export]
	private Label[] controlLabels;
	private string ENABLED_STRING = "option_enable";
	private string DISABLED_STRING = "option_disable";
	private string LOW_STRING = "option_low";
	private string MEDIUM_STRING = "option_medium";
	private string HIGH_STRING = "option_high";
	private string MUTE_STRING = "option_mute";
	private string FULLSCREEN_STRING = "option_fullscreen";
	private string FULLSCREEN_NORMAL_STRING = "option_normal_fullscreen";
	private string FULLSCREEN_EXCLUSIVE_STRING = "option_exclusive_fullscreen";
	private void UpdateLabels()
	{
		Vector2I resolution = SaveManager.WINDOW_SIZES[SaveManager.Config.windowSize];
		videoLabels[0].Text = Tr("option_display").Replace("0", (SaveManager.Config.targetDisplay + 1).ToString());
		videoLabels[1].Text = SaveManager.Config.useFullscreen ? FULLSCREEN_STRING : $"{resolution.X}:{resolution.Y}";
		videoLabels[2].Text = SaveManager.Config.useExclusiveFullscreen ? FULLSCREEN_EXCLUSIVE_STRING : FULLSCREEN_NORMAL_STRING;
		videoLabels[3].Text = SaveManager.Config.useVsync ? ENABLED_STRING : DISABLED_STRING;
		videoLabels[4].Text = $"{SaveManager.Config.renderScale}%";
		videoLabels[5].Text = SaveManager.Config.resizeMode.ToString();
		switch (SaveManager.Config.antiAliasing)
		{
			case 0:
				videoLabels[6].Text = DISABLED_STRING;
				break;
			case 1:
				videoLabels[6].Text = "FXAA";
				break;
			case 2:
				videoLabels[6].Text = "2x MSAA";
				break;
			case 3:
				videoLabels[6].Text = "4x MSAA";
				break;
			case 4:
				videoLabels[6].Text = "8x MSAA";
				break;
		}
		videoLabels[7].Text = SaveManager.Config.useHDBloom ? HIGH_STRING : LOW_STRING;

		if (SaveManager.Config.softShadowQuality == SaveManager.QualitySetting.DISABLED)
			videoLabels[8].Text = "option_hard_shadows";
		else
			videoLabels[8].Text = CalculateQualityString(SaveManager.Config.softShadowQuality);
		videoLabels[9].Text = CalculateQualityString(SaveManager.Config.postProcessingQuality);
		videoLabels[10].Text = CalculateQualityString(SaveManager.Config.reflectionQuality);

		audioLabels[0].Text = SaveManager.Config.isMasterMuted ? MUTE_STRING : $"{SaveManager.Config.masterVolume}% ";
		audioLabels[1].Text = SaveManager.Config.isBgmMuted ? MUTE_STRING : $"{SaveManager.Config.bgmVolume}%";
		audioLabels[2].Text = SaveManager.Config.isSfxMuted ? MUTE_STRING : $"{SaveManager.Config.sfxVolume}%";
		audioLabels[3].Text = SaveManager.Config.isVoiceMuted ? MUTE_STRING : $"{SaveManager.Config.voiceVolume}%";

		languageLabels[0].Text = SaveManager.Config.subtitlesEnabled ? ENABLED_STRING : DISABLED_STRING;
		languageLabels[2].Text = SaveManager.Config.voiceLanguage == SaveManager.VoiceLanguage.English ? "lang_en" : "lang_ja";

		switch (SaveManager.Config.controllerType)
		{
			case SaveManager.ControllerType.PlayStation:
				controlLabels[0].Text = "option_controller_ps";
				break;
			case SaveManager.ControllerType.Xbox:
				controlLabels[0].Text = "option_controller_xbox";
				break;
			case SaveManager.ControllerType.Nintendo:
				controlLabels[0].Text = "option_controller_nintendo";
				break;
			case SaveManager.ControllerType.Steam:
				controlLabels[0].Text = "option_controller_steam";
				break;
		}

		controlLabels[1].Text = $"{Mathf.RoundToInt(SaveManager.Config.deadZone * 100)}%";
	}


	private string CalculateQualityString(SaveManager.QualitySetting setting)
	{
		switch (setting)
		{
			case SaveManager.QualitySetting.LOW:
				return LOW_STRING;
			case SaveManager.QualitySetting.MEDIUM:
				return MEDIUM_STRING;
			case SaveManager.QualitySetting.HIGH:
				return HIGH_STRING;
		}

		return DISABLED_STRING;
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
		if (string.IsNullOrEmpty(id)) // Button was remapped to the same button
			CancelSFX();
		else // Actual remap has occurred
			ConfirmSFX();

		if (e == null)
			return;

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

		ConfirmSFX();
		StartSelectionTimer();
		UpdateLabels();
		SaveManager.ApplyConfig();
	}


	private bool SlideVideoOption(int direction)
	{
		if (VerticalSelection == 0)
		{
			if (DisplayServer.GetScreenCount() <= 1)
				return false;

			SaveManager.Config.targetDisplay = WrapSelection(SaveManager.Config.targetDisplay + direction, DisplayServer.GetScreenCount());
		}
		else if (VerticalSelection == 1)
		{
			int fullscreenResolution = FindLargestWindowResolution() + 1;

			// Switch out of fullscreen mode
			if (SaveManager.Config.useFullscreen)
			{
				SaveManager.Config.useFullscreen = !SaveManager.Config.useFullscreen;
				SaveManager.Config.windowSize = fullscreenResolution;
			}

			SaveManager.Config.windowSize += direction;

			// Prevent user from choosing an impossible resolution
			if (SaveManager.Config.windowSize < 0)
				SaveManager.Config.windowSize = fullscreenResolution;
			else if (SaveManager.Config.windowSize > fullscreenResolution)
				SaveManager.Config.windowSize = 0;

			// Enter fullscreen mode
			if (SaveManager.Config.windowSize == fullscreenResolution)
			{
				SaveManager.Config.useFullscreen = true;
				SaveManager.Config.windowSize = FindLargestWindowResolution();
			}
		}
		else if (VerticalSelection == 2)
			SaveManager.Config.useExclusiveFullscreen = !SaveManager.Config.useExclusiveFullscreen;
		else if (VerticalSelection == 3)
			SaveManager.Config.useVsync = !SaveManager.Config.useVsync;
		else if (VerticalSelection == 4)
		{
			SaveManager.Config.renderScale += direction * 10;
			if (SaveManager.Config.renderScale < 50)
				SaveManager.Config.renderScale = 150;
			else if (SaveManager.Config.renderScale >= 150)
				SaveManager.Config.renderScale = 50;
		}
		else if (VerticalSelection == 5)
		{
			int resizeMode = (int)SaveManager.Config.resizeMode;
			resizeMode = WrapSelection(resizeMode + direction, (int)RenderingServer.ViewportScaling3DMode.Max);
			SaveManager.Config.resizeMode = (RenderingServer.ViewportScaling3DMode)resizeMode;
		}
		else if (VerticalSelection == 6) // TODO Change this to 5 when upgrading to godot v4.3
			SaveManager.Config.antiAliasing = WrapSelection(SaveManager.Config.antiAliasing + direction, 3);
		else if (VerticalSelection == 7)
			SaveManager.Config.useHDBloom = !SaveManager.Config.useHDBloom;
		else if (VerticalSelection == 8)
		{
			int softShadowQuality = (int)SaveManager.Config.softShadowQuality;
			softShadowQuality = WrapSelection(softShadowQuality + direction, (int)SaveManager.QualitySetting.COUNT);
			SaveManager.Config.softShadowQuality = (SaveManager.QualitySetting)softShadowQuality;
		}
		else if (VerticalSelection == 9)
		{
			int postProcessingQuality = (int)SaveManager.Config.postProcessingQuality;
			postProcessingQuality = WrapSelection(postProcessingQuality + direction, (int)SaveManager.QualitySetting.COUNT);
			SaveManager.Config.postProcessingQuality = (SaveManager.QualitySetting)postProcessingQuality;
			StageSettings.instance.UpdatePostProcessingStatus();
		}
		else if (VerticalSelection == 10)
		{
			int reflectionQuality = (int)SaveManager.Config.reflectionQuality;
			reflectionQuality = WrapSelection(reflectionQuality + direction, (int)SaveManager.QualitySetting.COUNT);
			SaveManager.Config.reflectionQuality = (SaveManager.QualitySetting)reflectionQuality;
		}

		return true;
	}


	private int FindLargestWindowResolution()
	{
		for (int i = SaveManager.WINDOW_SIZES.Length - 1; i >= 0; i--)
		{
			if (SaveManager.WINDOW_SIZES[i] >= DisplayServer.ScreenGetSize())
				continue;

			return i;
		}

		return -1;
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
		else if (VerticalSelection == 1)
		{
			float deadZone = SaveManager.Config.deadZone;
			deadZone = Mathf.Clamp(deadZone + .1f * direction, 0f, .9f);
			SaveManager.Config.deadZone = deadZone;
			SaveManager.ApplyInputMap();
			return true;
		}

		return false;
	}

	private void ConfirmVideoOption()
	{
		if (VerticalSelection == 1) // Toggle fullscreen mode
		{
			SaveManager.Config.useFullscreen = !SaveManager.Config.useFullscreen;
			SaveManager.Config.windowSize = FindLargestWindowResolution();
		}
		else
			SlideVideoOption(1);

		ConfirmSFX();
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

		ConfirmSFX();
	}


	private void ConfirmControlOption()
	{
		if (VerticalSelection == 1) return;

		if (VerticalSelection == 0)
			SlideControlOption(1);
		else if (VerticalSelection == 2)
			FlipBook(Submenus.Mapping, false, 0);
		else
			FlipBook(Submenus.Test, false, VerticalSelection);

		ConfirmSFX();
	}


	private void ConfirmSFX()
	{
		animator.Play("confirm");
		animator.Advance(0.0);
	}


	private void CancelSFX()
	{
		animator.Play("cancel");
		animator.Advance(0.0);
	}
}
