using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class HomingAttackState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState bounceState;

	[Export]
	private float normalStrikeSpeed;
	[Export]
	private float perfectStrikeSpeed;
	[Export]
	private float homingAttackAcceleration;

	private bool IsPerfectHomingAttack { get; set; }

	public override void EnterState()
	{
		Player.VerticalSpeed = 0;
		Player.Lockon.IsHomingAttacking = true;
		Player.State.ChangeHitbox("spin");
		Player.State.AttackState = PlayerStateController.AttackStates.Weak;

		IsPerfectHomingAttack = Player.Lockon.IsMonitoringPerfectHomingAttack;
		if (IsPerfectHomingAttack)
		{
			Player.Lockon.PlayPerfectStrike();
			Player.State.AttackState = PlayerStateController.AttackStates.Strong;
		}

		Player.Effect.StartSpinFX();
		/*
		REFACTOR TODO
		Player.Animator.StartSpin(2.0f);
		*/
	}

	public override void ExitState()
	{
		Player.Lockon.IsHomingAttacking = false;
		Player.Lockon.ResetLockonTarget();

		/*
		REFACTOR TODO Move to Bounce state processing
		if (Player.Skills.IsSkillEquipped(SkillKey.CrestFire) && IsBounceLockoutActive)
		{
			Player.Skills.DeactivateFireCrest(true);
			return;
		}
		*/

		Player.State.AttackState = PlayerStateController.AttackStates.None;
		/* REFACTOR TODO?
			Change state Player.ResetActionState();
			Player.StateMachine.ChangeState
		*/
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.Lockon.Target == null) // Target disappeared. Transition to jumpdash
		{
			Player.MovementAngle = Player.PathFollower.ForwardAngle;
			Player.State.ChangeHitbox("RESET");
			return jumpDashState;
		}

		if (IsPerfectHomingAttack)
			Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, perfectStrikeSpeed, homingAttackAcceleration * 2.0f * PhysicsManager.physicsDelta);
		else
			Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, normalStrikeSpeed, homingAttackAcceleration * PhysicsManager.physicsDelta);
		Player.Velocity = Player.Lockon.HomingAttackDirection.Normalized() * Player.MoveSpeed;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Player.Lockon.HomingAttackDirection);
		Player.MoveAndSlide();

		// REFACTOR TODO switch to bounce state
		if (Player.GetSlideCollisionCount() != 0)
		{
		}

		Player.UpdateUpDirection(true);
		Player.PathFollower.Resync();

		// REFACTOR TODO Replace this with a wall check and switch to the bounce state instead
		if (Player.CheckGround())
			return landState;

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}
}
