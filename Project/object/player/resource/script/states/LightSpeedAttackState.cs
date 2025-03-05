using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class LightSpeedAttackState : PlayerState
{
	[Export] private PlayerState fallState;
	[Export] private PlayerState stompState;
	[Export] private PlayerState jumpDashState;

	[Export] private float normalStrikeSpeed;
	[Export] private float perfectStrikeSpeed;
	[Export] private float acceleration;

	public Vector3 startPosition;
	public Vector3 endPosition;
	public Vector3 inHandle => startPosition;
	public Vector3 outHandle => endPosition;

	public override void EnterState()
	{
		startPosition = Player.GlobalPosition;

		// Note: Animation and hitbox properties carry over from HomingAttackState.
		Player.Effect.PlayActionSFX(Player.Effect.JumpDashSfx);
	}

	public override void ExitState()
	{
		Player.IsLightSpeedAttacking = Player.IsHomingAttacking = false;
		Player.AttackState = PlayerController.AttackStates.None;
		Player.ChangeHitbox("RESET");
		Player.Lockon.CallDeferred(PlayerLockonController.MethodName.ResetLockonTarget);
		Player.Effect.StopSpinFX();
		Player.Effect.StopTrailFX();
		Player.Animator.ResetState();

		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.CrestFire))
			return;

		if (Player.IsBouncing)
		{
			Player.Skills.CallDeferred(PlayerSkillController.MethodName.ActivateFireCrestBurst);
		}
		else
		{
			Player.Lockon.ResetLockonTarget();
			Player.Skills.DeactivateFireCrest();
		}
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Player.Lockon.IsTargetAttackable) // Target disappeared. Transition to jumpdash
		{
			Player.MovementAngle = Player.PathFollower.ForwardAngle;
			Player.ChangeHitbox("RESET");
			return jumpDashState;
		}

		if (Player.IsPerfectHomingAttacking)
			Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, perfectStrikeSpeed, acceleration * 2.0f * PhysicsManager.physicsDelta);
		else
			Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, normalStrikeSpeed, acceleration * PhysicsManager.physicsDelta);
		Player.Velocity = Player.Lockon.HomingAttackDirection.Normalized() * Player.MoveSpeed;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Player.Lockon.HomingAttackDirection);
		Player.MoveAndSlide();
		Player.CheckGround();
		Player.CheckWall();
		Player.UpdateUpDirection(true);
		Player.PathFollower.Resync();

		bool isColliding = Player.GetSlideCollisionCount() != 0;
		if (isColliding && ProcessObstructions())
		{
			Player.StartBounce();
			return null;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}

	private bool ProcessObstructions()
	{
		// Check from the floor
		Vector3 castOffset = Vector3.Up * Player.CollisionSize.Y * .5f;
		Vector3 castPosition = Player.GlobalPosition + castOffset;
		if (Player.VerticalSpeed < 0)
			castPosition += Player.UpDirection * Player.VerticalSpeed * PhysicsManager.physicsDelta;
		Vector3 castVector = Player.Lockon.Target.GlobalPosition - castPosition;
		RaycastHit hit = Player.CastRay(castPosition, castVector, Runtime.Instance.lockonObstructionMask);
		DebugManager.DrawRay(castPosition, castVector, Colors.Magenta);
		return hit && hit.collidedObject.IsInGroup("wall") && !hit.collidedObject.IsInGroup("level wall");
	}
}
