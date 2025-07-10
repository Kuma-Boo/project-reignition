using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class FallState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;

	public override void EnterState()
	{
		Player.AllowLandingGrind = true;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		ProcessGravity();
		Player.ApplyMovement();
		Player.IsMovingBackward = Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.BackAngle);
		Player.CheckGround();
		Player.CheckWall();
		Player.UpdateUpDirection();

		if (Player.IsOnGround)
			return landState;


		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			if (SaveManager.Config.useStompJumpButtonMode)
				return stompState;

			PlayerState attackState = GetAttackTargetState();
			if (GetAttackTargetState() != null)
				return attackState;
		}

		if (Player.Lockon.Monitoring && Player.Controller.IsAttackBufferActive)
		{
			Player.Controller.ResetAttackBuffer();

			PlayerState attackState = GetAttackTargetState();
			if (GetAttackTargetState() != null)
				return attackState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LightSpeedDash) &&
			Player.Controller.IsLightDashBufferActive)
		{
			Player.StartLightSpeedDash();
		}

		return null;
	}

	private PlayerState GetAttackTargetState()
	{
		if (!Player.Lockon.Monitoring)
			return null;

		if (Player.Lockon.IsTargetAttackable)
			return homingAttackState;

		if (Player.CanJumpDash)
			return jumpDashState;

		return null;
	}
}
