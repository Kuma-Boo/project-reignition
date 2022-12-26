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
		public CameraSettingsResource settings; //Must be assigned to something.
		private CameraSettingsResource previousSettings; //Reference to the camera data that was being used when this trigger was entered.
		private Vector3 previousStaticPosition;
		private CameraController CameraController => Character.Camera;

		public override void Activate()
		{
			if (settings == null)
			{
				GD.PrintErr($"{Name} doesn't have a CameraSettingResource attached!");
				return;
			}

			if (previousSettings == null)
			{
				previousSettings = CameraController.ActiveSettings;
				if (previousSettings.IsStaticCamera && previousSettings.autosetStaticPosition) //Cache static position
					previousStaticPosition = previousSettings.staticPosition;
			}

			if (settings.IsStaticCamera && settings.autosetStaticPosition)
				settings.staticPosition = GlobalPosition;

			CameraController.UpdateCameraSettings(settings, transitionTime, transitionType == TransitionType.Crossfade);
		}

		public override void Deactivate()
		{
			if (previousSettings.IsStaticCamera)

				if (previousSettings == null || settings == null) return;
			if (CameraController.ActiveSettings != settings) return; //Already overridden by a different trigger

			if (previousSettings.IsStaticCamera && previousSettings.autosetStaticPosition) //Reset static position
				previousSettings.staticPosition = previousStaticPosition;

			CameraController.UpdateCameraSettings(previousSettings, transitionTime);
		}
	}
}
