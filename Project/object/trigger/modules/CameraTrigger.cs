//Module for camera triggers. STAGE TRIGGER MODE MUST BE SET "DISABLE ON EXIT" TO FUNCTION PROPERLY.
using Godot;

namespace Project.Gameplay.Triggers
{
	public class CameraTrigger : StageTriggerModule
	{
		[Export]
		public float entryTransitionSpeed; //How long the transition is
		[Export]
		public float exitTransitionSpeed = -1; //Set to -1 to use the same as the entry transition time
		[Export]
		public bool crossfade; //Only works properly if entryTransitionTime is 0

		[Export]
		public CameraSettingsResource cameraData; //Leave empty to make this a RESET trigger.
		[Export]
		public CameraSettingsResource previousData; //Leave empty to automatically assign
		private CameraController CameraController => CameraController.instance;

		public override void Activate()
		{
			if (previousData == null) //Cache settings on the first time
				previousData = CameraController.targetSettings;

			CameraController.SetCameraData(cameraData, entryTransitionSpeed, crossfade);
		}

		public override void Deactivate(bool isMovingForward)
		{
			if (CameraController.targetSettings != cameraData) return; //Already overriden by a differnt trigger
			CameraController.SetCameraData(previousData, exitTransitionSpeed >= 0 ? exitTransitionSpeed : entryTransitionSpeed, false);
		}
	}
}
