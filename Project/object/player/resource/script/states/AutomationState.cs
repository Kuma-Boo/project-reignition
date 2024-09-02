using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class AutomationState : PlayerState
{
	[Export]
	private PlayerState runState;

	public AutomationTrigger Automation { get; set; }
	private Path3D initialPath;

	/// <summary> Extra acceleration applied when the player is moving too slow. </summary>
	private const float CatchupAccelerationAmount = 80.0f;

	public override void EnterState()
	{
		Automation.Activate();

		initialPath = Player.PathFollower.ActivePath;
		Player.PathFollower.Resync();

		Player.StartExternal(Automation, Player.PathFollower, .05f);
		Player.Animator.ExternalAngle = 0;
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle); // Rotate to follow pathfollower
		Player.IsMovingBackward = false; // Prevent getting stuck in backstep animation

		if (Player.Animator.IsBrakeAnimationActive)
			Player.Animator.StopBrake();
	}

	public override void ExitState()
	{
		Automation.Deactivate();

		Player.PathFollower.Resync();
		Player.StopExternal();
		Player.UpDirection = Player.PathFollower.Up();
		Player.Animator.SnapRotation(Player.MovementAngle);
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Player.Skills.IsSpeedBreakActive)
		{
			if (Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) < .8f) // Accelerate quicker to reduce low-speed jank
				Player.MoveSpeed += CatchupAccelerationAmount * PhysicsManager.physicsDelta;

			if (Player.IsLockoutActive && Player.ActiveLockoutData.overrideSpeed)
				Player.MoveSpeed = Player.ActiveLockoutData.ApplySpeed(Player.MoveSpeed, Player.Stats.GroundSettings);
			else
				Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 1); // Move to max speed
		}

		Player.PathFollower.Progress += Player.MoveSpeed * PhysicsManager.physicsDelta;
		Player.MovementAngle = Player.PathFollower.ForwardAngle;

		Player.UpdateExternalControl();
		Player.Animator.ExternalAngle = 0;

		if (Player.PathFollower.ActivePath != initialPath || Automation.IsFinished)
			return runState;

		return null;
	}
}
