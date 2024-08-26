using Godot;

namespace Project.Gameplay;

public partial class RunState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState idleState;

	public override void EnterState()
	{

	}

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
		float targetMovementAngle = Player.Controller.CameraInputAxis.AngleTo(Vector2.Up);
		float dot = ExtensionMethods.DotAngle(targetMovementAngle, Player.MovementAngle);

		if (inputStrength < Player.Controller.DeadZone)
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		if (dot < -.5f || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}
}
