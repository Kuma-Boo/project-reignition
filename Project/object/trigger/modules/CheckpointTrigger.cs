using Godot;

namespace Project.Gameplay.Triggers
{
	public class CheckpointTrigger : StageTriggerModule
	{
		public static CheckpointTrigger activeCheckpoint;

		public override void Activate() => activeCheckpoint = this;
		public Transform RespawnTransform => GetParent<Spatial>().GlobalTransform;
	}
}
