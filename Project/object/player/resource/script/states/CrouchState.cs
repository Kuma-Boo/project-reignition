using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class CrouchState : PlayerState
{
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState jumpState;
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

		if (!Player.IsDrifting &&
			Player.StateMachine.QueuedState != jumpState)
		{
			Player.Skills.ConsumeJumpCharge();
		}

		float inputStrength = Player.Controller.GetInputStrength();
		if (!Mathf.IsZeroApprox(inputStrength) || Player.Skills.IsSpeedBreakActive) // Transition into moving state
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
		Player.CheckCeiling();

		if (!Player.IsOnGround)
			return fallState;

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
		{
			Player.Skills.ChargeJump();
			if (!Input.IsActionPressed("button_jump"))
			{
				if (!Player.Controller.IsBrakeHeld())
					return jumpState;

				Player.Skills.ConsumeJumpCharge();
				return idleState;
			}
		}
		else if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			return jumpState;
		}
		else if (!Input.IsActionPressed("button_action"))
		{
			return idleState;
		}

		if (Player.Skills.IsSpeedBreakActive)
			return runState;

		return null;
	}
}
