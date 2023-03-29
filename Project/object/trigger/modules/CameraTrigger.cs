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
		[Export(PropertyHint.Range, "-1,2,0.1")]
		public float deactivationTransitionTime = -1; //Override for deactivation
		[Export]
		public TransitionType transitionType;
		public enum TransitionType
		{
			Blend, //Interpolate between states
			Crossfade, //Crossfade states
		}

		[Export]
		public CameraSettingsResource settings; //Must be assigned to something.
		[Export]
		private CameraSettingsResource previousSettings; //Reference to the camera data that was being used when this trigger was entered.
		private Vector3 previousStaticPosition;
		private CameraController Camera => Character.Camera;

		public override void Activate()
		{
			if (settings == null)
			{
				GD.PrintErr($"{Name} doesn't have a CameraSettingResource attached!");
				return;
			}

			if (previousSettings == null)
			{
				previousSettings = Camera.ActiveSettings;
				previousStaticPosition = Camera.ActiveBlendData.StaticPosition; //Cache static position
			}

			Camera.UpdateCameraSettings(new CameraBlendData()
			{
				BlendTime = transitionTime,
				SettingsResource = settings,
				StaticPosition = GlobalPosition,
				IsCrossfadeEnabled = transitionType == TransitionType.Crossfade
			});
		}

		public override void Deactivate()
		{
			if (previousSettings == null || settings == null) return;
			if (Camera.ActiveSettings != settings) return; //Already overridden by a different trigger
			if (Character.IsRespawning) return;

			Camera.UpdateCameraSettings(new CameraBlendData()
			{
				BlendTime = Mathf.IsEqualApprox(deactivationTransitionTime, -1) ? transitionTime : deactivationTransitionTime,
				SettingsResource = previousSettings,
				StaticPosition = previousStaticPosition, //Restore cached static position
			});
		}
	}
}
