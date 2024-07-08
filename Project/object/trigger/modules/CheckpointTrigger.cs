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
		private int savedScore;
		private int savedObjectiveCount;

		private StageSettings Stage => StageSettings.instance;

		public override void Activate()
		{
			Stage.SetCheckpoint(this);
			Stage.ResetObjective(savedObjectiveCount);
			Stage.UpdateScore(savedScore, StageSettings.MathModeEnum.Replace);
		}

		public void UpdateCheckpointData()
		{
			PlayerPath = CharacterController.instance.PathFollower.ActivePath; // Store current player path
			CameraPath = CharacterController.instance.Camera.PathFollower.ActivePath; // Store current camera path
			CameraSettings = CharacterController.instance.Camera.ActiveSettings;
			savedScore = Stage.CurrentScore;
			savedObjectiveCount = Stage.CurrentObjectiveCount;
		}
	}
}
