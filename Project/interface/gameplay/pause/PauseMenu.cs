using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
    public class PauseMenu : Node
	{
		[Export]
		public NodePath pauseAnimator;
		private AnimationPlayer _pauseAnimator;

		[Export]
		public NodePath pauseCursor;
		private Node2D _pauseCursor;

		private bool canInteractWithPauseMenu = true;
		public void EnablePausing() => canInteractWithPauseMenu = true;
		public void DisablePausing() => canInteractWithPauseMenu = false;

		public override void _Ready()
		{
			_pauseAnimator = GetNode<AnimationPlayer>(pauseAnimator);
			_pauseCursor = GetNode<Node2D>(pauseCursor);
		}

		private void TogglePause()
		{
			canInteractWithPauseMenu = false; //Disable pause inputs during the animation
			GetTree().Paused = !GetTree().Paused;
			BGMPlayer.instance.StreamPaused = GetTree().Paused;
			_pauseAnimator.Play(GetTree().Paused ? "Pause" : "Unpause");

			if (CharacterController.instance.IsTimeBreakActive)//Fix speed break
				Engine.TimeScale = GetTree().Paused ? 1f : CharacterController.TIME_BREAK_RATIO;
		}

		public override void _Process(float _)
		{
			if (!canInteractWithPauseMenu) return;

			if (InputManager.controller.pauseButton.wasPressed)
			{
				TogglePause();
				return;
			}
		}
	}
}
