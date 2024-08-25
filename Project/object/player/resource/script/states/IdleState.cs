using Godot;

namespace Project.Gameplay;

public partial class IdleState : PlayerState
{
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Controller.MoveSpeed = 0;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Controller.IsJumpBufferActive)
		{
			Controller.ResetJumpBuffer();
			return jumpState;
		}

		if (!Controller.CheckGround())
			return fallState;

		return null;
	}
}
