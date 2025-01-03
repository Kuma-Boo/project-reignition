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

	[Export] private PackedScene presetOption;
	[Export] private VBoxContainer presetContainer;
	//[Export] private Node2D cursor;
	[Export]
	private Node2D cursor;
	[Export] private Sprite2D scrollbar;

	[Export] private Label saveLabel; //We're changing this to "overwrite" if a save already exists

	[Export] private TextEdit nameEditor;

	[Export] private AnimationPlayer animatorOptions;
	[Export] private AnimationPlayer animatorOptionsSelector;

	[Export] private AnimationPlayer animatorNameEditor;

	private Gameplay.SkillRing activeSkillRing => SaveManager.ActiveSkillRing;
	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private Vector2 containerVelocity;
	private const float scrollSmoothing = .1f;
	private readonly int scrollInterval = 240;
	private readonly int pageSize = 4;
	private int cursorPosition;
	private Vector2 cursorVelocity;
	private const float cursorSmoothing = .1f;

	private Array<SkillPresetOption> presetList = [];

	private bool isInitialized;

	public bool isSubMenuActive;

	public bool isEditingName;
	private int subIndex;

	protected override void SetUp()
	{
		// Create Preset Option nodes
		for (int i = 0; i < SaveManager.PresetCount; i++)
		{
			SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();
			newPreset.DisplayNumber = i + 1; // For displaying the number

			presetList.Add(newPreset);
			presetContainer.AddChild(newPreset);
		}
	}

	public override void _Process(double _)
	{
		float targetScrollPosition = (160 * scrollRatio) - 80;
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, scrollSmoothing);

		// Update cursor position
		float targetCursorPosition = cursorPosition * scrollInterval;
		cursor.Position = cursor.Position.SmoothDamp(Vector2.Down * targetCursorPosition, ref cursorVelocity, cursorSmoothing);

		Vector2 targetContainerPosition = new(presetContainer.Position.X, -scrollAmount * scrollInterval);
		presetContainer.Position = presetContainer.Position.SmoothDamp(targetContainerPosition, ref containerVelocity, scrollSmoothing);
	}

	private void UpdateScrollAmount(int inputSign)
	{
		int listSize = SaveManager.PresetCount;

		if (listSize <= pageSize)
		{
			// Disable scrolling
			scrollAmount = 0;
			scrollRatio = 0;
			//cursorPosition = VerticalSelection;
		}
		else
		{
			if (VerticalSelection == 0 || VerticalSelection == listSize - 1)
				cursorPosition = scrollAmount = VerticalSelection;
			else if ((inputSign < 0 && cursorPosition == 0) || (inputSign > 0 && cursorPosition == 3))
				scrollAmount += inputSign;
			else
				cursorPosition += inputSign;

			scrollAmount = Mathf.Clamp(scrollAmount, 0, listSize - pageSize);
			scrollRatio = (float)VerticalSelection / (SaveManager.PresetCount - 1);
			//cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
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
			presetList[i].Reset();
			GD.Print("LOADING PRESET " + i);

			presetList[i].presetName = SaveManager.ActiveGameData.presetNames[i];
			presetList[i].skills = SaveManager.ActiveGameData.presetSkills[i];
			presetList[i].skillAugments = SaveManager.ActiveGameData.presetSkillAugments[i];

			presetList[i].Initialize();
		}

		isInitialized = true;
		presetList[VerticalSelection].SelectRight();
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign == 0)
			return;

		if (isSubMenuActive && isEditingName == false)
		{
			subIndex = WrapSelection(subIndex + inputSign, 5);
			MoveSubCursor();
			return;
		}



		if (isEditingName == false)
		{
			presetList[VerticalSelection].DeselectInstant();
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, presetList.Count);
			MoveCursor(inputSign < 0 ? Direction.Up : Direction.Down, VerticalSelection);
		}

		UpdateScrollAmount(inputSign);

		GD.Print("Selected index ", VerticalSelection);
	}

	protected override void Confirm()
	{
		if (isSubMenuActive && isEditingName == false)
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
					if (!IsInvalid(VerticalSelection))
						RenamePreset();
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
		else if (isEditingName)
		{
			Enter();
		}
		else
		{
			// Show the submenu
			subIndex = 0;
			MoveSubCursor();
			animatorOptions.Play("show");
			isSubMenuActive = true;
		}


	}

	protected override void Enter()
	{
		if (isEditingName)
		{
			if (nameEditor.Text == "")
				presetList[VerticalSelection].presetName = "New Preset";

			presetList[VerticalSelection].presetName = nameEditor.Text;
			SaveSkills(VerticalSelection);
			isEditingName = false;
			animatorNameEditor.Play("hide");
		}
	}

	protected override void Cancel()
	{
		if (isSubMenuActive && isEditingName == false)
		{
			animatorOptions.Play("hide");
			isSubMenuActive = false;
		}
		else if (isEditingName)
		{
			animatorNameEditor.Play("hide");
			isEditingName = false;
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
					animatorOptionsSelector.Play("select-save");
				else
					animatorOptionsSelector.Play("select-save-invalid");
				break;
			case 1:
				if (!IsInvalid(VerticalSelection))
					animatorOptionsSelector.Play("select-load");

				else
					animatorOptionsSelector.Play("select-load-invalid");
				break;
			case 2:
				if (!IsInvalid(VerticalSelection))
					animatorOptionsSelector.Play("select-rename");
				else
					animatorOptionsSelector.Play("select-rename-invalid");
				break;
			case 3:
				if (!IsInvalid(VerticalSelection))
					animatorOptionsSelector.Play("select-delete");
				else
					animatorOptionsSelector.Play("select-delete-invalid");
				break;
			case 4:
				if (!IsInvalid(VerticalSelection))
					animatorOptionsSelector.Play("select-cancel");
				else
					animatorOptionsSelector.Play("select-cancel-invalid");
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
		if (string.IsNullOrEmpty(presetList[preset].presetName) && subIndex == 0)
			presetList[preset].presetName = "New Preset";

		presetList[preset].presetName = presetList[preset].presetName.Replace("\n", ""); //when we edit names, remove the newline code

		presetList[preset].skills = SaveManager.ActiveGameData.equippedSkills.Duplicate();
		presetList[preset].skillAugments = SaveManager.ActiveGameData.equippedAugments.Duplicate();

		SaveManager.ActiveGameData.presetNames[preset] = presetList[preset].presetName;
		SaveManager.ActiveGameData.presetSkills[preset] = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.presetSkillAugments[preset] = presetList[preset].skillAugments.Duplicate();


		// Save our new data to the file and play the animation to initialize the on-screen data
		if (subIndex == 0 || subIndex == 1) //Only play the save animation if we are selecting save or load, otherewise just display the data
			presetList[preset].SavePreset();
		else
			presetList[preset].Initialize();

		SaveManager.SaveGameData();
		MoveSubCursor(); //After saving, change the option box colors

		foreach (SkillPresetOption option in presetList)
		{
			GD.PrintT(option.presetName, option.skills, option.skillAugments);
		}
	}

	private void LoadSkills(int preset)
	{
		SaveManager.ActiveGameData.equippedSkills = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.equippedAugments = presetList[preset].skillAugments.Duplicate();
		activeSkillRing.UpdateTotalCost();

		presetList[preset].SelectPreset();
	}

	private void RenamePreset()
	{
		isEditingName = true;
		nameEditor.Text = "New Preset";
		animatorNameEditor.Play("show");
	}

	private void DeletePreset(int preset)
	{
		// A null/empty preset means it's already been deleted.
		if (string.IsNullOrEmpty(SaveManager.ActiveGameData.presetNames[preset]))
			return;

		presetList[preset].presetName = "";
		presetList[preset].skills = null;
		presetList[preset].skillAugments = null;

		SaveManager.ActiveGameData.presetNames[preset] = "";
		SaveManager.ActiveGameData.presetSkills[preset] = null;
		SaveManager.ActiveGameData.presetSkillAugments[preset] = null;

		SaveManager.SaveGameData();
		presetList[preset].Initialize();
		//SaveSkills(preset);

		MoveSubCursor();//Grays out the options menu 
	}

	private bool IsInvalid(int index) => presetList[index].IsInvalid;
}
