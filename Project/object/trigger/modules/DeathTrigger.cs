using Godot;

namespace Project.Gameplay.Triggers
{
	public class DeathTrigger : StageTriggerModule
	{
		[Export]
		public TriggerType triggerType;
		public enum TriggerType
		{
			Height,
			Water,
			Lava,
		}

		public override void Activate()
		{
			Character.Kill();
		}
	}
}