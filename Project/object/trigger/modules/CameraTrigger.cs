//Module for camera triggers. STAGE TRIGGER MODE MUST BE SET "DISABLE ON EXIT" TO FUNCTION PROPERLY.
using Godot;

namespace Project.Gameplay.Triggers
{
	public class CameraTrigger : StageTriggerModule
	{
		[Export]
		public bool recenterStrafe; //Used when the camera needs to be recentered. Camera Data is IGNORED when this is active.

		[Export]
		public bool crossfade;
		[Export]
		public CameraSettingsResource cameraData; //Leave empty to make this a RESET trigger.
		private CameraSettingsResource previousData;
		private CameraController CameraController => CameraController.instance;
		public override void Activate()
		{
			if (recenterStrafe)
			{
				CameraController.IsRecenteringStrafe = true;
				return;
			}

			if (CameraController.overrideCameraSettings != null) //Cache settings
				previousData = CameraController.overrideCameraSettings;

			CameraController.SetCameraData(cameraData, crossfade);
		}

		public override void Deactivate(bool isMovingForward)
		{
			if (recenterStrafe)
			{
				CameraController.IsRecenteringStrafe = false;
				return;
			}

			if (CameraController.overrideCameraSettings != cameraData) return; //Already overriden by a differnt trigger
			CameraController.SetCameraData(isMovingForward ? null : previousData, false);
		}
	}
}
