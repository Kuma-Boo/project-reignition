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

		if (Player.Lockon.IsMonitoring &&
			(Player.Controller.IsJumpBufferActive || Player.Controller.IsAttackBufferActive))
		{
			Player.Controller.ResetJumpBuffer();
			Player.Controller.ResetAttackBuffer();
			if (Player.Lockon.Target != null && Player.Lockon.IsTargetAttackable)
				return homingAttackState;

			return jumpDashState;
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
}
