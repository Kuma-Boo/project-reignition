using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SkillPresetSelect : Menu
{
	[Export] private PackedScene presetOption;
	[Export] private VBoxContainer presetContainer;

	[Export]
	private Node2D cursor;
	[Export] private Sprite2D scrollbar;

	[Export] private Label saveLabel; // We're changing this to "overwrite" if a save already exists

	[Export] private LineEdit nameEditor;

	[Export] private AnimationPlayer animatorOptions;
	[Export] private AnimationPlayer animatorNameEditor;

	[Export] private AudioStreamPlayer confirmSFX;
	[Export] private AudioStreamPlayer cancelSFX;
	[Export] private AudioStreamPlayer selectSFX;
	[Export] private AudioStreamPlayer failSFX;

	private SkillRing ActiveSkillRing => SaveManager.ActiveSkillRing;

	private int scrollAmount;
	private float scrollRatio;
	private Vector2 scrollVelocity;
	private Vector2 containerVelocity;
	private const float ScrollSmoothing = .1f;
	private readonly int ScrollInterval = 240;
	private readonly int PageSize = 4;

	private int cursorPosition;
	private Vector2 cursorVelocity;
	private const float CursorSmoothing = .1f;

	private Array<SkillPresetOption> presetList = [];

	public bool isSubMenuActive;

	public bool isEditingName;
	private int subIndex;

	protected override void SetUp()
	{
		//  Create Preset Option nodes
		for (int i = 0; i < SaveManager.PresetCount; i++)
		{
			SkillPresetOption newPreset = presetOption.Instantiate<SkillPresetOption>();
			newPreset.DisplayNumber = i + 1; //  For displaying the number

			presetList.Add(newPreset);
			presetContainer.AddChild(newPreset);
		}
	}

	public override void _Process(double _)
	{
		float targetScrollPosition = (160 * scrollRatio) - 80;
		scrollbar.Position = scrollbar.Position.SmoothDamp(Vector2.Right * targetScrollPosition, ref scrollVelocity, ScrollSmoothing);

		//  Update cursor position
		float targetCursorPosition = cursorPosition * ScrollInterval;
		cursor.Position = cursor.Position.SmoothDamp(Vector2.Down * targetCursorPosition, ref cursorVelocity, CursorSmoothing);

		Vector2 targetContainerPosition = new(presetContainer.Position.X, -scrollAmount * ScrollInterval);
		presetContainer.Position = presetContainer.Position.SmoothDamp(targetContainerPosition, ref containerVelocity, ScrollSmoothing);
	}

	protected override void ProcessMenu()
	{
		if (isEditingName)
		{
			if (Input.IsKeyPressed(Key.Enter) || Input.IsActionJustPressed("button_pause"))
			{
				Rename();
				return;
			}

			if (Input.IsKeyPressed(Key.Escape))
			{
				StopRenaming();
				return;
			}
		}

		if (Input.IsActionJustPressed("button_pause"))
			Confirm();

		base.ProcessMenu();
	}

	private void UpdateScrollAmount(int inputSign)
	{
		int listSize = presetList.Count;

		if (listSize <= PageSize)
		{
			//  Disable scrolling
			scrollAmount = 0;
			scrollRatio = 0;
			return;
		}

		if (VerticalSelection == 0 || VerticalSelection == listSize - 1)
			cursorPosition = scrollAmount = VerticalSelection;
		else if ((inputSign < 0 && cursorPosition == 0) || (inputSign > 0 && cursorPosition == 3))
			scrollAmount += inputSign;
		else
			cursorPosition += inputSign;

		scrollAmount = Mathf.Clamp(scrollAmount, 0, listSize - PageSize);
		scrollRatio = (float)VerticalSelection / (listSize - 1);
		cursorPosition = Mathf.Clamp(cursorPosition, 0, PageSize - 1);
	}

	public override void ShowMenu()
	{
		animator.Play("show");
		LoadPresets();

		base.ShowMenu();
	}

	public void LoadPresets()
	{
		for (int i = 0; i < presetList.Count; i++)
		{
			presetList[i].Reset();

			presetList[i].presetName = SaveManager.ActiveGameData.presetNames[i];
			presetList[i].skills = SaveManager.ActiveGameData.presetSkills[i];
			presetList[i].skillAugments = SaveManager.ActiveGameData.presetSkillAugments[i];

			presetList[i].Initialize();
		}

		presetList[VerticalSelection].SelectRight();
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign == 0)
			return;

		if (!isEditingName)
		{
			if (isSubMenuActive)
			{
				subIndex = WrapSelection(subIndex + inputSign, 5);
				MoveSubCursor();
				return;
			}

			presetList[VerticalSelection].DeselectInstant();
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, presetList.Count);
			selectSFX.Play();
			MoveCursor(inputSign, VerticalSelection);
		}

		UpdateScrollAmount(inputSign);
	}

	protected override void Confirm()
	{
		if (isEditingName)
			return;

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
					else
						failSFX.Play();
					break;
				case 2:
					if (!IsInvalid(VerticalSelection))
						StartRenaming();
					else
						failSFX.Play();
					break;
				case 3:
					if (!IsInvalid(VerticalSelection))
						DeletePreset(VerticalSelection);
					else
						failSFX.Play();
					break;
				case 4:
					animatorOptions.Play("hide");
					isSubMenuActive = false;
					break;
			}

			return;
		}

		//  Show the submenu
		subIndex = 0;

		animatorOptions.Play(IsInvalid(VerticalSelection) ? "select-save-invalid" : "select-save");
		animatorOptions.Advance(0.0);
		animatorOptions.Play("show");
		isSubMenuActive = true;
	}

	private void Rename()
	{
		if (string.IsNullOrEmpty(nameEditor.Text))
			presetList[VerticalSelection].presetName = "New Preset";

		confirmSFX.Play();
		presetList[VerticalSelection].presetName = nameEditor.Text;
		SaveSkills(VerticalSelection);
		isEditingName = false;
		animatorNameEditor.Play("hide");
	}

	protected override void Cancel()
	{
		if (isEditingName)
			return;

		if (isSubMenuActive)
		{
			animatorOptions.Play("hide");
			isSubMenuActive = false;
		}
		else
		{
			// Return to skill editing
			animator.Play("hide");
			OpenParentMenu();
		}
	}

	public void MoveSubCursor()
	{
		string targetAnimation = string.Empty;
		switch (subIndex)
		{
			case 0:
				targetAnimation = "select-save";
				break;
			case 1:
				targetAnimation = "select-load";
				break;
			case 2:
				targetAnimation = "select-rename";
				break;
			case 3:
				targetAnimation = "select-delete";
				break;
			case 4:
				targetAnimation = "select-cancel";
				break;
		}

		if (IsInvalid(VerticalSelection))
			targetAnimation += "-invalid";

		animatorOptions.Play(targetAnimation);

		selectSFX.Play();
		StartSelectionTimer();
	}

	private void MoveCursor(int dir, int index)
	{
		if (dir < 0)
			presetList[index].SelectUp();
		else
			presetList[index].SelectDown();

		StartSelectionTimer();
	}

	private void SaveSkills(int preset)
	{
		//  Storing our equipped skills into our current preset
		if (string.IsNullOrEmpty(presetList[preset].presetName) && subIndex == 0)
			presetList[preset].presetName = "New Preset";

		presetList[preset].presetName = presetList[preset].presetName.Replace("\n", ""); // when we edit names, remove the newline code

		presetList[preset].skills = SaveManager.ActiveGameData.equippedSkills.Duplicate();
		presetList[preset].skillAugments = SaveManager.ActiveGameData.equippedAugments.Duplicate();

		SaveManager.ActiveGameData.presetNames[preset] = presetList[preset].presetName;
		SaveManager.ActiveGameData.presetSkills[preset] = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.presetSkillAugments[preset] = presetList[preset].skillAugments.Duplicate();

		//  Save our new data to the file and play the animation to initialize the on-screen data
		if (subIndex == 0 || subIndex == 1) // Only play the save animation if we are selecting save or load, otherewise just display the data
			presetList[preset].SavePreset();
		else
			presetList[preset].Initialize();

		SaveManager.SaveGameData();
		animatorOptions.Play("select-save");
	}

	private void LoadSkills(int preset)
	{
		SaveManager.ActiveGameData.equippedSkills = presetList[preset].skills.Duplicate();
		SaveManager.ActiveGameData.equippedAugments = presetList[preset].skillAugments.Duplicate();
		ActiveSkillRing.LoadFromActiveData();

		presetList[preset].SelectPreset();
	}

	private void StartRenaming()
	{
		isEditingName = true;
		confirmSFX.Play();

		nameEditor.Text = SaveManager.ActiveGameData.presetNames[VerticalSelection];
		nameEditor.CaretColumn = nameEditor.Text.Length;
		nameEditor.SelectAll();
		animatorNameEditor.Play("show");
	}

	private void StopRenaming()
	{
		cancelSFX.Play();
		animatorNameEditor.Play("hide");
		isEditingName = false;
	}

	private void DeletePreset(int preset)
	{
		//  A null/empty preset means it's already been deleted.
		if (string.IsNullOrEmpty(SaveManager.ActiveGameData.presetNames[preset]))
			return;

		confirmSFX.Play();

		presetList[preset].presetName = "";
		presetList[preset].skills = null;
		presetList[preset].skillAugments = null;

		SaveManager.ActiveGameData.presetNames[preset] = "";
		SaveManager.ActiveGameData.presetSkills[preset] = null;
		SaveManager.ActiveGameData.presetSkillAugments[preset] = null;

		SaveManager.SaveGameData();
		presetList[preset].Initialize();

		animatorOptions.CurrentAnimation = "select-delete-invalid";
		animatorOptions.Seek(0.0, true, true); // Grays out the options menu
	}

	private bool IsInvalid(int index) => presetList[index].IsInvalid;
}
