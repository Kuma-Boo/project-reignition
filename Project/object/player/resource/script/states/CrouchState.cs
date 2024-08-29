using Godot;

namespace Project.Gameplay;

public partial class CrouchState : PlayerState
{
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.Animator.StartCrouching();
	}

	public override void ExitState()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (!Mathf.IsZeroApprox(inputStrength)) // Transition into moving state
		{
			Player.Animator.CrouchToMoveTransition();
			return;
		}

		Player.Animator.StopCrouching();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.MoveSpeed *= .5f;
		Player.ApplyMovement();
		Player.CheckGround();

		if (!Input.IsActionPressed("button_action"))
			return idleState;

		if (!Player.IsOnGround)
			return fallState;

		return null;
	}
}
