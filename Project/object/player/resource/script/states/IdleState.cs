using Godot;

namespace Project.Gameplay;

public partial class IdleState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState backstepState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.MoveSpeed = 0;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			return jumpState;
		}

		if (!Player.CheckGround())
			return fallState;

		if (!Player.Controller.CameraInputAxis.IsZeroApprox())
		{
			// Move
			return runState;
		}

		return null;
	}
}
