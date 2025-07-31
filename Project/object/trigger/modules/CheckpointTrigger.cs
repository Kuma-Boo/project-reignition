using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Updates the current checkpoint.
	/// </summary>
	public partial class CheckpointTrigger : TeleportTrigger
	{
		[Export] public Path3D PlayerPath { get; private set; }
		[Export] public Path3D CameraPath { get; private set; }
		[Export] public CameraSettingsResource CameraSettings;

		private StageSettings Stage => StageSettings.Instance;

		public override void Activate()
		{
			GD.Print("Activating Checkpoint");
			// Already the current checkpoint!
			if (Stage.CurrentCheckpoint == this)
				return;

			Stage.SetCheckpoint(this);
			SaveCheckpointData();
		}

		public void SaveCheckpointData()
		{
			if (PlayerPath == null)
				PlayerPath = StageSettings.Player.PathFollower.ActivePath; // Store current player path

			if (CameraPath == null)
				CameraPath = StageSettings.Player.Camera.PathFollower.ActivePath; // Store current camera path

			if (CameraSettings == null)
				CameraSettings = StageSettings.Player.Camera.ActiveSettings;
		}
	}
}
