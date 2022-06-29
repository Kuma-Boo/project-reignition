using Godot;

namespace Project.Gameplay
{
	//Triggers an event (cutscene)
	public class EventTrigger : StageTriggerObject
	{
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

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
		}
	}
}
