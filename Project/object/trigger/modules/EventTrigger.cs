using Godot;

namespace Project.Gameplay.Triggers
{
	//Triggers an event (cutscene)
	public class EventTrigger : StageTriggerModule
	{
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public NodePath playerStandin;
		[Export]
		public NodePath cameraStandin;

		private bool wasActivated;

		public override void _Ready()
		{
			_animator = GetNode<AnimationPlayer>(animator);
			Respawn();
		}

		public override bool IsRespawnable() => true;
		public override void Respawn()
		{
			wasActivated = false;
			_animator.Play("RESET");
		}

		public override void Activate()
		{
			if (wasActivated) return;

			wasActivated = true;
			_animator.Play("Event");

			if(playerStandin != null)
				Character.StartFollowingEventObject(GetNode<Spatial>(playerStandin));
		}

		public void PlayCharacterAnimation(string anim) //Call this from the animator to play a specific animation on the player
		{
			Character.Animator.PlayAnimation(anim);
		}

		public void FinishEvent()
		{
			Character.CancelAutomation();
		}
	}
}
