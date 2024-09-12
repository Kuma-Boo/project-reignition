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
	private PlayerState backflipState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.MoveSpeed = 0;
		Player.Effect.IsEmittingStepDust = false;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.Skills.IsSpeedBreakActive)
			return runState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();

			float inputAngle = Player.Controller.GetTargetMovementAngle(true);
			float inputStrength = Player.Controller.GetInputStrength();
			if (!Mathf.IsZeroApprox(inputStrength) &&
				Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			{
				return backflipState;
			}

			return jumpState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return crouchState;
		}

		if (!Player.CheckGround())
			return fallState;

		Player.CheckWall(CalculateWallCastDirection());
		if (!Player.IsOnWall)
		{
			if (Player.IsLockoutActive && Player.ActiveLockoutData.overrideSpeed && !Mathf.IsZeroApprox(Player.ActiveLockoutData.speedRatio))
				return runState;

			if (!Mathf.IsZeroApprox(Player.Controller.GetInputStrength()) && !Input.IsActionPressed("button_brake"))
			{
				if (Player.Controller.GetHoldingDistance(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle) >= 1.0f)
					return backstepState;

				return runState;
			}
		}

		Player.Animator.IdleAnimation();
		return null;
	}

	private Vector3 CalculateWallCastDirection()
	{
		if (Mathf.IsZeroApprox(Player.Controller.GetInputStrength()))
			return Player.GetMovementDirection();

		float targetAngle = Player.Controller.GetTargetMovementAngle();
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetAngle, Player.PathFollower.ForwardAngle);
		return Player.PathFollower.Forward().Rotated(Player.UpDirection, deltaAngle);
	}
}
