using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class ReversePathState : PlayerState
{
	[Export] private PlayerState idleState;
	[Export] private PlayerState crouchState;
	[Export] private PlayerState backflipState;
	[Export] private PlayerState jumpState;

	private bool playedTurnaroundAnimation;
	private readonly float deccelerationRatio = .9f;

	public override void EnterState()
	{
		playedTurnaroundAnimation = false;
		Player.IsMovingBackward = false;
		if (Player.Skills.IsSpeedBreakActive)
			Player.Skills.ToggleSpeedBreak();
	}

	public override void ExitState()
	{
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.Animator.DisabledSpeedSmoothing = true;
		Player.Animator.IdleAnimation();
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
	}

	public override PlayerState ProcessPhysics()
	{
		Player.MoveSpeed *= deccelerationRatio;
		ProcessGravity();
		Player.ApplyMovement();
		Player.CheckWall();
		Player.UpdateUpDirection();

		if (!Player.CheckGround())
			return null;

		Player.MoveSpeed = 0;

		if (!playedTurnaroundAnimation)
		{
			Player.Animator.ReversePathAnimation();
			playedTurnaroundAnimation = true;
			return null;
		}

		if (!Player.Animator.IsReversePathAnimationActive)
		{
			Player.Animator.DisabledSpeedSmoothing = true;
			return idleState;
		}

		Player.Animator.IdleAnimation();

		if (!Player.Skills.IsSpeedBreakActive)
		{
			if (Player.Controller.IsJumpBufferActive)
			{
				Player.MovementAngle = Player.PathFollower.ForwardAngle;
				Player.Controller.ResetJumpBuffer();

				float inputAngle = Player.Controller.GetTargetInputAngle();
				float inputStrength = Player.Controller.GetInputStrength();
				if (!Player.IsLockoutDisablingAction(LockoutResource.ActionFlags.Backflip) &&
					!Mathf.IsZeroApprox(inputStrength) &&
					Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
				{
					return backflipState;
				}

				if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
					return crouchState;

				return jumpState;
			}
		}

		return null;
	}
}
