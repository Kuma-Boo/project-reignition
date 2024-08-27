using Godot;

namespace Project.Gameplay;

public partial class HomingAttackState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;

	public override PlayerState ProcessPhysics()
	{
		// REFACTOR TODO Replace this with a wall check and switch to the bounce state instead
		if (Player.CheckGround())
			return landState;

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}
}
