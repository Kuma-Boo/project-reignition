using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public partial class AutomationTrigger : Area3D
	{
		[Export]
		public float distanceToTravel; //How far to travel. Set at 0 to travel the entire path
		[Export]
		public float minimumSpeedRatio = 1f;
		[Export]
		public NodePath automationPath;
		private Path3D _automationPath;
		[Export]
		public CameraSettingsResource cameraSettings;
		[Export]
		public float cameraBlend;

		private bool isEntered;
		private bool isProcessing;

		private float startingOffset;
		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Progress - startingOffset);
		private bool IsFinished => (!Mathf.IsZeroApprox(distanceToTravel) && DistanceTraveled >= distanceToTravel) || (_automationPath != null && Character.PathFollower.ActivePath != _automationPath);
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			_automationPath = GetNodeOrNull<Path3D>(automationPath);
		}

		public override void _PhysicsProcess(double _)
		{
			if (isProcessing)
			{
				if (IsFinished)
					Deactivate();

				if (Character.MoveSpd < Character.groundSettings.speed)
					Character.MoveSpd = Character.groundSettings.speed;
				Character.PathFollower.Progress += Character.MoveSpd * PhysicsManager.physicsDelta;
				return;
			}

			if (!isEntered) return;

			if (IsActivationValid())
				Activate();
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround || Character.groundSettings.GetSpeedRatio(Character.MoveSpd) < minimumSpeedRatio) return false;
			return true;
		}

		private void Activate()
		{
			Character.PathFollower.SetActivePath(_automationPath);
			Character.StartExternal(Character.PathFollower, false);

			startingOffset = Character.PathFollower.Progress;
			isProcessing = true;

			UpdateCamera();
		}

		public void UpdateCamera()
		{
			if (cameraSettings != null)
				Character.Camera.SetCameraData(cameraSettings, cameraBlend);
		}

		private void Deactivate()
		{
			isProcessing = false;
			Character.CancelMovementState(CharacterController.MovementStates.External);
		}

		public void OnEntered(Area3D _) => isEntered = true;
		public void OnExited(Area3D _) => isEntered = false;
	}
}
