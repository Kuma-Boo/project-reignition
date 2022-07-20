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
		private CameraSettingsResource previousData;
		private CameraController CameraController => CameraController.instance;
		public override void Activate()
		{
			if (CameraController.targetSettings != null) //Cache settings
				previousData = CameraController.targetSettings;

			CameraController.SetCameraData(cameraData, crossfade);
		}

		public override void Deactivate(bool isMovingForward)
		{
			if (CameraController.targetSettings != cameraData) return; //Already overriden by a differnt trigger
			CameraController.SetCameraData(isMovingForward ? null : previousData, false);
		}
	}
}
