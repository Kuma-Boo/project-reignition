using Godot;

namespace Project.Gameplay
{
    public class CameraTrigger : StageTriggerObject
    {
        [Export]
        public CameraSettingsResource cameraData; //Leave empty to make this a RESET trigger.

		public override void Activate()
		{
			CameraController.instance.SetCameraData(cameraData);
		}

		public override void Deactivate(bool isMovingForward)
		{
			CameraController.instance.SetCameraData(null);
		}
	}
}
