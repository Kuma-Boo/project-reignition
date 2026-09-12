using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class TimeAttackStartRun : Menu
{
	[Export] private Description description;
	[Export] TimeAttackReady readyMenu;
	[Export] TimeAttackLevelList levelList;
	[Export] TimeAttackLeaderboard leaderboard;
	[Export] Array<TimeAttackButton> buttonList;
	[Export] private PackedScene levelOption;
	[Export] AnimationPlayer navigationButtonPlayer;

	private bool isLeaderboardActive;
	private int currentSelection = 1;
	private int maxSelection = 2;

	[Export] private AnimationPlayer alertAnimator;
	private bool isAlertMenuActive = false;
	private bool isYesSelected = false;

	public override void ShowMenu()
	{
		base.ShowMenu();
		isLeaderboardActive = false;
		leaderboard.SpawnLeaderboardOptionsSub();
		leaderboard.SpawnLeaderboardOptionsMain();
		navigationButtonPlayer.Play("show");
	}

	public override void EnableProcessing()
	{
		base.EnableProcessing();
		//RedrawSelection();
	}

	protected override void UpdateSelection()
	{
		if (isAlertMenuActive)
		{
			int inputReturn = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));
			if ((inputReturn > 0 && isYesSelected) || (inputReturn < 0 && !isYesSelected))
			{
				isYesSelected = !isYesSelected;
				alertAnimator.Play(isYesSelected ? "select-yes" : "select-no");
			}

			return;
		}

		Vector2I input = new(Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), Mathf.Sign(Input.GetAxis("ui_up", "ui_down")));
		StartSelectionTimer();
		ProcessMenuInput(input);
	}

	protected override void ProcessMenu()
	{
		if (Input.IsActionJustPressed("ui_text_delete") && !isAlertMenuActive)
		{
			ShowAlertMenu();
			return;
		}

		base.ProcessMenu();
	}

	private void ProcessMenuInput(Vector2I input)
	{

		if (isLeaderboardActive)
		{
			if (input.X != 0)
			{
				Runtime.Instance.IsUsingMouse = false;
				ExitLeaderboard();
			}

			return;
		}

		if (!isLeaderboardActive)
		{
			Runtime.Instance.IsUsingMouse = false;

			if (input.X != 0)
			{
				EnterLeaderboard();
				return;
			}

			currentSelection += input.Y;
			if (currentSelection > maxSelection || currentSelection < 1)
				currentSelection = WrapSelection(currentSelection, maxSelection, 1);

			RedrawSelection();
		}


	}

	private void EnterLeaderboard()
	{
		if (isLeaderboardActive)
			return;

		leaderboard.EnableProcessing();
		isLeaderboardActive = true;
		for (int i = 0; i < buttonList.Count; i++)
			buttonList[i].DeselectButton();
		leaderboard.ShowMenu();
	}

	private void ExitLeaderboard()
	{
		if (!isLeaderboardActive)
			return;

		leaderboard.DisableProcessing();
		buttonList[currentSelection - 1].SelectButton();
		leaderboard.DeselectMenu();
		isLeaderboardActive = false;
	}

	private void RedrawSelection()
	{

		for (int i = 0; i < buttonList.Count; i++)
			buttonList[i].DeselectButton();

		buttonList[currentSelection - 1].SelectButton();

		description.Text = buttonList[currentSelection - 1].description;
		description.ShowDescription();
	}

	protected override void Confirm()
	{
		if (isAlertMenuActive)
		{
			if (isYesSelected)
			{
				isAlertMenuActive = false;
				alertAnimator.Advance(0.0);
				alertAnimator.Play("confirm");
				SaveManager.TimeData.ResetCategory(TimeAttackManager.Instance.CurrentRunType);
				SaveManager.SaveTimeAttackData();
				leaderboard.SpawnLeaderboardOptionsSub();
				leaderboard.SpawnLeaderboardOptionsMain();
				leaderboard.SwapToMain();
			}
			else
			{
				isAlertMenuActive = false;
				alertAnimator.Advance(0.0);
				alertAnimator.Play("hide");
			}
			return;
		}
		if (isLeaderboardActive)
			return;

		if (currentSelection == 1)
		{
			navigationButtonPlayer.Play("hide");
			readyMenu.SetupReadyMenu();
		}
		animator.Play("confirm-" + currentSelection);
		base.Confirm();
	}

	protected override void Cancel()
	{
		if (isAlertMenuActive)
		{
			isAlertMenuActive = false;
			CancelAlertMenu();
			return;
		}

		if (!isLeaderboardActive)
			animator.Play("hide");
	}

	private void OpenLevelList()
	{
		levelList.Visible = true;
		levelList.parentMenu = this;
		levelList.ShowMenu();
	}

	private void OpenReadyMenu() => readyMenu.ShowMenu();

	private void ReceiveMouseInput(int selection)
	{
		if (currentSelection == selection)
			return;

		Runtime.Instance.IsUsingMouse = true;
		currentSelection = selection;
		if (isProcessing)
			RedrawSelection();
	}

	private void ShowAlertMenu()
	{
		isAlertMenuActive = true;
		isYesSelected = false;
		leaderboard.DisableProcessing();

		alertAnimator.Advance(0.0);
		alertAnimator.Play("show");
	}

	private void CancelAlertMenu()
	{
		if (isYesSelected)
		{
			alertAnimator.Play("select-no");
			alertAnimator.Advance(0.0);
		}

		alertAnimator.Play("hide");

		if (leaderboard.isActive)
			leaderboard.EnableProcessing();
	}

	private void AlertMenuClosed()
	{
		isAlertMenuActive = false;

		if (leaderboard.isActive)
			leaderboard.EnableProcessing();
		EnableProcessing();
	}
}
