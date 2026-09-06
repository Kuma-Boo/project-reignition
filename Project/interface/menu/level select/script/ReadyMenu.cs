using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class ReadyMenu : Menu
{
	[Export] private Label mapLabel;
	[Export] private Label missionLabel;
	[Export] private Description description;
	[Export] private AnimationPlayer notifAnimPlayer;
	[Export] private AudioStreamPlayer selectSfx;
	[Export] private ModDrawer modDrawer;
	public void ShowDescription() => description.ShowDescription();
	public void HideDescription() => description.HideDescription();

	public void SetMapText(string text) => mapLabel.Text = text;
	public void SetMissionText(string text) => missionLabel.Text = text;

	public override void ShowMenu()
	{
		modDrawer.SetCanOpen(true);
		if (!SaveManager.Config.areCharaModsEnabled)
			modDrawer.Visible = false;

		if (menuMemory[MemoryKeys.SkillMenuOpen] == 1)
		{
			animator.Play("show-from-skill");
		}
		else
		{
			if (Runtime.Instance.IsUsingMouse)
			{
				HorizontalSelection = -1;
				animator.Play("select-none");
			}
			else
			{
				// Default to yes
				HorizontalSelection = 0;
				animator.Play("select-yes");
			}
			animator.Advance(0.0);
			base.ShowMenu();
		}

		if (TimeAttackManager.Instance.IsRunActive)
		{
			if (!TimeAttackManager.Instance.IsLastLevel())
				SetupReadyMenu(TimeAttackManager.Instance.GetCurrentLevel());
		}

		if (!TimeAttackManager.Instance.IsRunActive && SaveManager.ActiveGameData.HasNewSkill())
			notifAnimPlayer.Play("show");
		else
			notifAnimPlayer.Play("hide");

		menuMemory[MemoryKeys.SkillMenuOpen] = 0;
	}

	protected override void ProcessMenu()
	{
		if (Runtime.Instance.IsActionJustPressed("sys_pause", "ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen"))
		{
			menuMemory[MemoryKeys.SkillMenuOpen] = 1;
			HideDescription();
			OpenSubmenu();
			animator.Play("open-skill-menu");
			return;
		}

		base.ProcessMenu();
	}

	protected override void Confirm()
	{
		if (HorizontalSelection == -1)
			return;

		if (HorizontalSelection == 0) // Load level
		{
			modDrawer.SetCanOpen(false);
			StopBgm(); // Stop bgm

			if (!TimeAttackManager.Instance.IsRunActive)
				menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.LevelSelect;
			else
			{

				if (TimeAttackManager.Instance.CurrentRunType != TimeAttackManager.RunType.SingleRun)
				{
					SaveManager.TimeData.Tries = 0;
					TimeAttackManager.Instance.ClearCurrentRun();
					TimeAttackManager.Instance.ClearCurrentSavedRun();

					SaveManager.TimeData.equippedSkillsContinue = SaveManager.ActiveGameData.equippedSkills;
					SaveManager.TimeData.equippedAugmentsContinue = SaveManager.ActiveGameData.equippedAugments;

					SaveManager.TimeData.CurrentRunType = TimeAttackManager.Instance.CurrentRunType;
					SaveManager.SaveTimeAttackData(); //Overwrites currently saved run
				}
				else
				{
					SaveManager.TimeData.equippedSkillsSingle = SaveManager.ActiveGameData.equippedSkills;
					SaveManager.TimeData.equippedAugmentsSingle = SaveManager.ActiveGameData.equippedAugments;
				}

				menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.TimeAttack;
			}
			base.Confirm();
		}
		else
		{
			Cancel();
		}
	}

	public override void OpenSubmenu()
	{
		DisableProcessing();
		_submenus[0].ShowMenu();
	}

	protected override void Cancel()
	{
		modDrawer.SetCanOpen(false);
		HorizontalSelection = 1;
		animator.Play("select-no");
		animator.Advance(0.0);
		base.Cancel();
	}

	protected override void UpdateSelection()
	{
		int sign = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));

		if ((sign > 0 && HorizontalSelection != 1) || sign < 0 && HorizontalSelection != 0)
		{
			HorizontalSelection = sign > 0 ? 1 : 0;
			selectSfx.Play();
			UpdateVisuals();
		}
	}

	private void UpdateVisuals()
	{
		if (HorizontalSelection == -1)
		{
			animator.Play("select-none");
			return;
		}

		animator.Play(HorizontalSelection == 0 ? "select-yes" : "select-no");
	}

	public void SetBgmPlayer(BGMPlayer audioStreamPlayer) => bgm = audioStreamPlayer;

	/// <summary> The current Level Data. </summary>
	public LevelDataResource LevelData { get; set; }
	/// <summary> Loads the level. </summary>
	public void LoadLevel()
	{
		if (!TimeAttackManager.Instance.IsRunActive)
		{
			// Handle Pre Event Indexes
			if (!string.IsNullOrEmpty(LevelData.PreStoryEvent) &&
				SaveManager.ActiveGameData.LevelData.GetClearStatus(LevelData.LevelID) == SaveManager.LevelSaveData.LevelStatus.New)
			{
				TransitionManager.QueueSceneChange($"{TransitionManager.EventScenePath}{LevelData.PreStoryEvent}.tscn");
				TransitionManager.StartTransition(new()
				{
					inSpeed = 0.5f,
					color = Colors.Black,
				});
				return;
			}
		}

		TransitionManager.QueueSceneChange(LevelData.LevelPath);
		TransitionManager.StartTransition(new()
		{
			inSpeed = 1f,
			color = Colors.Black,
			loadAsynchronously = true,
			disableAutoTransition = true,
			showMissionDescription = true
		});
		TransitionManager.Instance.SetMissionDescriptionText(missionLabel.Text, description.Text);
		TransitionManager.Instance.UpdateLoadingText("load_level");
	}

	/// <summary> Sets up the ready menu for time attack. </summary>
	public void SetupReadyMenu(LevelDataResource level)
	{
		SetMapText(Tr(level.GetAreaKey()));
		SetMissionText(Tr(level.MissionTypeKey));
		description.Text = Tr(level.MissionDescriptionKey);
		LevelData = level;
	}

	private void ReceiveMouseInput(int selection)
	{
		if (!isProcessing)
			return;

		if (HorizontalSelection != selection && selection != -1)
			selectSfx.Play();

		Runtime.Instance.IsUsingMouse = true;
		HorizontalSelection = selection;
		UpdateVisuals();
	}
}
