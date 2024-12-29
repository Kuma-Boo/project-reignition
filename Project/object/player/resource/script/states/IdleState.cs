using Godot;
using Project.Core;

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
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.overrideSpeed &&
			Mathf.IsZeroApprox(Player.ActiveLockoutData.speedRatio))
		{
			Player.Animator.IdleAnimation();
			return null;
		}

		if (Player.Skills.IsSpeedBreakActive)
			return runState;

		if (!Player.Skills.IsSpeedBreakActive)
		{
			if (Player.Controller.IsJumpBufferActive)
			{
				Player.Controller.ResetJumpBuffer();

				float inputAngle = Player.Controller.GetTargetInputAngle();
				float inputStrength = Player.Controller.GetInputStrength();
				if (!Mathf.IsZeroApprox(inputStrength) &&
					Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
				{
					return backflipState;
				}

				if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
					return crouchState;

				return jumpState;
			}

			if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
				Player.Controller.IsActionBufferActive)
			{
				Player.Controller.ResetActionBuffer();
				return crouchState;
			}
		}

		if (!Player.CheckGround())
			return fallState;
		Player.CheckWall(CalculateWallCastDirection());
		if (Player.CheckCeiling())
			return null;

		if (!Player.IsOnWall)
		{
			if (Player.IsLockoutActive && Player.ActiveLockoutData.overrideSpeed && !Mathf.IsZeroApprox(Player.ActiveLockoutData.speedRatio))
				return runState;

			if (!Player.Controller.IsBrakeHeld() &&
				(SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) || !Mathf.IsZeroApprox(Player.Controller.GetInputStrength())))
			{
				if (Player.Controller.GetHoldingDistance(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle) >= 1.0f)
					return backstepState;

				return runState;
			}

			if (!Mathf.IsZeroApprox(Player.MoveSpeed))
				return runState;
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
