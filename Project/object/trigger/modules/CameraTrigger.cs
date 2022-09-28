using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a <see cref="CameraSettingsResource"/>.
	/// </summary>
	public partial class CameraTrigger : StageTriggerModule
	{
		[Export]
		public float transitionTime; //How long the transition is
		[Export]
		public TransitionType transitionType;
		public enum TransitionType
		{
			Blend, //Interpolate between states; Use a transition time of 0 to perform an instant cut
			Crossfade, //Crossfade scenes
		}

		[Export]
		public CameraSettingsResource cameraData; //Must be assigned to something.
		[Export]
		public CameraSettingsResource previousData; //Leave empty to automatically assign.
		[Export]
		public CameraTrigger blendTrigger; //Used for blending camera triggers together.
		private CameraController CameraController => Character.Camera;

		public override void Activate()
		{
			if (cameraData != null && cameraData.isStaticCamera && cameraData.autosetStaticPosition)
				cameraData.staticPosition = GlobalPosition;

			if (previousData == null) //Cache settings on the first time
				previousData = CameraController.targetSettings;

			CameraController.SetCameraData(cameraData, transitionTime, transitionType == TransitionType.Crossfade);
		}

		public override void Deactivate()
		{
			if (CameraController.targetSettings != cameraData) return; //Already overriden by a different trigger
			CameraController.SetCameraData(previousData, transitionTime);
		}
	}
}
