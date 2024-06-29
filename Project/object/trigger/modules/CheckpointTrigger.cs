using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Updates the current checkpoint.
	/// </summary>
	public partial class CheckpointTrigger : TeleportTrigger
	{
		public Path3D PlayerPath { get; private set; }
		public Path3D CameraPath { get; private set; }
		public CameraSettingsResource CameraSettings;
		private int savedObjectiveCount;

		public override void Activate()
		{
			StageSettings.instance.SetCheckpoint(this);
			StageSettings.instance.ResetObjective(savedObjectiveCount);
		}

		public void UpdateCheckpointData()
		{
			PlayerPath = CharacterController.instance.PathFollower.ActivePath; // Store current player path
			CameraPath = CharacterController.instance.Camera.PathFollower.ActivePath; // Store current camera path
			CameraSettings = CharacterController.instance.Camera.ActiveSettings;
			savedObjectiveCount = StageSettings.instance.CurrentObjectiveCount;
		}
	}
}
