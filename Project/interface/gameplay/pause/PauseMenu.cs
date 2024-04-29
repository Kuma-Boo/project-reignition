using Godot;
using Project.Core;

namespace Project.Interface
{
	public partial class PauseMenu : Node
	{
		public static bool AllowPausing = true;

		[Export]
		private AnimationMixer animator;
		[Export]
		private AudioStreamPlayer screenChangeSFX;
		[Export]
		private Node2D cursor;
		[Export]
		private AnimationPlayer cursorAnimator;

		private bool isActive;

		private readonly StringName SELECTION_PARAMETER = "parameters/selection/transition_request";
		private readonly StringName CONFIRM_PARAMETER = "parameters/confirm/transition_request";
		private readonly StringName CONFIRM_ENABLED_PARAMETER = "parameters/confirm_enabled/transition_request";
		private readonly StringName STATE_PARAMETER = "parameters/state/transition_request";
		private readonly StringName SHOW_TRIGGER_PARAMETER = "parameters/show_trigger/request";


		public override void _Ready()
		{
			animator.Active = true;
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
					AllowPausing = false;
					animator.Set(CONFIRM_PARAMETER, currentSelection.ToString());
					animator.Set(CONFIRM_ENABLED_PARAMETER, "true");
				}
				else if (sign != 0)
				{
					int targetSelection = Mathf.Clamp(currentSelection + sign, 0, MAX_SELECTION);
					if (targetSelection != currentSelection)
						UpdateSelection(targetSelection);
				}
			}
		}


		/// <summary> Actually applies the current selection (called from the animator). </summary>
		private void ApplySelection()
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
				case 3: // Return to the main menu
					StartSceneTransition(TransitionManager.MENU_SCENE_PATH);
					break;
			}
		}


		/// <summary> Selected menu option. </summary>
		private int currentSelection;
		/// <summary> Can the cursor currently be moved? </summary>
		private bool canMoveCursor;
		private const int MAX_SELECTION = 3;
		private const int SELECTION_SPACING = 32;
		private void EnableCursorMovement() => canMoveCursor = true;
		private void UpdateCursorPosition()
		{
			cursor.Position = Vector2.Down * currentSelection * SELECTION_SPACING;
			cursorAnimator.Play("show");
			cursorAnimator.Seek(0, true);
		}


		private void UpdateSelection(int selection)
		{
			canMoveCursor = false;
			cursorAnimator.Play("hide");
			currentSelection = selection;
			animator.Set(SELECTION_PARAMETER, selection.ToString());
		}


		private void EnableInteraction() => AllowPausing = true;
		private void TogglePause()
		{
			canMoveCursor = false; //Disable cursor movement
			AllowPausing = false; //Disable pause inputs during the animation

			isActive = !isActive;
			animator.Set(CONFIRM_ENABLED_PARAMETER, "false");
			animator.Set(STATE_PARAMETER, isActive ? "show" : "hide");
			animator.Set(SHOW_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);

			if (isActive) //Reset selection
			{
				currentSelection = 0;
				UpdateCursorPosition();
			}
		}


		private void ApplyPause()
		{
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
