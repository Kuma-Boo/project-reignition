using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
	public partial class PauseMenu : Node
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
			BGMPlayer.StageMusicPaused = GetTree().Paused;
			_pauseAnimator.Play(GetTree().Paused ? "Pause" : "Unpause");

			float targetTimeScale = GetTree().Paused ? 0.0f : 1.0f; //Calculate the correct time scale
			if (CharacterController.instance.Skills.IsTimeBreakActive) //Correct time scales for time break
				targetTimeScale = CharacterSkillManager.TIME_BREAK_RATIO;
			Engine.TimeScale = targetTimeScale;

			GD.Print("TODO - Pausing shaders are currently unimplemented. Shaders need to be updated to use the 'time_scale' global uniform.");
		}

		public override void _Process(double _)
		{
			if (!canInteractWithPauseMenu) return;
			if (InputManager.controller.pauseButton.wasPressed)
				TogglePause();
		}
	}
}
