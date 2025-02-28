using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class Options : Menu
{
	[Export] private Control cursor;
	[Export] private AnimationPlayer cursorAnimator;
	[Export] private Control contentContainer;

	[Export] private AnimationPlayer resetAnimator;
	private bool isResetSelected;

	private int maxSelection;
	private int scrollOffset;
	private void CalculateMaxSelection()
	{
		// Recalculate max selection
		switch (currentSubmenu)
		{
			case Submenus.Options:
				maxSelection = 5;
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
				maxSelection = 6;
				break;
			case Submenus.Mapping:
				maxSelection = controlMappingOptions.Length;
				break;
			case Submenus.PartyMapping:
				maxSelection = partyMappingOptions.Length + ExtraPartyModeOptionCount;
				break;
			case Submenus.Test:
				maxSelection = 0;
				break;
		}
	}

	private Callable FullscreenToggleCallable => new(this, MethodName.ToggleFullscreen);
	public static readonly StringName MenuParameter = "menu_texture";

	private Submenus currentSubmenu = Submenus.Options;
	private enum Submenus
	{
		Options, // Main menu
		Video, // Menu for configuring video settings
		Audio,  // Menu for configuring audio volume
		Language, // Menu for localization and language
		Control, // Menu for configuring general control settings
		Reset, // Submenu for resetting the configuration settings
		Mapping, // Control submenu for configuring adventure mode's input mappings
		PartyMapping, // Control submenu for configuring party mode's input mappings
		Unbind, // Control sub-submenu for unbinding inputs
		Test // Control submenu for testing controls
	}

	protected override void SetUp()
	{
		bgm.Play();

		SetUpControlOptions();
		UpdateLabels();
		CalculateMaxSelection();
		UpdatePartyModeDevice(0);
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

		if (submenu == Submenus.PartyMapping)
			UpdateLabels();
	}

	protected override void ProcessMenu()
	{
		UpdateScrolling();
		UpdateCursor();

		if (Input.IsActionJustPressed("button_pause") || Input.IsActionJustPressed("ui_accept"))
			Select();
		else
			base.ProcessMenu();

		if (isPlayerLocked)
			CallDeferred(MethodName.UpdatePlayerPosition);
	}

	[Export] private Node3D lockNode;
	private bool isPlayerLocked;
	private PlayerController Player => StageSettings.Player;
	private void LockPlayer()
	{
		isPlayerLocked = true;
		StageSettings.Player.Skills.EnableBreakSkills();
	}
	private void UnlockPlayer()
	{
		isPlayerLocked = false;
		StageSettings.Player.Skills.DisableBreakSkills();
	}
	private void UpdatePlayerPosition()
	{
		Vector3 lockPosition = Player.GlobalPosition;
		lockPosition.X = Mathf.Clamp(lockPosition.X, lockNode.GlobalPosition.X - 1.2f, lockNode.GlobalPosition.X + 1.2f);
		lockPosition.Z = lockNode.GlobalPosition.Z;
		Player.GlobalPosition = lockPosition;
	}

	protected override void Confirm()
	{
		switch (currentSubmenu)
		{
			case Submenus.Options:
				ConfirmOption();
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
			case Submenus.PartyMapping:
				if (SlidePartyMappingOption(1))
				{
					ConfirmSFX();
					return;
				}

				int selectedIndex = VerticalSelection - ExtraPartyModeOptionCount;
				if (!controlMappingOptions[selectedIndex].IsReady)
					return;

				ConfirmSFX();
				partyMappingOptions[selectedIndex].CallDeferred(ControlOption.MethodName.StartListening);
				break;
			case Submenus.Test:
				return;
			case Submenus.Reset:
				if (!isResetSelected)
				{
					CancelResetMenu();
					return;
				}

				SaveManager.Config = new();
				SaveManager.ResetInputMap();
				resetAnimator.Play("confirm");
				currentSubmenu = Submenus.Options;
				break;
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
				FlipBook(Submenus.Control, true, 3);
				break;
			case Submenus.PartyMapping:
				if (VerticalSelection >= ExtraPartyModeOptionCount &&
					!partyMappingOptions[VerticalSelection - ExtraPartyModeOptionCount].IsReady)
				{
					return;
				}

				CancelSFX();
				FlipBook(Submenus.Control, true, 4);
				break;
			case Submenus.Test:
				return;
			case Submenus.Reset:
				CancelResetMenu();
				break;
			default:
				CancelSFX();
				FlipBook(Submenus.Options, true, (int)currentSubmenu - 1);
				break;
		}
	}

	private void CancelResetMenu()
	{
		if (isResetSelected)
		{
			resetAnimator.Play("select-no");
			resetAnimator.Advance(0.0);
		}

		isResetSelected = false;
		resetAnimator.Play("hide");
		currentSubmenu = Submenus.Options;
	}

	private void Select()
	{
		if (currentSubmenu != Submenus.Test)
		{
			Confirm();
			return;
		}

		if (Player.IsLaunching)
			return;

		// Cancel test
		UnlockPlayer();
		animator.Play("test_end");
		currentSubmenu = Submenus.Control;
	}

	protected override void UpdateSelection()
	{
		if (currentSubmenu == Submenus.Test)
			return;

		if (maxSelection == 0)
			return;

		// Listening for inputs
		if (currentSubmenu == Submenus.Mapping && !controlMappingOptions[VerticalSelection].IsReady)
			return;

		// Listening for inputs
		if (currentSubmenu == Submenus.PartyMapping &&
			VerticalSelection >= ExtraPartyModeOptionCount &&
			!partyMappingOptions[VerticalSelection - ExtraPartyModeOptionCount].IsReady)
		{
			return;
		}

		if (currentSubmenu == Submenus.Reset || Mathf.IsZeroApprox(Input.GetAxis("ui_up", "ui_down")))
		{
			UpdateHorizontalSelection();
			return;
		}

		StartSelectionTimer();
		int targetSelection = VerticalSelection + Mathf.Sign(Input.GetAxis("ui_up", "ui_down"));
		VerticalSelection = WrapSelection(targetSelection, maxSelection);

		animator.Play("select");
		animator.Seek(0, true);
		cursorAnimator.Play("show");
		cursorAnimator.AnimationSetNext("show", "loop");
		cursorAnimator.Seek(0, true);
	}

	[Export] private Control scrollBar;
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
		{
			scrollBar.Position = scrollBar.Position.SmoothDamp(targetPosition, ref scrollBarVelocity, scrollBarSmoothing);
		}
	}

	private void UpdateCursor()
	{
		int offset = VerticalSelection - scrollOffset;
		contentContainer.Position = Vector2.Up * scrollOffset * 60;
		cursor.Position = new(cursor.Position.X, 300 + (offset * 60));
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

	[Export] private Label[] videoLabels;
	[Export] private Label[] audioLabels;
	[Export] private Label[] languageLabels;
	[Export] private Label[] controlLabels;
	[Export] private Label[] partyMappingLabels;

	private readonly string EnabledString = "option_enable";
	private readonly string DisabledString = "option_disable";
	private readonly string HoldString = "option_hold";
	private readonly string ToggleString = "option_toggle";
	private readonly string LowString = "option_low";
	private readonly string MediumString = "option_medium";
	private readonly string HighString = "option_high";
	private readonly string MuteString = "option_mute";
	private readonly string FullscreenString = "option_fullscreen";
	private readonly string FullscreenNormalString = "option_normal_fullscreen";
	private readonly string FullscreenExclusiveString = "option_exclusive_fullscreen";
	private readonly string PlayerString = "option_player_number";
	private void UpdateLabels()
	{
		Vector2I resolution = SaveManager.WindowSizes[SaveManager.Config.windowSize];
		videoLabels[0].Text = Tr("option_display").Replace("0", (SaveManager.Config.targetDisplay + 1).ToString());
		videoLabels[1].Text = SaveManager.Config.useFullscreen ? FullscreenString : $"{resolution.X}:{resolution.Y}";
		videoLabels[2].Text = SaveManager.Config.useExclusiveFullscreen ? FullscreenExclusiveString : FullscreenNormalString;
		if (SaveManager.Config.framerate == 0)
			videoLabels[3].Text = Tr("option_unlimited_fps");
		else
			videoLabels[3].Text = Tr("option_fps").Replace("0", SaveManager.FrameRates[SaveManager.Config.framerate].ToString());
		videoLabels[4].Text = SaveManager.Config.useVsync ? EnabledString : DisabledString;
		videoLabels[5].Text = $"{SaveManager.Config.renderScale}%";
		videoLabels[6].Text = SaveManager.Config.resizeMode.ToString();
		switch (SaveManager.Config.antiAliasing)
		{
			case 0:
				videoLabels[7].Text = DisabledString;
				break;
			case 1:
				videoLabels[7].Text = "FXAA";
				break;
			case 2:
				videoLabels[7].Text = "2x MSAA";
				break;
			case 3:
				videoLabels[7].Text = "4x MSAA";
				break;
			case 4:
				videoLabels[7].Text = "8x MSAA";
				break;
		}
		videoLabels[8].Text = CalculateQualityString(SaveManager.Config.bloomMode);

		if (SaveManager.Config.softShadowQuality == SaveManager.QualitySetting.Disabled)
			videoLabels[9].Text = "option_hard_shadows";
		else
			videoLabels[9].Text = CalculateQualityString(SaveManager.Config.softShadowQuality);
		videoLabels[10].Text = CalculateQualityString(SaveManager.Config.postProcessingQuality);
		videoLabels[11].Text = CalculateQualityString(SaveManager.Config.reflectionQuality);
		videoLabels[12].Text = SaveManager.Config.useMotionBlur ? EnabledString : DisabledString;
		videoLabels[13].Text = SaveManager.Config.useScreenShake ? $"{SaveManager.Config.screenShake}%" : DisabledString;

		audioLabels[0].Text = SaveManager.Config.isMasterMuted ? MuteString : $"{SaveManager.Config.masterVolume}%";
		audioLabels[1].Text = SaveManager.Config.isBgmMuted ? MuteString : $"{SaveManager.Config.bgmVolume}%";
		audioLabels[2].Text = SaveManager.Config.isSfxMuted ? MuteString : $"{SaveManager.Config.sfxVolume}%";
		audioLabels[3].Text = SaveManager.Config.isVoiceMuted ? MuteString : $"{SaveManager.Config.voiceVolume}%";

		languageLabels[0].Text = SaveManager.Config.subtitlesEnabled ? EnabledString : DisabledString;
		languageLabels[2].Text = SaveManager.Config.voiceLanguage == SaveManager.VoiceLanguage.English ? "lang_en" : "lang_ja";

		switch (SaveManager.Config.controllerType)
		{
			case SaveManager.ControllerType.Automatic:
				controlLabels[0].Text = "option_controller_auto";
				break;
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
		controlLabels[2].Text = SaveManager.Config.useHoldBreakMode ? HoldString : ToggleString;

		partyMappingLabels[0].Text = Tr(PlayerString).Replace("0", partyPlayerIndex.ToString());
		partyMappingLabels[1].Text = partyMappingOptions[0].GetDevice();
	}

	private string CalculateQualityString(SaveManager.QualitySetting setting)
	{
		switch (setting)
		{
			case SaveManager.QualitySetting.Low:
				return LowString;
			case SaveManager.QualitySetting.Medium:
				return MediumString;
			case SaveManager.QualitySetting.High:
				return HighString;
		}

		return DisabledString;
	}

	[Export] private ControlOption[] controlMappingOptions;
	[Export] private ControlOption[] partyMappingOptions;
	private int partyPlayerIndex = 1; // Index of the player whose controls are currently being edited
	private readonly int ExtraPartyModeOptionCount = 2; // Offset for playerIndex and controllerIndex
	private void SetUpControlOptions()
	{
		foreach (ControlOption controlOption in controlMappingOptions)
			controlOption.SwapMapping += RedrawControlOptions;

		foreach (ControlOption controlOption in partyMappingOptions)
			controlOption.SwapMapping += RedrawControlOptions;
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
			if (controlOption.ActionName == id)
				controlOption.ReceiveInput(e, true);
		}

		foreach (ControlOption controlOption in partyMappingOptions)
		{
			if (controlOption.ActionName == id)
				controlOption.ReceiveInput(e, true);
		}
	}

	private void UpdateHorizontalSelection()
	{
		if (Mathf.IsZeroApprox(Input.GetAxis("ui_left", "ui_right"))) return;

		int direction = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));

		if (currentSubmenu == Submenus.Reset)
		{
			if ((direction > 0 && isResetSelected) || (direction < 0 && !isResetSelected))
			{
				isResetSelected = !isResetSelected;
				resetAnimator.Play(isResetSelected ? "select-yes" : "select-no");
			}

			return;
		}

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
			case Submenus.PartyMapping:
				settingUpdated = SlidePartyMappingOption(direction);
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
		{
			SaveManager.Config.useExclusiveFullscreen = !SaveManager.Config.useExclusiveFullscreen;
		}
		else if (VerticalSelection == 3)
		{
			SaveManager.Config.framerate = WrapSelection(SaveManager.Config.framerate + direction, SaveManager.FrameRates.Length);
		}
		else if (VerticalSelection == 4)
		{
			SaveManager.Config.useVsync = !SaveManager.Config.useVsync;
		}
		else if (VerticalSelection == 5)
		{
			SaveManager.Config.renderScale += direction * 10;
			SaveManager.Config.renderScale = Mathf.Clamp(SaveManager.Config.renderScale, 50, 150);
		}
		else if (VerticalSelection == 6)
		{
			int resizeMode = (int)SaveManager.Config.resizeMode;
			resizeMode = WrapSelection(resizeMode + direction, (int)RenderingServer.ViewportScaling3DMode.Max);
			SaveManager.Config.resizeMode = (RenderingServer.ViewportScaling3DMode)resizeMode;
		}
		else if (VerticalSelection == 7) // TODO Change this to 6 when upgrading to godot v4.3
		{
			SaveManager.Config.antiAliasing = WrapSelection(SaveManager.Config.antiAliasing + direction, 3);
		}
		else if (VerticalSelection == 8)
		{
			int bloomMode = (int)SaveManager.Config.bloomMode;
			bloomMode = WrapSelection(bloomMode + direction, (int)SaveManager.QualitySetting.Count);
			if (bloomMode == (int)SaveManager.QualitySetting.Medium) // Skip medium setting
				bloomMode = WrapSelection(bloomMode + direction, (int)SaveManager.QualitySetting.Count);
			SaveManager.Config.bloomMode = (SaveManager.QualitySetting)bloomMode;
		}
		else if (VerticalSelection == 9)
		{
			int softShadowQuality = (int)SaveManager.Config.softShadowQuality;
			softShadowQuality = WrapSelection(softShadowQuality + direction, (int)SaveManager.QualitySetting.Count);
			SaveManager.Config.softShadowQuality = (SaveManager.QualitySetting)softShadowQuality;
		}
		else if (VerticalSelection == 10)
		{
			int postProcessingQuality = (int)SaveManager.Config.postProcessingQuality;
			postProcessingQuality = WrapSelection(postProcessingQuality + direction, (int)SaveManager.QualitySetting.Count);
			SaveManager.Config.postProcessingQuality = (SaveManager.QualitySetting)postProcessingQuality;
			StageSettings.Instance.UpdatePostProcessingStatus();
		}
		else if (VerticalSelection == 11)
		{
			int reflectionQuality = (int)SaveManager.Config.reflectionQuality;
			reflectionQuality = WrapSelection(reflectionQuality + direction, (int)SaveManager.QualitySetting.Count);
			SaveManager.Config.reflectionQuality = (SaveManager.QualitySetting)reflectionQuality;
		}
		else if (VerticalSelection == 12)
		{
			SaveManager.Config.useMotionBlur = !SaveManager.Config.useMotionBlur;
		}
		else if (VerticalSelection == 13)
		{
			if (!IsSlideVolumeValid(SaveManager.Config.screenShake, direction))
				return false;

			SaveManager.Config.useScreenShake = true;
			SaveManager.Config.screenShake = SlideVolume(SaveManager.Config.screenShake, direction);
		}

		return true;
	}

	private int FindLargestWindowResolution()
	{
		for (int i = SaveManager.WindowSizes.Length - 1; i >= 0; i--)
		{
			if (SaveManager.WindowSizes[i] >= DisplayServer.ScreenGetSize())
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
			deadZone = Mathf.Clamp(deadZone + (.1f * direction), 0f, .9f);
			SaveManager.Config.deadZone = deadZone;
			SaveManager.ApplyInputMap();
			return true;
		}
		else if (VerticalSelection == 2)
		{
			SaveManager.Config.useHoldBreakMode = !SaveManager.Config.useHoldBreakMode;
			return true;
		}

		return false;
	}

	private bool SlidePartyMappingOption(int direction)
	{
		if (VerticalSelection < ExtraPartyModeOptionCount)
		{
			if (VerticalSelection == 0)
			{
				// Change player index
				partyPlayerIndex += direction;
				if (partyPlayerIndex > 4)
					partyPlayerIndex = 1;
				else if (partyPlayerIndex < 1)
					partyPlayerIndex = 4;
				foreach (ControlOption controlOption in partyMappingOptions)
				{
					controlOption.PartyModeControllerIndex = partyPlayerIndex;
					controlOption.RedrawBinding();
				}
			}

			UpdatePartyModeDevice(direction);
			return true;
		}

		return false;
	}

	private void UpdatePartyModeDevice(int direction)
	{
		// Change device
		if (VerticalSelection == 1)
		{
			int deviceIndex = SaveManager.Config.partyModeDevices[partyPlayerIndex - 1];
			deviceIndex += direction;
			if (deviceIndex < 0)
				deviceIndex = 7;
			else if (deviceIndex > 7)
				deviceIndex = 0;
			SaveManager.Config.partyModeDevices[partyPlayerIndex - 1] = deviceIndex;
		}

		foreach (ControlOption controlOption in partyMappingOptions)
			controlOption.UpdateDevice();
	}

	private void ConfirmOption()
	{
		if (VerticalSelection == 4)
		{
			currentSubmenu = Submenus.Reset;
			resetAnimator.Play("show");
			isResetSelected = false;
			return;
		}

		ConfirmSFX();
		FlipBook((Submenus)VerticalSelection + 1, false, 0);
	}

	private void ConfirmVideoOption()
	{
		if (VerticalSelection == 1) // Toggle fullscreen mode
		{
			SaveManager.Config.useFullscreen = !SaveManager.Config.useFullscreen;
			SaveManager.Config.windowSize = FindLargestWindowResolution();
		}
		else if (VerticalSelection == 13)
		{
			SaveManager.Config.useScreenShake = !SaveManager.Config.useScreenShake;
		}
		else
		{
			SlideVideoOption(1);
		}

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
			SlideControlOption(1);
		else if (VerticalSelection == 3)
			FlipBook(Submenus.Mapping, false, 0);
		else if (VerticalSelection == 4)
			FlipBook(Submenus.PartyMapping, false, 0);
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
