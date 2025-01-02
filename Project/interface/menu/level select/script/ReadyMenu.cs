using Godot;
using Project.Core;

namespace Project.Interface.Menus;

public partial class ReadyMenu : Menu
{
	[Export]
	private Label mapLabel;
	[Export]
	private Label missionLabel;
	[Export]
	private Description description;
	private bool fromSkillMenu;
	public void ShowDescription() => description.ShowDescription();
	public void HideDescription() => description.HideDescription();

	public void SetMapText(string text) => mapLabel.Text = text;
	public void SetMissionText(string text) => missionLabel.Text = text;

	public override void ShowMenu()
	{
		if (fromSkillMenu)
		{
			EnableProcessing();
			animator.Play("show-from-skill");
		}
		else
		{
			base.ShowMenu();
		}

		fromSkillMenu = false;
		HorizontalSelection = 0; // Default to yes
	}

	protected override void ProcessMenu()
	{
		if (Input.IsActionJustPressed("button_pause"))
		{
			fromSkillMenu = true;
			HideDescription();
			OpenSubmenu();
			return;
		}

		base.ProcessMenu();
	}

	protected override void Confirm()
	{
		if (HorizontalSelection == 0) // Load level
		{
			StopBGM(); // Stop bgm
			menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.LevelSelect;
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
		HorizontalSelection = 1;
		animator.Play("select-no");
		animator.Advance(0.0);
		base.Cancel();
	}

	protected override void UpdateSelection()
	{
		int sign = Mathf.Sign(Input.GetAxis("move_left", "move_right"));
		if (sign == 0) return;

		if (sign > 0 && HorizontalSelection == 0)
		{
			HorizontalSelection = 1;
			animator.Play("select-no");
		}
		else if (sign < 0 && HorizontalSelection == 1)
		{
			HorizontalSelection = 0;
			animator.Play("select-yes");
		}
	}

	/// <summary> Path to the level scene. </summary>
	public string LevelPath { get; set; }
	/// <summary> Loads the level. </summary>
	public void LoadLevel()
	{
		TransitionManager.QueueSceneChange(LevelPath);
		TransitionManager.StartTransition(new()
		{
			inSpeed = 1f,
			color = Colors.Black,
			loadAsynchronously = true,
			disableAutoTransition = true,
			showMissionDescription = true
		});
		TransitionManager.instance.SetMissionDescriptionText(missionLabel.Text, description.Text);
		TransitionManager.instance.UpdateLoadingText("load_level");
	}
}
