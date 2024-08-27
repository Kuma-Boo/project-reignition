using Godot;

namespace Project.Gameplay;

public partial class LandState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState backstepState;

	public override void EnterState()
	{
		Player.Lockon.IsMonitoring = false;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.MoveSpeed > 0)
			return runState;

		return backstepState;
	}
}
