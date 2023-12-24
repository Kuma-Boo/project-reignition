using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a <see cref="CameraSettingsResource"/>.
	/// </summary>
	public partial class CameraTrigger : StageTriggerModule
	{
		[Export(PropertyHint.Range, "0,2,0.1")]
		public float transitionTime; // How long the transition is (in seconds). Use a transition time of 0 to perform an instant cut.
		[Export(PropertyHint.Range, "-1,2,0.1")]
		public float deactivationTransitionTime = -1; // Override for deactivation
		[Export]
		public TransitionType transitionType;
		public enum TransitionType
		{
			Blend, // Interpolate between states
			Crossfade, // Crossfade states
		}

		[Export]
		/// <summary> Update static position/rotations every frame? </summary>
		public bool UpdateEveryFrame { get; private set; }

		[Export(PropertyHint.Range, "0, 179, .1")]
		/// <summary> FOV. Set to 0 to reset to default fov. </summary>
		public float targetFOV;

		[Export]
		/// <summary> Must be assigned to something. </summary>
		public CameraSettingsResource settings;
		[Export]
		/// <summary> Reference to the camera data that was being used when this trigger was entered. </summary>
		private CameraSettingsResource previousSettings;
		private Vector3 previousStaticPosition;
		private Basis previousStaticRotation;
		private CameraController Camera => Character.Camera;

		public void UpdateStaticData(CameraBlendData data)
		{
			if (data.SettingsResource.useStaticPosition)
				data.StaticPosition = GlobalPosition;

			if (data.SettingsResource.copyRotation)
				data.RotationBasis = GlobalBasis;
		}


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
				previousStaticPosition = Camera.ActiveBlendData.StaticPosition; // Cache static position
				previousStaticRotation = Camera.ActiveBlendData.RotationBasis; // Cache static rotation
			}

			Camera.UpdateCameraSettings(new CameraBlendData()
			{
				BlendTime = transitionTime,
				SettingsResource = settings,
				IsCrossfadeEnabled = transitionType == TransitionType.Crossfade,
				Trigger = this
			});

			UpdateStaticData(Camera.ActiveBlendData);
		}


		public override void Deactivate()
		{
			if (previousSettings == null || settings == null) return;
			if (Camera.ActiveSettings != settings) return; // Already overridden by a different trigger
			if (Character.IsRespawning) return;

			Camera.UpdateCameraSettings(new CameraBlendData()
			{
				BlendTime = Mathf.IsEqualApprox(deactivationTransitionTime, -1) ? transitionTime : deactivationTransitionTime,
				SettingsResource = previousSettings,
				StaticPosition = previousStaticPosition, // Restore cached static position
				RotationBasis = previousStaticRotation // Restore cached static rotation
			});
		}
	}
}
