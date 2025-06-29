using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Interface.Menus;

public partial class LevelSelect : Menu
{
	[Export] private SaveManager.WorldEnum world;
	[Export] private string areaKey;
	[Export] private Description description;
	[Export] private ReadyMenu readyMenu;

	[Export] private Control cursor;
	private int cursorPosition;
	private Vector2 cursorWidthVelocity;

	[Export] private Control options;
	private Vector2 optionVelocity;
	[Export] private Sprite2D scrollbar;

	public bool ContainsNewStage { get; private set; }

	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private const float ScrollSmoothing = .05f;
	private readonly List<LevelOption> levelOptions = [];

	public bool HasNewLevel()
	{
		foreach (Node node in options.GetChildren())
		{
			if (node is LevelOption levelOption)
			{
				levelOption.UpdateLevelData();

				if (levelOption.IsUnlocked && levelOption.ClearState == Core.SaveManager.GameData.LevelStatus.New)
					return true;
			}
		}

		return false;
	}

	public bool IsWorldUnlocked()
	{
		if (DebugManager.Instance.UseDemoSave)
		{
			/// For the demo, assume the world is unlocked if a stage is available to play.
			foreach (Node node in options.GetChildren())
			{
				if (node is LevelOption levelOption)
				{
					if (levelOption.IsUnlocked)
						return true;
				}
			}

			return false;
		}

		// For the full release--use the actual save data
		return SaveManager.ActiveGameData.IsWorldUnlocked(world);
	}

	protected override void SetUp()
	{
		foreach (Node node in options.GetChildren())
		{
			if (node is LevelOption levelOption)
				levelOptions.Add(levelOption);
		}

		base.SetUp();
	}

	protected override void ProcessMenu()
	{
		base.ProcessMenu();
		UpdateListPosition(ScrollSmoothing);
	}

	public override void ShowMenu()
	{
		VerticalSelection = menuMemory[MemoryKeys.LevelSelect];
		RecalculateListPosition();
		UpdateListPosition(0);

		animator.Play("show");
		UpdateDescription();

		for (int i = 0; i < levelOptions.Count; i++)
			levelOptions[i].ShowOption();

		if (SaveManager.Config.useRetailMenuMusic) // Using retail menu music
			return;

		bool canPlayBgm = IsWorldUnlocked() && bgm?.Stream != null;
		if (canPlayBgm && bgm?.Playing == false)
		{
			// Change to world specific level select music
			parentMenu.FadeBgm(.5f);
			FadeBgm(.5f, true, .5f); // Fade in bgm
			CurrentBgmTime = parentMenu.CurrentBgmTime; // Sync bgm
			readyMenu.SetBgmPlayer(bgm); // Update readymenu's bgm player
		}
		else if (!canPlayBgm)
		{
			// As a fallback, play the parent menu's bgm (won't do anything if parent bgm is already playing)
			parentMenu.PlayBgm();
			readyMenu.SetBgmPlayer(parentMenu.bgm);
		}
	}

	public override void HideMenu()
	{
		for (int i = 0; i < levelOptions.Count; i++)
			levelOptions[i].HideOption();
	}

	protected override void Confirm()
	{
		if (!levelOptions[VerticalSelection].IsUnlocked)
			return;

		base.Confirm();
	}

	protected override void Cancel()
	{
		// Revert bgm music
		if (bgm?.Playing == true)
		{
			FadeBgm(.5f); // Fade out bgm
			parentMenu.FadeBgm(.5f, true, .5f); // Fade in parent bgm
			parentMenu.CurrentBgmTime = CurrentBgmTime; // Sync bgm
		}

		base.Cancel();
	}

	/// <summary> Shows the "Are you ready?" screen. </summary>
	public override void OpenSubmenu()
	{
		readyMenu.SetMapText(areaKey);
		readyMenu.SetMissionText(levelOptions[VerticalSelection].data.MissionTypeKey);
		readyMenu.parentMenu = this;
		readyMenu.LevelPath = levelOptions[VerticalSelection].data.LevelPath;
		readyMenu.ShowMenu();
	}

	protected override void UpdateSelection()
	{
		if (Mathf.IsZeroApprox(Input.GetAxis("ui_up", "ui_down"))) return;

		VerticalSelection = WrapSelection(VerticalSelection + Mathf.Sign(Input.GetAxis("ui_up", "ui_down")), levelOptions.Count);
		menuMemory[MemoryKeys.LevelSelect] = VerticalSelection;
		animator.Play("select");
		animator.Seek(0, true);
		UpdateDescription();
		StartSelectionTimer();
		RecalculateListPosition();
	}

	private void UpdateDescription()
	{
		description.ShowDescription();
		description.Text = levelOptions[VerticalSelection].GetDescription();
	}

	private void RecalculateListPosition()
	{
		cursorPosition = VerticalSelection;
		if (levelOptions.Count > 5)
		{
			if (VerticalSelection < 3)
			{
				scrollRatio = 0;
				scrollAmount = 0;
			}
			else if (VerticalSelection >= levelOptions.Count - 3)
			{
				scrollRatio = 1;
				scrollAmount = levelOptions.Count - 5;
				cursorPosition = 4 - (levelOptions.Count - 1 - VerticalSelection);
			}
			else
			{
				scrollAmount = VerticalSelection - 2;
				scrollRatio = (VerticalSelection - 2) / (levelOptions.Count - 5.0f);
				cursorPosition = 2;
			}
		}
	}

	private void UpdateListPosition(float smoothing)
	{
		float targetScrollPosition = 360 * (VerticalSelection / (levelOptions.Count - 1f));
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, smoothing);

		cursor.Position = cursor.Position.SmoothDamp(new(cursor.Position.X, 220 + (96 * cursorPosition)), ref cursorWidthVelocity, smoothing);
		options.Position = options.Position.SmoothDamp(Vector2.Up * ((96 * scrollAmount) - 32), ref optionVelocity, smoothing);
	}
}