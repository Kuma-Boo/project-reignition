using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class PauseMenu : Node
{
	public static bool AllowPausing = true;

	[Export]
	private AnimationTree animator;
	[Export]
	private Node2D pauseCursor;
	[Export]
	private AnimationPlayer pauseCursorAnimator;
	[Export]
	private AudioStreamPlayer selectSFX;
	[Export]
	private Menus.Description description;

	[ExportGroup("Status Menu")]
	[Export]
	private Node2D statusCursor;
	[Export]
	private AnimationPlayer statusCursorAnimator;
	[Export]
	private Label[] values;

	[ExportSubgroup("Mission Menu")]
	[Export]
	private Label missionTypeLabel;
	[Export]
	private Label missionDescriptionLabel;
	[Export]
	private Control fireSoulParent;
	[Export]
	private Sprite2D[] fireSoulSprites;
	[Export]
	private Sprite2D rankSprite;

	[ExportSubgroup("Skill Menu")]
	[Export]
	private Label noSkillLabel;
	[Export]
	private VBoxContainer skillContainer;
	[Export]
	private Control skillCursor;
	[Export]
	private AnimationPlayer skillCursorAnimator;
	[Export]
	private PackedScene pauseSkillScene;
	[Export]
	private int[] rectVerticalValues;
	[Export]
	private Sprite2D levelSprite;
	private PauseSkill[] skills;
	private int skillScrollOffset;

	private bool isActive;
	private enum Submenu
	{
		PAUSE,
		STATUS,
		SKILL
	}
	private Submenu submenu;

	private readonly StringName SelectionTransition = "parameters/selection/transition_request";
	private readonly StringName ConfirmTransition = "parameters/confirm/transition_request";
	private readonly StringName ConfirmEnabledTransition = "parameters/confirm_enabled/transition_request";
	private readonly StringName StateRequest = "parameters/state/transition_request";
	private readonly StringName ShowTrigger = "parameters/show_trigger/request";

	private readonly StringName StatusShowTrigger = "parameters/status_show_trigger/request";
	private readonly StringName StatusHideTrigger = "parameters/status_hide_trigger/request";
	private readonly StringName StatusSelectionTransition = "parameters/status_selection/transition_request";
	private readonly StringName ValueSelectionTransition = "parameters/value_selection/transition_request";

	public override void _Ready()
	{
		animator.Active = true;
		levelSprite.RegionRect = new
		(
			new(levelSprite.RegionRect.Position.X, rectVerticalValues[(int)SaveManager.ActiveGameData.lastPlayedWorld]),
			levelSprite.RegionRect.Size
		);

		noSkillLabel.Visible = SaveManager.ActiveSkillRing.EquippedSkills.Count == 0;
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

	public override void _PhysicsProcess(double delta)
	{
		if (!AllowPausing || !Stage.IsLevelIngame || TransitionManager.IsTransitionActive) return;

		if (Input.IsActionJustPressed("button_pause"))
		{
			TogglePause();
		}
		else if (GetTree().Paused && canMoveCursor)
		{
			int sign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
			if (Input.IsActionJustPressed("button_jump"))
			{
				if (submenu == Submenu.PAUSE)
				{
					AllowPausing = false;
					if (currentSelection == 2)
					{
						ApplySelection();
					}
					else
					{
						animator.Set(ConfirmTransition, currentSelection.ToString());
						animator.Set(ConfirmEnabledTransition, "true");
					}
				}
				else if (submenu == Submenu.STATUS && currentSelection == 2 && skills.Length != 0) // Enter skill menu
				{
					ApplySelection();
				}
			}
			else if (Input.IsActionJustPressed("button_action"))
			{
				if (submenu == Submenu.PAUSE)
				{
					TogglePause();
				}
				else if (submenu == Submenu.STATUS)
				{
					AllowPausing = false;
					statusCursorAnimator.Play("hide");
					description.HideDescription();
					animator.Set(StatusHideTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				}
				else
				{
					skillCursorAnimator.Play("hide");
					submenu = Submenu.STATUS;
					currentSelection = 2;
				}
			}
			else if (sign != 0)
			{
				int targetSelection = currentSelection + sign;
				if (submenu == Submenu.SKILL) // Allow wrapping when viewing skills
				{
					if (targetSelection < 0)
						targetSelection = skills.Length - 1;
					else if (targetSelection == skills.Length)
						targetSelection = 0;
				}
				else
				{
					targetSelection = Mathf.Clamp(targetSelection, 0, submenu == Submenu.PAUSE ? 3 : 2);
				}

				if (targetSelection != currentSelection)
					UpdateSelection(targetSelection, true);
			}
		}
	}

	private readonly StringName SUBMENU_PARAMETER = "parameters/submenu_transition/transition_request";
	/// <summary> Actually applies the current selection (called from the animator). </summary>
	private void ApplySelection()
	{
		if (submenu == Submenu.PAUSE)
		{
			switch (currentSelection)
			{
				case 0: // Resume
					isActive = false;
					ApplyPause();
					break;
				case 1: // Restart
					StartSceneTransition(string.Empty);
					break;
				case 2: // Status menu
					submenu = Submenu.STATUS;
					pauseCursorAnimator.Play("hide");
					animator.Set(SUBMENU_PARAMETER, "status");
					animator.Set(StatusShowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
					UpdateSelection(0);
					UpdateCursorPosition();
					UpdateStatusMenuData();
					break;
				case 3: // Return to the main menu
					SaveManager.SaveGameData();
					StartSceneTransition(TransitionManager.MENU_SCENE_PATH);
					break;
			}
		}
		else if (submenu == Submenu.STATUS && currentSelection == 2) // Enter skill menu
		{
			submenu = Submenu.SKILL;
			skillCursorAnimator.Play("select");
			skillCursorAnimator.Advance(0.0);
			UpdateSelection(skillSelection); // Remember previously selected skill
			UpdateCursorPosition();
		}
	}

	private void CancelSelection()
	{
		submenu = Submenu.PAUSE;
		UpdateSelection(2);
		animator.Set(SUBMENU_PARAMETER, "pause");
	}

	private StageSettings Stage => StageSettings.instance;
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
		values[8].Text = CharacterController.instance.Skills.TextDisplay;

		// Mission menu
		missionTypeLabel.Text = Stage.Data.MissionTypeKey;
		missionDescriptionLabel.Text = Stage.Data.MissionDescriptionKey;

		fireSoulParent.Visible = Stage.Data.HasFireSouls;
		if (Stage.Data.HasFireSouls)
		{
			for (int i = 0; i < fireSoulSprites.Length; i++)
			{
				bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(Stage.Data.LevelID, i + 1);
				fireSoulSprites[i].RegionRect = new(new(isCollected ? 450 : 400, fireSoulSprites[i].RegionRect.Position.Y), fireSoulSprites[i].RegionRect.Size);
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
		if (submenu == Submenu.SKILL)
			skillCursorAnimator.Play("loop");
	}
	private void UpdateCursorPosition()
	{
		switch (submenu)
		{
			case Submenu.PAUSE:
				pauseCursor.Position = Vector2.Down * currentSelection * 32;
				pauseCursorAnimator.Play("show");
				pauseCursorAnimator.Seek(0.0, true);
				break;
			case Submenu.STATUS:
				statusCursor.Position = Vector2.Down * currentSelection * 32;
				statusCursorAnimator.Play("show");
				statusCursorAnimator.Seek(0.0, true);
				break;
			case Submenu.SKILL:
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
			case Submenu.PAUSE:
				pauseCursorAnimator.Play("move");
				animator.Set(SelectionTransition, selection.ToString());
				break;
			case Submenu.STATUS:
				statusCursorAnimator.Play("move");
				animator.Set(ValueSelectionTransition, selection.ToString());
				animator.Set(StatusSelectionTransition, selection.ToString());
				UpdateStatusDescription();
				break;
			case Submenu.SKILL:
				skillSelection = selection;
				UpdateCursorPosition();
				UpdateSkillDescription();
				break;
		}
	}

	private void UpdateStatusDescription()
	{
		if (currentSelection == 0)
			description.SetText("pause_status_description");
		else if (currentSelection == 1)
			description.SetText("pause_mission_description");
		else
			description.SetText("pause_skill_description");
		description.ShowDescription();
	}

	private void UpdateSkillDescription()
	{
		PauseSkill pauseSkill = skillContainer.GetChild<PauseSkill>(currentSelection);
		description.SetText(pauseSkill.Skill.DescriptionKey);
		description.ShowDescription();
	}

	private void EnableInteraction() => AllowPausing = true;
	private float unpausedSpeed;
	private void TogglePause()
	{
		canMoveCursor = false; //Disable cursor movement
		AllowPausing = false; //Disable pause inputs during the animation

		isActive = !isActive;
		animator.Set(ConfirmEnabledTransition, "false");
		if (submenu == Submenu.PAUSE)
			animator.Set(StateRequest, isActive ? "show" : "hide");
		else
			animator.Set(StateRequest, "status-hide");

		if (isActive) //Reset selection
		{
			UpdateSelection(0);
			UpdateCursorPosition();
			animator.Set(ShowTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			unpausedSpeed = (float)Engine.TimeScale;
			Engine.TimeScale = 1.0f;
		}
		else
		{
			Engine.TimeScale = unpausedSpeed;
		}
	}

	private void ApplyPause()
	{
		if (submenu != Submenu.PAUSE)
		{
			UpdateSelection(0); // Select Continue
			CancelSelection();
		}

		GetTree().Paused = isActive;
		BGMPlayer.StageMusicPaused = isActive;
	}

	private void StartSceneTransition(string targetScene)
	{
		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
		TransitionManager.QueueSceneChange(targetScene);
		TransitionManager.StartTransition(new()
		{
			inSpeed = .5f,
			outSpeed = .5f,
			color = Colors.Black,
			disableAutoTransition = string.IsNullOrEmpty(targetScene)
		});
	}

	/// <summary>
	/// Unpause and finish scene transition.
	/// </summary>
	private void TransitionFinished() => GetTree().SetDeferred("paused", false);
}