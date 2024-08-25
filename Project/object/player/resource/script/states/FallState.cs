using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class FallState : PlayerState
{
	[Export]
	private PlayerState landState;

	public override PlayerState ProcessPhysics()
	{
		Controller.VerticalSpeed = Mathf.MoveToward(Controller.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		Controller.ApplyMovement();

		if (Controller.CheckGround())
			return landState;

		return null;
	}
}
