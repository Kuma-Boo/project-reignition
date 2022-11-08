using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Force the player to move along a path.
	/// </summary>
	public partial class AutomationTrigger : Area3D
	{
		[Export]
		private float distanceToTravel; //How far to travel. Set at 0 to travel the entire path
		[Export]
		private float minimumSpeedRatio = 1f;
		[Export]
		private Path3D automationPath; //Leave NULL to use the player's current path.
		[Export]
		private CameraSettingsResource cameraSettings;
		[Export]
		private float cameraBlend;

		private bool isEntered;
		private bool isActive;

		private float startingProgress;
		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Progress - startingProgress);
		private bool IsFinished => (!Mathf.IsZeroApprox(distanceToTravel) && DistanceTraveled >= distanceToTravel) || (automationPath != null && Character.PathFollower.ActivePath != automationPath);
		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (isActive)
			{
				if (IsFinished)
				{
					Deactivate();
					return;
				}

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

			startingProgress = Character.PathFollower.Progress;
			isActive = true;

			UpdateCamera();
		}

		private void Deactivate()
		{
			isActive = false;
			Character.ResetMovementState();
		}

		public void UpdateCamera()
		{
			if (cameraSettings != null)
				Character.Camera.SetCameraData(cameraSettings, cameraBlend);
		}

		public void OnEntered(Area3D _) => isEntered = true;
		public void OnExited(Area3D _) => isEntered = false;
	}
}
