using Godot;
using Project.Core;

namespace Project.Interface
{
	public partial class PauseMenu : Node
	{
		[Export]
		private AnimationPlayer pauseAnimator;

		private bool canInteractWithPauseMenu = true;
		private InputManager.Controller Controller => InputManager.controller;

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
			RuntimeConstants.Instance.pearlTexture.SpeedScale = targetTimeScale;
			RuntimeConstants.Instance.richPearlTexture.SpeedScale = targetTimeScale;

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

			if (Controller.pauseButton.wasPressed)
				TogglePause();
			else if (GetTree().Paused && canMoveCursor)
			{
				if (Controller.verticalAxis.sign != 0)
				{
					int targetSelection = Mathf.Clamp(currentSelection + Controller.verticalAxis.sign, 0, MAX_SELECTION);
					if (targetSelection != currentSelection)
						UpdateSelection(targetSelection);
				}
				else if (InputManager.controller.jumpButton.wasPressed)
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
			TransitionData data = new TransitionData()
			{
				inSpeed = .5f,
				outSpeed = .5f,
				color = Colors.Black
			};

			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
			TransitionManager.QueueSceneChange(targetScene, false);

			TransitionManager.StartTransition(data);
		}

		/// <summary>
		/// Unpause and finish scene transition.
		/// </summary>
		private void TransitionFinished() => GetTree().SetDeferred("paused", false);
	}
}
