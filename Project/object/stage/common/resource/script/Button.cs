using Godot;

namespace Project.Gameplay
{
	public class Button : RespawnableObject
	{
		[Export]
		public NodePath buttonAnimator;
		private AnimationPlayer _buttonAnimator;
		[Export]
		public NodePath eventTrigger;
		public EventTrigger _eventTrigger;

		private bool isActive;

		public override void SetUp()
		{
			_buttonAnimator = GetNode<AnimationPlayer>(buttonAnimator);
			_eventTrigger = GetNode<EventTrigger>(eventTrigger);

			StageManager.instance.RegisterRespawnableObject(this);
			Spawn();
		}

		public override void Spawn()
		{
			isActive = false;
			_buttonAnimator.Play("RESET");
		}

		public override void OnEntered(Area _)
		{
			if (isActive) return;

			isActive = true;
			_buttonAnimator.Play("activate");
			_eventTrigger.Activate();
		}
	}
}
