using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class SpinJumpState : PlayerState
{
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;

	public bool IsShortenedJump { get; set; }
	private readonly float JumpCurve = .95f;

	public override void EnterState()
	{
		Player.IsSpinJump = true;
		Player.AttackState = PlayerController.AttackStates.Weak;
		Player.Animator.StartSpin(5f);
		Player.Effect.StartSpinFX();
	}

	public override void ExitState()
	{
		Player.IsSpinJump = false;
		Player.AttackState = PlayerController.AttackStates.None;
		Player.Animator.ResetState(0);
		Player.Effect.StopSpinFX();
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Input.IsActionPressed("button_jump"))
			IsShortenedJump = true;

		ProcessMoveSpeed();
		ProcessTurning();
		ProcessGravity();
		Player.ApplyMovement();
		Player.IsMovingBackward = Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.BackAngle);
		Player.CheckGround();
		Player.CheckWall(Vector3.Zero);
		if (Player.CheckCeiling())
			return null;
		Player.UpdateUpDirection();

		if (Player.IsOnGround)
			return landState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
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

	protected override void ProcessGravity()
	{
		if (IsShortenedJump && Player.VerticalSpeed > 0)
			Player.VerticalSpeed *= JumpCurve; // Kill jump height

		base.ProcessGravity();
	}
}
