namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Updates the current checkpoint.
	/// </summary>
	public partial class CheckpointTrigger : TeleportTrigger
	{
		public override void Activate() => StageSettings.instance.SetCheckpoint(this);
	}
}
