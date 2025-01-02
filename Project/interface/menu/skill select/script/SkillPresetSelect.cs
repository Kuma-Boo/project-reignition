using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SkillPresetSelect : Menu
{
	private enum Direction
	{
		Up,
		Down
	}

	[Export]
	private SkillSelect skillSelectMenu;
	[Export]
	private PackedScene presetOption;
	[Export]
	private VBoxContainer presetContainer;
	[Export]
	private Sprite2D scrollbar;

	[Export]
	private Label saveLabel; //We're changing this to "overwrite" if a save already exists

	[Export]
	private AnimationPlayer animatorOptions;

	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private Vector2 containerVelocity;
	private const float scrollSmoothing = .1f;

	private readonly int scrollInterval = 63;
	private readonly int pageSize = 5;

	/// <summary> Number of available save slots for presets. </summary>
	private readonly int PresetsSlots = 20;

	private Array<SkillPresetOption> presetList = [];

	private bool isInitialized;

	public bool isSubMenuActive;
	private int subIndex;

	protected override void SetUp()
	{
		// Create Preset Option nodes
		for (int i = 0; i < PresetsSlots; i++)
		{
			SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();
			newPreset.DisplayNumber = i + 1; // For displaying the number

			presetList.Add(newPreset);
			presetContainer.AddChild(newPreset);
		}
	}

	public override void ShowMenu()
	{
		GD.Print("Showing presets");
		animator.Play("show");
		LoadPresets();

		base.ShowMenu();
	}

	public void LoadPresets()
	{
		//currentPresets = new Array<SkillPreset>();
		for (int i = 0; i < presetList.Count; i++)
		{
			GD.Print("LOADING PRESET " + i);

			presetList[i].presetName = SaveManager.ActiveGameData.presetNames[i];
			presetList[i].skills = SaveManager.ActiveGameData.presetSkills[i];
			presetList[i].skillAugments = SaveManager.ActiveGameData.presetSkillAugments[i];

			presetList[i].Initialize();
		}

		isInitialized = true;
		presetList[0].SelectRight();
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign == 0)
			return;

		if (isSubMenuActive)
		{
			subIndex = WrapSelection(subIndex + inputSign, 5);
			MoveSubCursor();
			return;
		}

		presetList[VerticalSelection].DeselectInstant();
		VerticalSelection = WrapSelection(VerticalSelection + inputSign, presetList.Count);
		MoveCursor(inputSign < 0 ? Direction.Up : Direction.Down, VerticalSelection);
		GD.Print("Selected index ", VerticalSelection);
	}

	protected override void Confirm()
	{
		if (isSubMenuActive)
		{
			switch (subIndex)
			{
				case 0:
					SaveSkills(VerticalSelection);
					break;
				case 1:
					if (!IsInvalid(VerticalSelection))
						LoadSkills(VerticalSelection);
					break;
				case 2:
					//RenamePreset();
					break;
				case 3:
					if (!IsInvalid(VerticalSelection))
						DeletePreset(VerticalSelection);
					break;
				case 4:
					animatorOptions.Play("hide");
					isSubMenuActive = false;
					break;
			}
			return;
		}

		// Show the submenu
		subIndex = 0;
		MoveSubCursor();
		animatorOptions.Play("show");
		isSubMenuActive = true;
	}

	protected override void Cancel()
	{
		if (isSubMenuActive)
		{
			animatorOptions.Play("hide");
			isSubMenuActive = false;
		}
		else
		{
			animator.Play("hide");
			SaveManager.SaveGameData();
			OpenParentMenu();
			//Return to skill editing
		}
	}

	public void MoveSubCursor()
	{
		switch (subIndex)
		{
			case 0:
				if (!IsInvalid(VerticalSelection))
					animatorOptions.Play("select-save");
				else
					animatorOptions.Play("select-save-invalid");
				break;
			case 1:
				if (!IsInvalid(VerticalSelection))
					animatorOptions.Play("select-load");

				else
					animatorOptions.Play("select-load-invalid");
				break;
			case 2:
				if (!IsInvalid(VerticalSelection))
					animatorOptions.Play("select-rename");
				else
					animatorOptions.Play("select-rename-invalid");
				break;
			case 3:
				if (!IsInvalid(VerticalSelection))
					animatorOptions.Play("select-delete");
				else
					animatorOptions.Play("select-delete-invalid");
				break;
			case 4:
				if (!IsInvalid(VerticalSelection))
					animatorOptions.Play("select-cancel");
				else
					animatorOptions.Play("select-cancel-invalid");
				break;
		}

		if (!isSelectionScrolling)
			StartSelectionTimer();
	}

	private void MoveCursor(Direction dir, int index)
	{
		GD.Print("Index: " + index);

		if (dir == Direction.Up)
			presetList[index].SelectUp();
		else if (dir == Direction.Down)
			presetList[index].SelectDown();

		if (!isSelectionScrolling)
			StartSelectionTimer();
	}

	private void SaveSkills(int preset)
	{
		// Storing our equipped skills into our current preset
		if (string.IsNullOrEmpty(presetList[preset].presetName))
			presetList[preset].presetName = "New Preset";

		presetList[preset].skills = SaveManager.ActiveGameData.equippedSkills.Duplicate();
		presetList[preset].skillAugments = SaveManager.ActiveGameData.equippedAugments.Duplicate();

		SaveManager.ActiveGameData.presetNames[preset] = presetList[preset].presetName;
		SaveManager.ActiveGameData.presetSkills[preset] = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.presetSkillAugments[preset] = presetList[preset].skillAugments.Duplicate();

		// Save our new data to the file and play the animation to initialize the on-screen data
		presetList[preset].SavePreset();
		SaveManager.SaveGameData();

		foreach (SkillPresetOption option in presetList)
		{
			GD.PrintT(option.presetName, option.skills, option.skillAugments);
		}
	}

	private void LoadSkills(int preset)
	{
		SaveManager.ActiveGameData.equippedSkills = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.equippedAugments = presetList[preset].skillAugments.Duplicate();

		//skillSelectMenu.Redraw();
		presetList[preset].SelectPreset();
	}

	private void RenamePreset()
	{

	}

	private void DeletePreset(int preset)
	{
		// A null/empty preset means it's already been deleted.
		if (string.IsNullOrEmpty(SaveManager.ActiveGameData.presetNames[preset]))
			return;

		presetList[preset].presetName = null;
		presetList[preset].skills = null;
		presetList[preset].skillAugments = null;

		SaveManager.SaveGameData();
		presetList[preset].Redraw();
	}

	private bool IsInvalid(int index) => presetList[index].IsInvalid;
}
