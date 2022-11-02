using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public partial class AutomationTrigger : Area3D
	{
		[Export]
		private float distanceToTravel; //How far to travel. Set at 0 to travel the entire path
		[Export]
		private float minimumSpeedRatio = 1f;
		[Export]
		private Path3D automationPath;
		[Export]
		private CameraSettingsResource cameraSettings;
		[Export]
		private float cameraBlend;

		private bool isEntered;
		private bool isProcessing;

		private float startingOffset;
		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Progress - startingOffset);
		private bool IsFinished => (!Mathf.IsZeroApprox(distanceToTravel) && DistanceTraveled >= distanceToTravel) || (automationPath != null && Character.PathFollower.ActivePath != automationPath);
		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (isProcessing)
			{
				if (IsFinished)
					Deactivate();

				if (Character.MoveSpeed < Character.groundSettings.speed)
					Character.MoveSpeed = Character.groundSettings.speed;
				Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
				return;
			}

			if (!isEntered) return;

			if (IsActivationValid())
				Activate();
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround || Character.groundSettings.GetSpeedRatio(Character.MoveSpeed) < minimumSpeedRatio) return false;
			return true;
		}

		private void Activate()
		{
			Character.PathFollower.SetActivePath(automationPath);
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
