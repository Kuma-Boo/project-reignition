using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
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

		[ExportGroup("Status Menu")]
		[Export]
		private Node2D statusCursor;
		[Export]
		private AnimationPlayer statusCursorAnimator;
		[Export]
		private Label[] values;
		[Export]
		private Sprite2D fireSoulParent;
		[Export]
		private Sprite2D[] fireSoulSprites;
		[Export]
		private Sprite2D rankSprite;
		[Export]
		private Label skillList;
		[Export]
		private Control skillCursor;
		[Export]
		private AnimationPlayer skillCursorAnimator;
		[Export]
		private int[] rectVerticalValues;
		[Export]
		private Sprite2D levelSprite;

		private bool isActive;
		private enum Submenu
		{
			PAUSE,
			STATUS,
			SKILL
		}
		private Submenu submenu;

		private readonly StringName SELECTION_PARAMETER = "parameters/selection/transition_request";
		private readonly StringName CONFIRM_PARAMETER = "parameters/confirm/transition_request";
		private readonly StringName CONFIRM_ENABLED_PARAMETER = "parameters/confirm_enabled/transition_request";
		private readonly StringName STATE_PARAMETER = "parameters/state/transition_request";
		private readonly StringName SHOW_TRIGGER_PARAMETER = "parameters/show_trigger/request";

		private readonly StringName STATUS_SHOW_PARAMETER = "parameters/status_show_trigger/request";
		private readonly StringName STATUS_HIDE_PARAMETER = "parameters/status_hide_trigger/request";
		private readonly StringName STATUS_SELECTION_PARAMETER = "parameters/status_selection/transition_request";
		private readonly StringName VALUE_SELECTION_PARAMETER = "parameters/value_selection/transition_request";


		public override void _Ready()
		{
			animator.Active = true;
			levelSprite.RegionRect = new
			(
				new(levelSprite.RegionRect.Position.X, rectVerticalValues[(int)SaveManager.ActiveGameData.lastPlayedWorld]),
				levelSprite.RegionRect.Size
			);
		}


		public override void _PhysicsProcess(double delta)
		{
			if (!AllowPausing) return;

			if (Input.IsActionJustPressed("button_pause"))
				TogglePause();
			else if (GetTree().Paused && canMoveCursor)
			{
				int sign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
				if (Input.IsActionJustPressed("button_jump"))
				{
					if (submenu == Submenu.PAUSE)
					{
						AllowPausing = false;
						if (currentSelection == 2)
							ApplySelection();
						else
						{
							animator.Set(CONFIRM_PARAMETER, currentSelection.ToString());
							animator.Set(CONFIRM_ENABLED_PARAMETER, "true");
						}
					}
					else if (submenu == Submenu.STATUS && currentSelection == 2) // Enter skill menu
						ApplySelection();
				}
				else if (Input.IsActionJustPressed("button_action"))
				{
					if (submenu == Submenu.PAUSE)
						TogglePause();
					else if (submenu == Submenu.STATUS)
					{
						AllowPausing = false;
						statusCursorAnimator.Play("hide");
						animator.Set(STATUS_HIDE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
							targetSelection = skillList.GetLineCount() - 1;
						else if (targetSelection == skillList.GetLineCount())
							targetSelection = 0;
					}
					else
						targetSelection = Mathf.Clamp(targetSelection, 0, submenu == Submenu.PAUSE ? 3 : 2);

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
						animator.Set(STATUS_SHOW_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
			values[1].Text = "x" + Stage.CurrentRingCount.ToString("000") + "/999";
			values[2].Text = ExtensionMethods.FormatScore(SaveManager.ActiveGameData.GetHighScore(Stage.Data.LevelID));
			values[3].Text = Stage.DisplayScore;
			values[4].Text = ExtensionMethods.FormatTime(SaveManager.ActiveGameData.GetBestTime(Stage.Data.LevelID));
			values[5].Text = Stage.DisplayTime;
			values[6].Text = ExtensionMethods.FormatEXP(Stage.CurrentEXP);
			values[7].Text = ExtensionMethods.FormatEXP(SaveManager.ActiveGameData.exp);
			values[8].Text = CharacterController.instance.Skills.TextDisplay;

			// Mission menu
			fireSoulParent.Visible = SaveManager.ActiveGameData.HasFireSouls(Stage.Data.LevelID);
			if (fireSoulParent.Visible)
			{
				for (int i = 0; i < fireSoulSprites.Length; i++)
				{
					bool isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(Stage.Data.LevelID, i + 1);
					fireSoulSprites[i].RegionRect = new(new(isCollected ? 450 : 400, fireSoulSprites[i].RegionRect.Position.Y), fireSoulSprites[i].RegionRect.Size);
				}
			}

			int rank = SaveManager.ActiveGameData.GetRank(Stage.Data.LevelID);
			rankSprite.RegionRect = new(new(rankSprite.RegionRect.Position.X, 110 + 60 * rank), rankSprite.RegionRect.Size);
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
					int visualSelection = skillSelection - skillList.LinesSkipped;
					if (skillSelection > skillList.LinesSkipped + 6)
					{
						visualSelection = 6;
						skillList.LinesSkipped = skillSelection - visualSelection;
					}
					else if (skillSelection < skillList.LinesSkipped)
					{
						visualSelection = 0;
						skillList.LinesSkipped = skillSelection;
					}

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
					animator.Set(SELECTION_PARAMETER, selection.ToString());
					break;
				case Submenu.STATUS:
					statusCursorAnimator.Play("move");
					animator.Set(VALUE_SELECTION_PARAMETER, selection.ToString());
					animator.Set(STATUS_SELECTION_PARAMETER, selection.ToString());
					break;
				case Submenu.SKILL:
					skillSelection = selection;
					UpdateCursorPosition();
					break;
			}
		}


		private void EnableInteraction() => AllowPausing = true;
		private void TogglePause()
		{
			canMoveCursor = false; //Disable cursor movement
			AllowPausing = false; //Disable pause inputs during the animation

			isActive = !isActive;
			animator.Set(CONFIRM_ENABLED_PARAMETER, "false");
			if (submenu == Submenu.PAUSE)
				animator.Set(STATE_PARAMETER, isActive ? "show" : "hide");
			else
				animator.Set(STATE_PARAMETER, "status-hide");

			if (isActive) //Reset selection
			{
				UpdateSelection(0);
				UpdateCursorPosition();
				animator.Set(SHOW_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
				color = Colors.Black
			});
		}

		/// <summary>
		/// Unpause and finish scene transition.
		/// </summary>
		private void TransitionFinished() => GetTree().SetDeferred("paused", false);
	}
}
