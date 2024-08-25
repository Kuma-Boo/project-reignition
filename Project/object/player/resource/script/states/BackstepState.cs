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
		if (!Controller.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Controller.MoveSpeed))
			return idleState;

		return null;
	}
}
