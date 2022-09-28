namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Updates the current checkpoint.
	/// </summary>
	public partial class CheckpointTrigger : StageTriggerModule
	{
		public static CheckpointTrigger activeCheckpoint;
		public override void Activate() => activeCheckpoint = this;
	}
}
