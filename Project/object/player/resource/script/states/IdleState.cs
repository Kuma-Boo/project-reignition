using Godot;

namespace Project.Gameplay;

public partial class IdleState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState backstepState;
	[Export]
	private PlayerState crouchState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.MoveSpeed = 0;
		Player.Effect.IsEmittingStepDust = false;
	}

	public override PlayerState ProcessPhysics()
	{
		Player.Animator.IdleAnimation();

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			return jumpState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return crouchState;
		}

		if (!Player.CheckGround())
			return fallState;

		if (Player.IsLockoutActive && Player.ActiveLockoutData.overrideSpeed && !Mathf.IsZeroApprox(Player.ActiveLockoutData.speedRatio))
			return runState;

		if (!Mathf.IsZeroApprox(Player.Controller.GetInputStrength()) && !Input.IsActionPressed("button_brake"))
		{
			if (Player.Controller.GetHoldingDistance(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle) >= 1.0f)
				return backstepState;

			return runState;
		}

		return null;
	}
}
