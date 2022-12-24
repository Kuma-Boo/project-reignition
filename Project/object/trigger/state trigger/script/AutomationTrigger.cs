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
		private Path3D initialPath; //Reference to the player's initial path
		[Export]
		private CameraSettingsResource cameraSettings;
		[Export]
		private float cameraBlend;

		private bool isEntered;
		private bool isActive;

		private float DistanceTraveled => Mathf.Abs(Character.PathFollower.Progress - startingPoint);
		private bool IsFinished => (distanceToTravel > 0 && DistanceTraveled >= distanceToTravel) || (automationPath != null && DistanceTraveled >= automationPath.Curve.GetBakedLength());
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
				Character.MoveSpeed = Character.GroundSettings.Interpolate(Character.MoveSpeed, 1); //Move to max speed

			Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround) return false;
			//Ensure character is facing/moving the correct direction
			float dot = ExtensionMethods.DotAngle(Character.MovementAngle, CharacterController.CalculateForwardAngle(this.Forward()));
			if (dot < 0f) return false;

			return true;
		}

		private void Activate()
		{
			//Cancel any lockout that doesn't have an assigned priority (i.e. Dash Panels)
			if (Character.IsLockoutActive && Character.CurrentLockoutData.priority == -1)
				Character.RemoveLockoutData(Character.CurrentLockoutData);

			if (automationPath != null)
			{
				initialPath = Character.PathFollower.ActivePath;
				Character.PathFollower.SetActivePath(automationPath);
			}

			Character.PathFollower.Resync();

			float initialVelocity = Character.MoveSpeed;
			Character.StartExternal(this, Character.PathFollower, .05f, true);
			Character.MoveSpeed = initialVelocity;

			isActive = true;
			UpdateCamera();
		}

		private void Deactivate()
		{
			//Revert to previous path
			if (automationPath != null)
				Character.PathFollower.SetActivePath(initialPath);

			isActive = false;
			Character.PathFollower.Resync();

			Character.ResetMovementState();
			Character.MovementAngle = Character.PathFollower.ForwardAngle;
			Character.UpDirection = Character.PathFollower.Up();
		}

		public void UpdateCamera()
		{
			if (cameraSettings != null)
				Character.Camera.UpdateCameraSettings(cameraSettings, cameraBlend);
		}

		public void OnEntered(Area3D _) => isEntered = true;
		public void OnExited(Area3D _) => isEntered = false;
	}
}
