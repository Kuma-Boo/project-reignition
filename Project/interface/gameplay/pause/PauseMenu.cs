using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
	public partial class PauseMenu : Node
	{
		[Export]
		private Node2D pauseCursor;
		[Export]
		private AnimationPlayer pauseAnimator;

		private bool canInteractWithPauseMenu = true;
		public void EnablePausing() => canInteractWithPauseMenu = true;
		public void DisablePausing() => canInteractWithPauseMenu = false;

		private void TogglePause()
		{
			canInteractWithPauseMenu = false; //Disable pause inputs during the animation
			GetTree().Paused = !GetTree().Paused;
			BGMPlayer.StageMusicPaused = GetTree().Paused;
			pauseAnimator.Play(GetTree().Paused ? "Pause" : "Unpause");

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
