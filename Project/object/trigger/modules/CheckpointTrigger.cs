using Godot;

namespace Project.Gameplay
{
	public class CheckpointTrigger : StageTriggerObject
	{
		public static CheckpointTrigger activeCheckpoint;

		public override void Activate() => activeCheckpoint = this;
	}
}
