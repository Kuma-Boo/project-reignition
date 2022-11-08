namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Updates the current checkpoint.
	/// </summary>
	public partial class CheckpointTrigger : StageTriggerModule
	{
		public override void Activate()
		{
			StageSettings.instance.SetCheckpoint(this);
		}
	}
}
