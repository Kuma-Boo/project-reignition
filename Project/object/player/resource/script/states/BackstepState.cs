using Godot;

namespace Project.Gameplay;

public partial class BackstepState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState idleState;

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
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
			Player.MoveSpeed = -Player.Stats.BackstepSettings.UpdateInterpolate(Mathf.Abs(Player.MoveSpeed), 0);
			return;
		}

		float inputDot = Player.Controller.GetMovementInputDotProduct(Player.MovementAngle);
		if (inputDot >= 0f || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = -Player.Stats.BackstepSettings.UpdateInterpolate(Mathf.Abs(Player.MoveSpeed), -inputStrength);
			return;
		}

		Player.MoveSpeed = -Player.Stats.BackstepSettings.UpdateInterpolate(Mathf.Abs(Player.MoveSpeed), inputStrength);
	}
}
