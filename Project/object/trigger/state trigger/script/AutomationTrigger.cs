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
		private float startingPoint;
		[Export]
		private Path3D automationPath; //Leave NULL to use the player's current path.
		[Export]
		private CameraSettingsResource cameraSettings;
		[Export]
		private float cameraBlend;

		private bool isEntered;
		private bool isActive;

		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Progress - startingPoint);
		private bool IsFinished => DistanceTraveled >= distanceToTravel;
		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (isActive)
			{
				UpdateAutomation();

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

		private void UpdateAutomation()
		{
			if (!Character.Skills.IsSpeedBreakActive)
				Character.MoveSpeed = Character.groundSettings.Interpolate(Character.MoveSpeed, 1); //Move to max speed

			Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
			Character.UpdateExternalControl();
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround) return false;
			return true;
		}

		private void Activate()
		{
			//Cancel any lockout that doesn't have an assigned priority (i.e. Dash Panels)
			if (Character.IsLockoutActive && Character.CurrentLockoutData.priority == -1)
				Character.RemoveLockoutData(Character.CurrentLockoutData);

			float initialVelocity = Character.MoveSpeed;
			Character.PathFollower.SetActivePath(automationPath);
			Character.StartExternal(Character.PathFollower, .2f);
			Character.MoveSpeed = initialVelocity;

			isActive = true;
			UpdateCamera();
		}

		private void Deactivate()
		{
			isActive = false;
			Character.PathFollower.Resync();

			Character.ResetMovementState();
			Character.MovementAngle = Character.PathFollower.ForwardAngle;
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
