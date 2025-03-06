using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class PauseMenu : Node
{
	public static bool AllowPausing = true;

	[Signal] public delegate void OnSceneChangeSelectedEventHandler();

	[Export] AnimationTree animator;
	[Export] private Node2D pauseCursor;
	[Export] private AnimationPlayer pauseCursorAnimator;
	[Export] private AudioStreamPlayer selectSFX;
	[Export] private Menus.Description description;

	[ExportGroup("Status Menu")]
	[Export] private Node2D statusCursor;
	[Export] private AnimationPlayer statusCursorAnimator;
	[Export] private Label[] values;

	[ExportSubgroup("Mission Menu")]
	[Export] private Label missionTypeLabel;
	[Export] private Label missionDescriptionLabel;
	[Export] private Control fireSoulParent;
	[Export] private Sprite2D[] fireSoulSprites;
	[Export] private Sprite2D rankSprite;

	[ExportSubgroup("Skill Menu")]
	[Export] private Label noSkillLabel;
	[Export] private VBoxContainer skillContainer;
	[Export] private Control skillCursor;
	[Export] private AnimationPlayer skillCursorAnimator;
	[Export] private PackedScene pauseSkillScene;
	[Export] private int[] rectVerticalValues;
	[Export] private Sprite2D levelSprite;
	private PauseSkill[] skills;
	private int skillScrollOffset;
	[Export] private Sprite2D skillScrollbar;
	private Vector2 scrollVelocity;
	private readonly float ScrollSmoothing = .05f;

	private bool isActive;
	private enum Submenu
	{
		Pause,
		Status,
		Skill
	}
	private Submenu submenu;
	private StageSettings Stage => StageSettings.Instance;

	private readonly StringName SelectionTransition = "parameters/selection/transition_request";
	private readonly StringName ConfirmTransition = "parameters/confirm/transition_request";
	private readonly StringName ConfirmEnabledTransition = "parameters/confirm_enabled/transition_request";
	private readonly StringName StateRequest = "parameters/state/transition_request";
	private readonly StringName ShowTrigger = "parameters/show_trigger/request";

	private readonly StringName StatusShowTrigger = "parameters/status_show_trigger/request";
	private readonly StringName StatusHideTrigger = "parameters/status_hide_trigger/request";
	private readonly StringName StatusSelectionTransition = "parameters/status_selection/transition_request";
	private readonly StringName ValueSelectionTransition = "parameters/value_selection/transition_request";
	private readonly StringName SubmenuParameter = "parameters/submenu_transition/transition_request";

	private bool isConfirmButtonBuffered;
	private bool isCancelButtonBuffered;

	public override void _Ready()
	{
		animator.Active = true;
		levelSprite.RegionRect = new
		(
			new(levelSprite.RegionRect.Position.X, rectVerticalValues[(int)SaveManager.ActiveGameData.lastPlayedWorld]),
			levelSprite.RegionRect.Size
		);

		noSkillLabel.Visible = SaveManager.ActiveSkillRing.EquippedSkills.Count == 0;
		skillScrollbar.GetParent<Node2D>().Visible = SaveManager.ActiveSkillRing.EquippedSkills.Count != 0;

		skills = new PauseSkill[SaveManager.ActiveSkillRing.EquippedSkills.Count];
		skillContainer.SetDeferred("size", new Vector2(skillContainer.Size.X, skills.Length * 60));

		// Generate skill list
		foreach (SkillKey key in SaveManager.ActiveSkillRing.EquippedSkills)
		{
			PauseSkill pauseSkill = pauseSkillScene.Instantiate<PauseSkill>();
			pauseSkill.Skill = Runtime.Instance.SkillList.GetSkill(key);
			if (pauseSkill.Skill.HasAugments)
				pauseSkill.Skill = pauseSkill.Skill.GetAugment(SaveManager.ActiveSkillRing.GetAugmentIndex(key));
			pauseSkill.Initialize();
			skillContainer.AddChild(pauseSkill);
		}
	}

	public override void _PhysicsProcess(double _)
	{
		if (!AllowPausing || !Stage.IsLevelIngame || TransitionManager.IsTransitionActive) return;

		if (skillScrollbar.IsVisibleInTree())
		{
			float denominator = SaveManager.ActiveSkillRing.EquippedSkills.Count - 1;
			if (denominator > 0)
			{
				float targetPosition = Mathf.Lerp(-80, 80, skillSelection / denominator);
				skillScrollbar.Position = skillScrollbar.Position.SmoothDamp(Vector2.Right * targetPosition, ref scrollVelocity, ScrollSmoothing);
			}
		}

		if (Input.IsActionJustPressed("button_pause") ||
			(Input.IsActionJustPressed("ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen")))
		{
			TogglePause();
			return;
		}

		if (!GetTree().Paused)
			return;

		UpdateBuffers();

		if (!canMoveCursor)
			return;

		if (isConfirmButtonBuffered)
		{
			Confirm();
			return;
		}

		if (isCancelButtonBuffered)
		{
			Cancel();
			return;
		}

		int sign = Mathf.Sign(Input.GetAxis("ui_up", "ui_down"));
		if (sign != 0)
			ChangeSelection(sign);
	}

	private void UpdateBuffers()
	{
		if (Input.IsActionJustPressed("button_jump") || Input.IsActionJustPressed("ui_select"))
		{
			isConfirmButtonBuffered = true;
			isCancelButtonBuffered = false;
		}

		if (Input.IsActionJustPressed("button_action") || Input.IsActionJustPressed("ui_cancel"))
		{
			isConfirmButtonBuffered = false;
			isCancelButtonBuffered = true;
		}
	}

	private void Confirm()
	{
		isConfirmButtonBuffered = false;
		if (submenu == Submenu.Status && currentSelection == 2 && skills.Length != 0) // Enter skill menu
		{
			ApplySelection();
			return;
		}

		if (submenu != Submenu.Pause)
			return;

		AllowPausing = false;
		if (currentSelection != 2)
		{
			animator.Set(ConfirmTransition, currentSelection.ToString());
			animator.Set(ConfirmEnabledTransition, "true");
			return;
		}

		ApplySelection();
	}

	private void Cancel()
	{
		isCancelButtonBuffered = false;
		switch (submenu)
		{
			case Submenu.Pause:
				TogglePause();
				break;
			case Submenu.Status:
				AllowPausing = false;
				statusCursorAnimator.Play("hide");
				description.HideDescription();
				animator.Set(StatusHideTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				break;
			case Submenu.Skill:
				skillCursorAnimator.Play("hide");
				submenu = Submenu.Status;
				currentSelection = 2;
				break;
		}
	}

	private void ChangeSelection(int direction)
	{
		int targetSelection = currentSelection + direction;
		if (submenu == Submenu.Skill) // Allow wrapping when viewing skills
		{
			if (targetSelection < 0)
				targetSelection = skills.Length - 1;
			else if (targetSelection == skills.Length)
				targetSelection = 0;
		}
		else
		{
			targetSelection = Mathf.Clamp(targetSelection, 0, submenu == Submenu.Pause ? 3 : 2);
		}

		if (targetSelection != currentSelection)
			UpdateSelection(targetSelection, true);
	}

	/// <summary> Actually applies the current selection (called from the animator). </summary>
	private void ApplySelection()
	{
		if (submenu == Submenu.Pause)
		{
			switch (currentSelection)
			{
				case 0: // Resume
					isActive = false;
					ApplyPause();
					break;
				case 1: // Restart
					TransitionManager.instance.QueuedScene = string.Empty;
					EmitSignal(SignalName.OnSceneChangeSelected);
					break;
				case 2: // Status menu
					submenu = Submenu.Status;
					pauseCursorAnimator.Play("hide");
					animator.Set(SubmenuParameter, "status");
					animator.Set(StatusShowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
					UpdateSelection(0);
					UpdateCursorPosition();
					UpdateStatusMenuData();
					break;
				case 3: // Open EXP menu
					SaveManager.SaveGameData();
					TransitionManager.instance.QueuedScene = TransitionManager.MENU_SCENE_PATH;
					EmitSignal(SignalName.OnSceneChangeSelected);
					break;
			}
		}
		else if (submenu == Submenu.Status && currentSelection == 2) // Enter skill menu
		{
			submenu = Submenu.Skill;
			skillCursorAnimator.Play("select");
			skillCursorAnimator.Advance(0.0);
			UpdateSelection(skillSelection); // Remember previously selected skill
			UpdateCursorPosition();
		}
	}

	private void CancelSelection()
	{
		submenu = Submenu.Pause;
		UpdateSelection(2);
		animator.Set(SubmenuParameter, "pause");
	}

	private void UpdateStatusMenuData()
	{
		// Status menu
		values[0].Text = SaveManager.ActiveGameData.level.ToString("00");
		values[1].Text = "×" + Stage.CurrentRingCount.ToString("000") + "/999";
		values[2].Text = ExtensionMethods.FormatMenuNumber(SaveManager.ActiveGameData.GetHighScore(Stage.Data.LevelID));
		values[3].Text = ExtensionMethods.FormatMenuNumber(Stage.TotalScore);
		values[4].Text = ExtensionMethods.FormatTime(SaveManager.ActiveGameData.GetBestTime(Stage.Data.LevelID));
		values[5].Text = Stage.DisplayTime;
		values[6].Text = ExtensionMethods.FormatMenuNumber(Stage.CurrentEXP);
		values[7].Text = ExtensionMethods.FormatMenuNumber(SaveManager.ActiveGameData.exp);
		values[8].Text = StageSettings.Player.Skills.TextDisplay;

		// Mission menu
		missionTypeLabel.Text = Stage.Data.MissionTypeKey;
		missionDescriptionLabel.Text = Stage.Data.MissionDescriptionKey;

		fireSoulParent.Visible = Stage.Data.HasFireSouls;
		if (Stage.Data.HasFireSouls)
		{
			for (int i = 0; i < fireSoulSprites.Length; i++)
			{
				bool isSaveCollected = SaveManager.ActiveGameData.IsFireSoulCollected(Stage.Data.LevelID, i + 1);
				bool isCheckpointCollected = StageSettings.Instance.fireSoulCheckpoints[i];

				fireSoulSprites[i].RegionRect = new(new(isSaveCollected || isCheckpointCollected ? 450 : 400, fireSoulSprites[i].RegionRect.Position.Y), fireSoulSprites[i].RegionRect.Size);
				fireSoulSprites[i].SelfModulate = isCheckpointCollected ? new(1f, 1f, 1f, .5f) : Colors.White;
			}
		}

		int rank = SaveManager.ActiveGameData.GetRankClamped(Stage.Data.LevelID);
		rankSprite.RegionRect = new(new(rankSprite.RegionRect.Position.X, 110 + (60 * rank)), rankSprite.RegionRect.Size);
	}

	/// <summary> Selected menu option. </summary>
	private int currentSelection;
	/// <summary> Current Selected skill option. </summary>
	private int skillSelection;
	/// <summary> Can the cursor currently be moved? </summary>
	private bool canMoveCursor;
	private void EnableCursorMovement()
	{
		canMoveCursor = true;
		if (submenu == Submenu.Skill)
			skillCursorAnimator.Play("loop");
	}
	private void UpdateCursorPosition()
	{
		switch (submenu)
		{
			case Submenu.Pause:
				pauseCursor.Position = Vector2.Down * currentSelection * 32;
				pauseCursorAnimator.Play("show");
				pauseCursorAnimator.Seek(0.0, true);
				break;
			case Submenu.Status:
				statusCursor.Position = Vector2.Down * currentSelection * 32;
				statusCursorAnimator.Play("show");
				statusCursorAnimator.Seek(0.0, true);
				break;
			case Submenu.Skill:
				int visualSelection = skillSelection - skillScrollOffset;
				if (skillSelection > skillScrollOffset + 6)
				{
					visualSelection = 6;
					skillScrollOffset = skillSelection - visualSelection;
				}
				else if (skillSelection < skillScrollOffset)
				{
					visualSelection = 0;
					skillScrollOffset = skillSelection;
				}

				skillContainer.Position = Vector2.Up * skillScrollOffset * 60;
				skillCursor.Position = Vector2.Down * visualSelection * 60;
				skillCursorAnimator.Play("show");
				skillCursorAnimator.Seek(0.0, true);
				break;
		}
	}

	private void UpdateSelection(int selection, bool playSFX = default)
	{
		if (playSFX)
			selectSFX.Play();

		canMoveCursor = false;
		currentSelection = selection;

		switch (submenu)
		{
			case Submenu.Pause:
				pauseCursorAnimator.Play("move");
				animator.Set(SelectionTransition, selection.ToString());
				break;
			case Submenu.Status:
				statusCursorAnimator.Play("move");
				animator.Set(ValueSelectionTransition, selection.ToString());
				animator.Set(StatusSelectionTransition, selection.ToString());
				UpdateStatusDescription();
				break;
			case Submenu.Skill:
				skillSelection = selection;
				UpdateCursorPosition();
				UpdateSkillDescription();
				break;
		}
	}

	private void UpdateStatusDescription()
	{
		description.Text = currentSelection switch
		{
			0 => "pause_status_description",
			1 => "pause_mission_description",
			_ => "pause_skill_description",
		};
		description.ShowDescription();
	}

	private void UpdateSkillDescription()
	{
		PauseSkill pauseSkill = skillContainer.GetChild<PauseSkill>(currentSelection);
		description.Text = pauseSkill.Skill.DescriptionKey;
		description.ShowDescription();
	}

	private void EnableInteraction() => AllowPausing = true;
	private float unpausedSpeed;
	private void TogglePause()
	{
		canMoveCursor = false; // Disable cursor movement
		AllowPausing = false; // Disable pause inputs during the animation

		isActive = !isActive;
		animator.Set(ConfirmEnabledTransition, "false");
		if (submenu == Submenu.Pause)
			animator.Set(StateRequest, isActive ? "show" : "hide");
		else
			animator.Set(StateRequest, "status-hide");

		if (isActive) // Reset selection
		{
			// Reset cursors
			skillCursorAnimator.Play("RESET");

			UpdateSelection(0);
			UpdateCursorPosition();
			animator.Set(ShowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			unpausedSpeed = (float)Engine.TimeScale;
			Engine.TimeScale = 1.0f;
		}
		else if (!TransitionManager.IsTransitionActive)
		{
			Engine.TimeScale = unpausedSpeed;
		}
	}

	private void ApplyPause()
	{
		if (submenu != Submenu.Pause)
		{
			UpdateSelection(0); // Select Continue
			CancelSelection();
		}

		GetTree().Paused = isActive;
		BGMPlayer.StageMusicPaused = isActive;
	}
}