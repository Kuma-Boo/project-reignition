using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a <see cref="CameraSettingsResource"/>.
	/// </summary>
	public class CameraTrigger : StageTriggerModule
	{
		[Export]
		public float entryTransitionSpeed; //How long the transition is
		[Export]
		public float exitTransitionSpeed = -1; //Set to -1 to use the same as the entry transition time
		[Export]
		public bool crossfade; //Only works properly if entryTransitionTime is 0

		[Export]
		public CameraSettingsResource cameraData; //Must be assigned to something.
		[Export]
		public CameraSettingsResource previousData; //Leave empty to automatically assign.
		[Export]
		public CameraTrigger blendTrigger; //Used for blending camera triggers together.
		private CameraController Camera => Character.Camera;

		public override void Activate()
		{
			if (previousData == null) //Cache settings on the first time
				previousData = Camera.targetSettings;

			Camera.SetCameraData(cameraData, entryTransitionSpeed, crossfade);
		}

		public override void Deactivate()
		{
			if (Camera.targetSettings != cameraData) return; //Already overriden by a different trigger
			Camera.SetCameraData(previousData, exitTransitionSpeed >= 0 ? exitTransitionSpeed : entryTransitionSpeed);
		}
	}
}
