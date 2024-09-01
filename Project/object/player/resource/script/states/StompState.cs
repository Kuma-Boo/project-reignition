using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class StompState : PlayerState
{
	[Export]
	private PlayerState landState;

	/// <summary> How fast to fall when stomping </summary>
	private readonly float StompSpeed = -32;
	/// <summary> How much gravity to add each frame. </summary>
	private readonly float JumpCancelGravity = 180;
	/// <summary> How much gravity to add each frame. </summary>
	private readonly float StompGravity = 540;

	public override void EnterState()
	{
		Player.MoveSpeed = 0;
		Player.Lockon.IsMonitoring = false;
		Player.Lockon.ResetLockonTarget();

		if (Player.State.IsGrindstepping)
			Player.Animator.ResetState(.1f);

		/* REFACTOR TODO
		allowLandingSkills = true;
		SetActionState(ActionStates.Stomping);
		*/

		bool attackStomp = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.StompAttack);
		if (attackStomp)
		{
			Player.State.AttackState = PlayerStateController.AttackStates.Weak;
			Player.State.ChangeHitbox("stomp");
		}
		Player.Animator.StompAnimation(attackStomp);
	}

	public override PlayerState ProcessPhysics()
	{
		Player.MoveSpeed = 0; // Go STRAIGHT down
		UpdateVerticalSpeed();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.UpdateUpDirection(true);

		if (Player.IsOnGround)
			return landState;

		return null;
	}

	private void UpdateVerticalSpeed()
	{
		/* REFACTOR TODO
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.StompAttack))
			Player.VerticalSpeed = Mathf.MoveToward(VerticalSpeed, StompSpeed, StompGravity * PhysicsManager.physicsDelta);
		else
		*/
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, StompSpeed, JumpCancelGravity * PhysicsManager.physicsDelta);
	}
}
