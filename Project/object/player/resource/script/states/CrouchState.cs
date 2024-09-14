using Godot;

namespace Project.Gameplay;

public partial class CrouchState : PlayerState
{
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.Animator.StartCrouching();
		Player.ChangeHitbox("crouch");
	}

	public override void ExitState()
	{
		Player.ChangeHitbox("RESET");
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

		if (!Player.IsOnGround)
			return fallState;

		if (!Input.IsActionPressed("button_action"))
			return idleState;

		if (Player.Skills.IsSpeedBreakActive)
			return runState;

		return null;
	}
}
