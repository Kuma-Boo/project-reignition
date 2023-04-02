using Godot;
using Project.Core;

namespace Project.Interface
{
	public partial class PauseMenu : Node
	{
		[Export]
		private AnimationPlayer pauseAnimator;

		private bool canInteractWithPauseMenu = true;

		private void EnableInteraction() => canInteractWithPauseMenu = true;

		private void TogglePause()
		{
			canMoveCursor = false; //Disable cursor movement
			canInteractWithPauseMenu = false; //Disable pause inputs during the animation

			bool isPaused = !GetTree().Paused;

			GetTree().Paused = isPaused;
			BGMPlayer.StageMusicPaused = isPaused;
			pauseAnimator.Play(isPaused ? "pause" : "unpause");

			float targetTimeScale = isPaused ? 0.0f : 1.0f; //Calculate the correct time scale
			Runtime.Instance.pearlTexture.SpeedScale = targetTimeScale;
			Runtime.Instance.richPearlTexture.SpeedScale = targetTimeScale;

			if (isPaused) //Reset selection
			{
				currentSelection = 0;
				UpdateCursorPosition();
			}
		}

		[Export]
		private Control cursorNode;
		[Export]
		private AnimationPlayer cursorAnimator;
		/// <summary> Selected menu option. </summary>
		private int currentSelection;
		/// <summary> Can the cursor currently be moved? </summary>
		private bool canMoveCursor;
		private const int MAX_SELECTION = 3;
		private const int SELECTION_SPACING = 56;

		private void EnableCursorMovement() => canMoveCursor = true;
		private void UpdateCursorPosition()
		{
			cursorNode.Position = Vector2.Down * currentSelection * SELECTION_SPACING;
			cursorAnimator.Play("show");
			cursorAnimator.Seek(0, true);
		}

		private void UpdateSelection(int selection)
		{
			canMoveCursor = false;
			cursorAnimator.Play("hide");
			currentSelection = selection;
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!canInteractWithPauseMenu || Countdown.IsCountdownActive) return;

			if (Input.IsActionJustPressed("button_pause"))
				TogglePause();
			else if (GetTree().Paused && canMoveCursor)
			{
				int sign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
				if (sign != 0)
				{
					int targetSelection = Mathf.Clamp(currentSelection + sign, 0, MAX_SELECTION);
					if (targetSelection != currentSelection)
						UpdateSelection(targetSelection);
				}
				else if (Input.IsActionJustPressed("button_jump"))
				{
					switch (currentSelection)
					{
						case 0: //Resume
							TogglePause();
							break;
						case 1: //Restart
							StartSceneTransition(string.Empty);
							break;
						case 3:
							StartSceneTransition(TransitionManager.MENU_SCENE_PATH); //Return to the main menu
							break;
					}
				}
			}
		}

		private void StartSceneTransition(string targetScene)
		{
			pauseAnimator.Play("screen-change");

			canMoveCursor = false;
			canInteractWithPauseMenu = false;

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
