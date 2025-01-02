using Godot;
using System;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

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

	//private Array<SkillPreset> currentPresets = [];
	private Array<string> currentPresetNames = [];
	private Array<Array<SkillKey>> currentPresetSkills = [];
	private Array<Dictionary<SkillKey, int>> currentPresetAugments = [];

	private bool isInitialized;

	public bool isSubMenuActive;
	private int subIndex;

	public override void ShowMenu()
	{
		GD.Print("Showing presets");
		animator.Play("show");
		LoadPresets();

		base.ShowMenu();
	}

	public void LoadPresets()
	{
		GD.Print("Loading Presets");

		if (!isInitialized)
		{
			presetList = new Array<SkillPresetOption>();
			//currentPresets = new Array<SkillPreset>();
			currentPresetNames = new Array<string>();
			currentPresetSkills = new Array<Array<SkillKey>>();
			currentPresetAugments = new Array<Dictionary<SkillKey, int>>();
			for (int i = 0; i < PresetsSlots; i++)
			{
				//presetList.Add(null);
				currentPresetNames.Add("");
				currentPresetSkills.Add(null);
				currentPresetAugments.Add(null);
				//currentPresets.Add(new SkillPreset("", null, null));
				SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();


				if (SaveManager.ActiveGameData.presetSkills[i] != null &&
					SaveManager.ActiveGameData.presetSkillAugments[i] != null &&
					SaveManager.ActiveGameData.presetNames[i] != "")
				{
					GD.Print("LOADING PRESET " + i);

					currentPresetNames[i] = SaveManager.ActiveGameData.presetNames[i];
					currentPresetSkills[i] = SaveManager.ActiveGameData.presetSkills[i];
					currentPresetAugments[i] = SaveManager.ActiveGameData.presetSkillAugments[i];

					newPreset.presetName = currentPresetNames[i];
					newPreset.skills = currentPresetSkills[i];
					newPreset.skillAugments = currentPresetAugments[i];

					//newPreset.thisPreset = currentPresets[i];
					//GD.Print("PRESET " + i + ":");
					//GD.Print(newPreset.thisPreset.presetName.ToString());
					//GD.Print(newPreset.thisPreset.skills.ToString());
					//GD.Print(newPreset.thisPreset.skillAugments.ToString());

				}
				else
				{
					currentPresetNames[i] = "";
					currentPresetSkills[i] = null;
					currentPresetAugments[i] = null;

				}



				presetList.Add(newPreset);

				presetContainer.AddChild(newPreset);
				presetList[i].Index = i + 1;//for displaying the number

				presetList[i].Initialize();
			}
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
		if (string.IsNullOrEmpty(currentPresetNames[preset]) &&
			currentPresetSkills[preset] == null &&
			currentPresetAugments[preset] == null)
		{
			currentPresetNames[preset] = "New Preset";
			currentPresetSkills[preset] = [];
			currentPresetAugments[preset] = [];
		}


		//Storing our equipped skills into the temporary preset
		currentPresetSkills[preset] = SaveManager.ActiveGameData.equippedSkills;
		currentPresetAugments[preset] = SaveManager.ActiveGameData.equippedAugments;


		//Set a new name if our current one is empty
		if (currentPresetNames[preset] == null || currentPresetNames[preset] == "")
			currentPresetNames[preset] = "New Preset";

		//Sets the preset selection object to the saved temporary preset
		presetList[preset].presetName = currentPresetNames[preset];
		presetList[preset].skills = currentPresetSkills[preset];
		presetList[preset].skillAugments = currentPresetAugments[preset];

		//Turns the class back into separate data
		//SaveManager.ActiveGameData.FromSkillPreset(currentPresets[preset], preset); 
		SaveManager.ActiveGameData.presetNames[preset] = currentPresetNames[preset];
		SaveManager.ActiveGameData.presetSkills[preset] = currentPresetSkills[preset];
		SaveManager.ActiveGameData.presetSkillAugments[preset] = currentPresetAugments[preset];

		//Save our new data to the file and play the animation to initialize the on-screen data
		presetList[preset].SavePreset();
		SaveManager.SaveGameData();


	}

	private void LoadSkills(int preset)
	{


		SaveManager.ActiveGameData.equippedSkills = currentPresetSkills[preset];
		SaveManager.ActiveGameData.equippedAugments = currentPresetAugments[preset];

		skillSelectMenu.Redraw();
		presetList[preset].SelectPreset();


	}

	private void RenamePreset()
	{

	}

	private void DeletePreset(int preset)
	{
		if (SaveManager.ActiveGameData.presetNames[preset] != "" &&
			SaveManager.ActiveGameData.presetSkills[preset] != null &&
			SaveManager.ActiveGameData.presetSkillAugments[preset] != null)
		{
			currentPresetNames[preset] = "";
			currentPresetSkills[preset] = null;
			currentPresetAugments[preset] = null;

			presetList[preset].presetName = currentPresetNames[preset];
			presetList[preset].skills = currentPresetSkills[preset];
			presetList[preset].skillAugments = currentPresetAugments[preset];
			//currentPresets[preset].SetName("");
			//currentPresets[preset].SetSkills(null);
			//currentPresets[preset].SetSkills(null);
			//presetList[preset].thisPreset.SetPreset(currentPresets[preset]);

			//SaveManager.ActiveGameData.FromSkillPreset(currentPresets[preset],preset);


			SaveManager.SaveGameData();
			presetList[preset].Initialize();
		}

	}

	private bool IsInvalid(int index)
	{
		if (currentPresetNames[index] == "" &&
			currentPresetSkills[index] == null &&
			currentPresetAugments[index] == null)
			return true;
		else
			return false;

	}


}
