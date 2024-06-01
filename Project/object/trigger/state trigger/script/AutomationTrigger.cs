using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Force the player to move along a path.
	/// </summary>
	public partial class AutomationTrigger : Area3D
	{
		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();

		[Export]
		/// <summary> Where to stop automation. </summary>
		private float endPoint;
		[Export]
		/// <summary> Always activate, regardless of which way the player entered/moves. </summary>
		private bool ignoreDirection;

		private bool isEntered;
		private bool isActive;

		private bool IsFinished => Character.PathFollower.Progress >= endPoint;
		private CharacterController Character => CharacterController.instance;

		/// <summary> Extra acceleration applied when the player is moving too slow. </summary>
		private const float LOW_SPEED_ACCELERATION = 80.0f;

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
			{
				if (Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < .8f) // Accelerate quicker to reduce low-speed jank
					Character.MoveSpeed += LOW_SPEED_ACCELERATION * PhysicsManager.physicsDelta;
				Character.MoveSpeed = Character.GroundSettings.Interpolate(Character.MoveSpeed, 1); // Move to max speed
			}

			Character.PathFollower.Progress += Character.MoveSpeed * PhysicsManager.physicsDelta;
			Character.MovementAngle = Character.PathFollower.ForwardAngle;

			Character.UpdateExternalControl(false);
			Character.Animator.ExternalAngle = 0;
		}

		private bool IsActivationValid()
		{
			if (!Character.IsOnGround) return false;

			if (!ignoreDirection)
			{
				// Ensure character is facing/moving the correct direction
				float dot = ExtensionMethods.DotAngle(Character.MovementAngle, ExtensionMethods.CalculateForwardAngle(this.Forward()));
				if (dot < 0f || Character.IsMovingBackward) return false;
			}

			return true;
		}

		private void Activate()
		{
			EmitSignal(SignalName.Activated);
			isActive = true;

			// Cancel any lockout that doesn't have an assigned priority (i.e. Dash Panels)
			if (Character.IsLockoutActive && Character.ActiveLockoutData.priority == -1)
				Character.RemoveLockoutData(Character.ActiveLockoutData);

			Character.PathFollower.Resync();

			float initialVelocity = Character.MoveSpeed;
			Character.StartExternal(this, Character.PathFollower, .05f, true);
			Character.MoveSpeed = initialVelocity;
			Character.Animator.ExternalAngle = 0;
			Character.Animator.SnapRotation(Character.Animator.ExternalAngle); // Rotate to follow pathfollower
			Character.IsMovingBackward = false; // Prevent getting stuck in backstep animation
		}

		private void Deactivate()
		{
			EmitSignal(SignalName.Deactivated);
			isActive = false;

			Character.PathFollower.Resync();
			Character.ResetMovementState();
			Character.UpDirection = Character.PathFollower.Up();
			Character.Animator.SnapRotation(Character.MovementAngle);
		}

		public void OnEntered(Area3D _) => isEntered = true;
		public void OnExited(Area3D _) => isEntered = false;
	}
}
