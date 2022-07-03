using Godot;

namespace Project.Gameplay
{
	public class CheckpointTrigger : StageTriggerModule
	{
		public static CheckpointTrigger activeCheckpoint;

		public override void Activate() => activeCheckpoint = this;
	}
}
