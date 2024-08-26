using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class FallState : PlayerState
{
	[Export]
	private PlayerState landState;

	public override PlayerState ProcessPhysics()
	{
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		Player.ApplyMovement();

		if (Player.CheckGround())
			return landState;

		return null;
	}
}
