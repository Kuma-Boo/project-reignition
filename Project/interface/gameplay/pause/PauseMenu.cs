using Godot;
using Project.Core;

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
		private Node2D skillCursor;
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
		private Label skillList;
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
						{
							pauseCursorAnimator.Play("hide");
							animator.Set(STATUS_SHOW_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
						}
						else
						{
							animator.Set(CONFIRM_PARAMETER, currentSelection.ToString());
							animator.Set(CONFIRM_ENABLED_PARAMETER, "true");
						}
					}
					else if (submenu == Submenu.STATUS && currentSelection == 2) // Enter skill menu
					{

					}
				}
				else if (Input.IsActionJustPressed("button_action"))
				{
					if (submenu == Submenu.PAUSE)
						TogglePause();
					else
					{
						AllowPausing = false;
						statusCursorAnimator.Play("hide");
						animator.Set(STATUS_HIDE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
						animator.Set(SUBMENU_PARAMETER, "status");
						UpdateSelection(0);
						UpdateCursorPosition();
						break;
					case 3: // Return to the main menu
						StartSceneTransition(TransitionManager.MENU_SCENE_PATH);
						break;
				}
			}
			else if (submenu == Submenu.STATUS)
			{
				if (currentSelection == 2) // Enter skill menu
				{
					submenu = Submenu.SKILL;
					UpdateSelection(0); // Select the first skill
					UpdateCursorPosition();
				}
			}
		}


		private void CancelSelection()
		{
			submenu = Submenu.PAUSE;
			UpdateSelection(2);
			animator.Set(SUBMENU_PARAMETER, "pause");
		}


		/// <summary> Selected menu option. </summary>
		private int currentSelection;
		/// <summary> Can the cursor currently be moved? </summary>
		private bool canMoveCursor;
		private void EnableCursorMovement() => canMoveCursor = true;
		private void UpdateCursorPosition()
		{
			switch (submenu)
			{
				case Submenu.PAUSE:
					pauseCursor.Position = Vector2.Down * currentSelection * 32;
					pauseCursorAnimator.Play("show");
					pauseCursorAnimator.Advance(0.0);
					break;
				case Submenu.STATUS:
					statusCursor.Position = Vector2.Down * currentSelection * 32;
					statusCursorAnimator.Play("show");
					statusCursorAnimator.Advance(0.0);
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
			TransitionManager.StartTransition(new TransitionData()
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
