//Module for camera triggers. STAGE TRIGGER MODE MUST BE SET "DISABLE ON EXIT" TO FUNCTION PROPERLY.
using Godot;

namespace Project.Gameplay.Triggers
{
	public class CameraTrigger : StageTriggerModule
	{
		[Export]
		public bool crossfade;
		[Export]
		public CameraSettingsResource cameraData; //Leave empty to make this a RESET trigger.
		[Export]
		public CameraSettingsResource previousData; //Leave empty to automatically assign
		private CameraController CameraController => CameraController.instance;
		public override void Activate()
		{
			if (previousData == null) //Cache settings on the first time
				previousData = CameraController.targetSettings;

			CameraController.SetCameraData(cameraData, crossfade);
		}

		public override void Deactivate(bool isMovingForward)
		{
			if (CameraController.targetSettings != cameraData) return; //Already overriden by a differnt trigger
			CameraController.SetCameraData(previousData, false);
		}
	}
}
