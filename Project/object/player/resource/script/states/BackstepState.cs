using Godot;

namespace Project.Gameplay;

public partial class BackstepState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState idleState;

	public override void EnterState()
	{
		Player.IsMovingBackward = true;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.ApplyMovement();

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		return null;
	}

	private void ProcessMoveSpeed()
	{
		float inputStrength = Mathf.Min(Player.Controller.CameraInputAxis.Length(), 1f);
		if (inputStrength < Player.Controller.DeadZone)
		{
			Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float inputDot = ExtensionMethods.DotAngle(Player.GetTargetMovementAngle(), Player.MovementAngle);
		if (inputDot <= 0f || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void ProcessTurning()
	{
		Player.MovementAngle = Player.PathFollower.BackAngle + Player.PathTurnInfluence;
	}
}
