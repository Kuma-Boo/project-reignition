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
		private bool ignoreDirection; //Always activate, regardless of which way the player entered/moves

		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();

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
			Character.MovementAngle = Character.PathFollower.ForwardAngle;

			Character.UpdateExternalControl();

			Character.Animator.ExternalAngle = 0;
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround) return false;

			if (!ignoreDirection)
			{
				//Ensure character is facing/moving the correct direction
				float dot = ExtensionMethods.DotAngle(Character.MovementAngle, Character.CalculateForwardAngle(this.Forward()));
				if (dot < 0f || Character.IsMovingBackward) return false;
			}

			return true;
		}

		private void Activate()
		{
			//Cancel any lockout that doesn't have an assigned priority (i.e. Dash Panels)
			if (Character.IsLockoutActive && Character.ActiveLockoutData.priority == -1)
				Character.RemoveLockoutData(Character.ActiveLockoutData);

			if (automationPath != null)
			{
				initialPath = Character.PathFollower.ActivePath;
				Character.PathFollower.SetActivePath(automationPath);
			}

			Character.PathFollower.Resync();

			float initialVelocity = Character.MoveSpeed;
			Character.StartExternal(this, Character.PathFollower, .05f, true);
			Character.MoveSpeed = initialVelocity;
			Character.Animator.SnapRotation(0);
			Character.IsMovingBackward = false; //Prevent getting stuck in backstep animation

			isActive = true;
			EmitSignal(SignalName.Activated);
		}

		private void Deactivate()
		{
			isActive = false;
			Character.PathFollower.Resync();

			Character.ResetMovementState();
			Character.UpDirection = Character.PathFollower.Up();
			Character.Animator.SnapRotation(Character.MovementAngle);

			//Revert to previous path
			if (automationPath != null)
				Character.PathFollower.SetActivePath(initialPath);

			EmitSignal(SignalName.Deactivated);
		}

		public void OnEntered(Area3D _) => isEntered = true;
		public void OnExited(Area3D _) => isEntered = false;
	}
}
