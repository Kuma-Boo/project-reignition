using Godot;

namespace Project.Gameplay;

public partial class FallState : PlayerState
{
	[Export]
	private PlayerState landState;

	public override PlayerState ProcessPhysics()
	{
		Controller.MoveAndSlide();

		if (Controller.CheckGround())
			return landState;

		return null;
	}
}
