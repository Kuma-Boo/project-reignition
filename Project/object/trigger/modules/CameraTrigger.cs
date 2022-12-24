using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a <see cref="CameraSettingsResource"/>.
	/// </summary>
	public partial class CameraTrigger : StageTriggerModule
	{
		[Export(PropertyHint.Range, "0,2,0.1")]
		public float transitionTime; //How long the transition is (in seconds). Use a transition time of 0 to perform an instant cut.
		[Export]
		public TransitionType transitionType;
		public enum TransitionType
		{
			Blend, //Interpolate between states
			Crossfade, //Crossfade scenes
		}

		[Export]
		public CameraSettingsResource cameraData; //Must be assigned to something.
		private CameraSettingsResource previousData; //Reference to the camera data that was being used when this trigger was entered.
		private CameraController CameraController => Character.Camera;

		public override void Activate()
		{
			if (cameraData == null)
			{
				GD.PrintErr($"{Name} doesn't have a CameraSettingResource attached!");
				return;
			}

			if (CameraController.BlendToSettings == cameraData) return; //Already set

			if (cameraData.isStaticCamera && cameraData.autosetStaticPosition)
				cameraData.staticPosition = GlobalPosition;

			if (previousData == null)
				previousData = CameraController.BlendToSettings;

			CameraController.UpdateCameraSettings(cameraData, transitionTime, transitionType == TransitionType.Crossfade);
		}

		public override void Deactivate()
		{
			if (cameraData == null) return;
			if (CameraController.BlendToSettings != cameraData) return; //Already overriden by a different trigger

			GD.Print($"Changed camera settings to {previousData}");
			CameraController.UpdateCameraSettings(previousData, transitionTime);
		}
	}
}
