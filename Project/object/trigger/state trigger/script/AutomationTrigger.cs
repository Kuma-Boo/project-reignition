using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Force the player to move along a path.
/// </summary>
public partial class AutomationTrigger : Area3D
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();

	/// <summary> The distance along the path where automation stops. </summary>
	[Export]
	private float endPoint;
	/// <summary> Always activate regardless of which way the player entered/moves. </summary>
	[Export]
	private bool ignoreDirection;

	private bool isEntered;
	private bool isActive;
	/// <summary> Reference to the automation path. </summary>
	private Path3D automationPath;

	private bool IsFinished => Player.PathFollower.ActivePath != automationPath || Player.PathFollower.Progress >= endPoint;
	private PlayerController Player => StageSettings.Player;

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
		if (!Player.Skills.IsSpeedBreakActive)
		{
			if (Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) < .8f) // Accelerate quicker to reduce low-speed jank
				Player.MoveSpeed += LOW_SPEED_ACCELERATION * PhysicsManager.physicsDelta;

			if (Player.State.IsLockoutActive && Player.State.ActiveLockoutData.overrideSpeed)
				Player.MoveSpeed = Player.State.ActiveLockoutData.ApplySpeed(Player.MoveSpeed, Player.Stats.GroundSettings);
			else
				Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 1); // Move to max speed
		}

		Player.PathFollower.Progress += Player.MoveSpeed * PhysicsManager.physicsDelta;
		Player.MovementAngle = Player.PathFollower.ForwardAngle;

		Player.State.UpdateExternalControl(false);
		Player.Animator.ExternalAngle = 0;
	}

	private bool IsActivationValid()
	{
		if (!Player.IsOnGround) return false;

		if (!ignoreDirection)
		{
			// Ensure character is facing/moving the correct direction
			float dot = ExtensionMethods.DotAngle(Player.MovementAngle, ExtensionMethods.CalculateForwardAngle(this.Forward()));
			if (dot < 0f || Player.IsMovingBackward) return false;
		}

		return true;
	}

	private void Activate()
	{
		EmitSignal(SignalName.Activated);
		isActive = true;

		automationPath = Player.PathFollower.ActivePath;
		Player.PathFollower.Resync();

		float initialVelocity = Player.MoveSpeed;
		Player.State.StartExternal(this, Player.PathFollower, .05f, true);
		Player.MoveSpeed = initialVelocity;
		Player.Animator.ExternalAngle = 0;
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle); // Rotate to follow pathfollower
		Player.IsMovingBackward = false; // Prevent getting stuck in backstep animation

		if (Player.Animator.IsBrakeAnimationActive)
			Player.Animator.StopBrake();
	}

	private void Deactivate()
	{
		EmitSignal(SignalName.Deactivated);
		isActive = false;

		Player.PathFollower.Resync();
		Player.State.StopExternal();
		Player.UpDirection = Player.PathFollower.Up();
		Player.Animator.SnapRotation(Player.MovementAngle);
	}

	public void OnEntered(Area3D _) => isEntered = true;
	public void OnExited(Area3D _) => isEntered = false;
}
