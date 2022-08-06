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

		public override void Activate()
		{
			if (previousData == null) //Cache settings on the first time
				previousData = Character.Camera.targetSettings;

			Character.Camera.SetCameraData(cameraData, entryTransitionSpeed, crossfade);
		}

		public override void Deactivate(bool isMovingForward)
		{
			if (Character.Camera.targetSettings != cameraData) return; //Already overriden by a differnt trigger
			Character.Camera.SetCameraData(previousData, exitTransitionSpeed >= 0 ? exitTransitionSpeed : entryTransitionSpeed);
		}
	}
}
