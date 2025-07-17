using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SaveSelect : Menu
{
	[Export]
	private Sprite2D scrollbar;
	private Vector2 scrollbarVelocity;
	private float scrollRatio;
	private const int ScrollbarHeight = 625;
	private const float ScrollSmoothing = .05f;

	[Export]
	private Array<NodePath> saveOptions = [];
	private readonly Array<SaveOption> _saveOptions = [];
	private const int ActiveSaveOptionIndex = 3; // Corresponds to the center save option

	[Export]
	private AnimationPlayer deleteAnimator;
	private bool isDeleteMenuActive;
	private bool isDeleteSelected;

	protected override void SetUp()
	{
		VerticalSelection = menuMemory[MemoryKeys.SaveSelect];
		scrollRatio = VerticalSelection / (SaveManager.SaveSlotCount - 1.0f);

		for (int i = 0; i < saveOptions.Count; i++)
		{
			SaveOption option = GetNode<SaveOption>(saveOptions[i]);
			_saveOptions.Add(option);
			option.SetUp();
		}
	}

	public override void _PhysicsProcess(double _)
	{
		base._PhysicsProcess(_);
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * ScrollbarHeight * scrollRatio, ref scrollbarVelocity, ScrollSmoothing);
	}

	protected override void ProcessMenu()
	{
		if (Input.IsActionJustPressed("sys_clear") || Input.IsActionJustPressed("ui_text_delete"))
		{
			if (isDeleteMenuActive)
				CancelDeleteMenu();
			else
				ShowDeleteMenu();

			return;
		}

		base.ProcessMenu();
	}

	protected override void Confirm()
	{
		if (isDeleteMenuActive)
		{
			if (isDeleteSelected)
			{
				deleteAnimator.Play("confirm");
				DeleteSaveFile();
				isDeleteMenuActive = false;
			}
			else
			{
				CancelDeleteMenu();
			}

			return;
		}

		base.Confirm();
	}

	protected override void Cancel()
	{
		if (isDeleteMenuActive)
		{
			CancelDeleteMenu();
			return;
		}

		base.Confirm();
	}

	private void ShowDeleteMenu()
	{
		int saveIndex = _saveOptions[ActiveSaveOptionIndex].SaveIndex;
		if (SaveManager.GameSaveSlots[saveIndex].IsNewFile()) // Check if a save file is new
			return;

		deleteAnimator.Play("show");
		isDeleteMenuActive = true;
		isDeleteSelected = false;
	}

	protected override void UpdateSelection()
	{
		if (isDeleteMenuActive)
		{
			int input = Mathf.Sign(Input.GetAxis("ui_left", "ui_right"));
			if ((input > 0 && isDeleteSelected) ||
				(input < 0 && !isDeleteSelected))
			{
				isDeleteSelected = !isDeleteSelected;
				deleteAnimator.Play(isDeleteSelected ? "select-yes" : "select-no");
			}

			return;
		}

		// Only listen for vertical scrolling
		int inputSign = Mathf.Sign(Input.GetAxis("ui_up", "ui_down"));
		if (inputSign == 0) return;

		VerticalSelection = WrapSelection(VerticalSelection + inputSign, SaveManager.SaveSlotCount);
		animator.Play(inputSign < 0 ? ScrollUpAnimation : ScrollDownAnimation);
		scrollRatio = VerticalSelection / (SaveManager.SaveSlotCount - 1.0f);
		menuMemory[MemoryKeys.SaveSelect] = VerticalSelection;

		if (!isSelectionScrolling)
			StartSelectionTimer();
	}

	public override void OpenSubmenu()
	{
		SaveManager.ActiveSaveSlotIndex = _saveOptions[ActiveSaveOptionIndex].SaveIndex;
		SaveManager.ActiveSkillRing.LoadFromActiveData();

		if (SaveManager.ActiveGameData.IsNewFile())
		{
			SaveManager.ResetSaveData(SaveManager.ActiveSaveSlotIndex, false);
			SaveManager.SaveGameData();

			if (!DebugManager.Instance.UseDemoSave) // Don't load into cutscenes in the demo
			{
				// Load directly into the first cutscene
				TransitionManager.QueueSceneChange($"{TransitionManager.EventScenePath}1.tscn");
				TransitionManager.StartTransition(new()
				{
					color = Colors.Black,
					inSpeed = 1f,
				});
			}
		}

		if (DebugManager.Instance.UseDemoSave || OS.IsDebugBuild()) // Unlock all worlds in the demo
			SaveManager.ActiveGameData.UnlockAllWorlds();

		menuMemory[MemoryKeys.WorldSelect] = (int)SaveManager.ActiveGameData.lastPlayedWorld; // Set the world selection to the last played world
		_submenus[0].ShowMenu();

		DebugManager.Instance.OnSkillSelected(-1); // Update Debug Menu
	}

	public override void ShowMenu()
	{
		SaveManager.LoadGameData(); // Check for file updates
		base.ShowMenu();
	}

	private void CancelDeleteMenu()
	{
		if (isDeleteSelected)
		{
			deleteAnimator.Play("select-no");
			deleteAnimator.Advance(0.0);
		}

		isDeleteMenuActive = false;
		deleteAnimator.Play("hide");
	}

	/// <summary> Deletes the currently selected save file. </summary>
	private void DeleteSaveFile()
	{
		int saveIndex = _saveOptions[ActiveSaveOptionIndex].SaveIndex; // Get the currently selected save index

		SaveManager.ResetSaveData(saveIndex, true); // Reset SaveManager's loaded GameData
		SaveManager.DeleteSaveData(saveIndex); // Move the save game's file to the system trash

		UpdateSaveOptions();
	}

	/// <summary>  Updates the visual data on all save options. </summary>
	public void UpdateSaveOptions()
	{
		for (int i = 0; i < _saveOptions.Count; i++)
		{
			int saveIndex = VerticalSelection + (i - ActiveSaveOptionIndex);
			saveIndex = WrapSelection(saveIndex, SaveManager.SaveSlotCount);
			_saveOptions[i].SaveIndex = saveIndex;
		}
	}
}
