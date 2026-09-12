using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class TimeAttackNewRun : Menu
{
	[Export] AnimationPlayer newRunAnimator;
	[Export] private Description description;
	[Export] TimeAttack thisParent;
	[Export] SaveSelect saveSelect;
	[Export] TimeAttackStartRun startRun;
	[Export] TimeAttackLevelList levelList;
	[Export] private TextureRect buttonImage;
	[Export] private AnimationPlayer buttonImageAnimator;
	[Export] Array<TimeAttackButton> buttonList;
	private int currentSelection = 1;
	private int maxSelection = 3;

	public override void ShowMenu()
	{
		base.ShowMenu();
		description.Text = buttonList[0].description;
	}

	public override void EnableProcessing()
	{
		base.EnableProcessing();
		RedrawSelection();
	}

	protected override void UpdateSelection()
	{
		Vector2I input = new(Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), Mathf.Sign(Input.GetAxis("ui_up", "ui_down")));
		StartSelectionTimer();
		ProcessMenuInput(input);
	}

	private void ProcessMenuInput(Vector2I input)
	{
		if (input.Y == 0)
			return;

		Runtime.Instance.IsUsingMouse = false;
		currentSelection += input.Y;
		if (currentSelection > maxSelection || currentSelection < 1)
			currentSelection = WrapSelection(currentSelection, maxSelection, 1);

		RedrawSelection();
	}

	private void RedrawSelection()
	{
		for (int i = 0; i < buttonList.Count; i++)
			buttonList[i].DeselectButton();

		buttonImageAnimator.Play("show");
		buttonImage.Texture = buttonList[currentSelection - 1].image;
		buttonList[currentSelection - 1].SelectButton();

		description.Text = buttonList[currentSelection - 1].description;
		description.ShowDescription();
	}

	protected override void Confirm()
	{
		TimeAttackManager.Instance.ResetLevelCount();

		switch (currentSelection)
		{
			case 1:
				TimeAttackManager.Instance.SetRunType(TimeAttackManager.RunType.AnyP);
				break;
			case 2:
				TimeAttackManager.Instance.SetRunType(TimeAttackManager.RunType.GoalPercent);
				break;
			case 3:
				TimeAttackManager.Instance.SetRunType(TimeAttackManager.RunType.BossRush);
				break;
		}
		TimeAttackManager.Instance.SetRunActive(true);
		levelList.parentMenu = this;

		newRunAnimator.Play("confirm-" + currentSelection);
	}

	protected override void Cancel()
	{
		TimeAttackManager.Instance.SetRunActive(false);
		newRunAnimator.Play("hide");
	}

	public override void OpenSubmenu()
	{
		switch (currentSelection)
		{
			case 1:
				_submenus[0].ShowMenu();
				break;
			case 2:
				OpenSaveSelect();
				break;
			case 3:
				OpenSaveSelect();
				break;
		}
	}
	public void OpenSaveSelect()
	{
		saveSelect.Visible = true;
		saveSelect.parentMenu = this;
		saveSelect.ShowMenu();
	}

	public void OpenStartRun() => startRun.ShowMenu();

	private void ReceiveMouseInput(int selection)
	{
		if (currentSelection == selection)
			return;

		Runtime.Instance.IsUsingMouse = true;
		currentSelection = selection;

		if (isProcessing)
			RedrawSelection();
	}
}
