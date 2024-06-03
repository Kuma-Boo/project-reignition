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

		public override void Activate()
		{
			PlayerPath = CharacterController.instance.PathFollower.ActivePath; // Store current player path
			CameraPath = CharacterController.instance.Camera.PathFollower.ActivePath; // Store current camera path
			CameraSettings = CharacterController.instance.Camera.ActiveSettings;
			StageSettings.instance.SetCheckpoint(this);
		}
	}
}
