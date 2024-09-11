using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class JumpDashState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState stompState;
	[Export]
	private float jumpDashSpeed;
	[Export]
	private float jumpDashPower;
	[Export]
	private float jumpDashGravity;
	[Export]
	private float jumpDashMaxGravity;

	public override void EnterState()
	{
		// Moving directly backwards -- jumpdash directly forward
		if (ExtensionMethods.DeltaAngleRad(Player.MovementAngle, Player.PathFollower.BackAngle) <= Mathf.Pi * .25f)
			Player.MovementAngle = Player.PathFollower.ForwardAngle;
		else // Don't allow jumpdashing backwards (sideways is OK though)
			Player.MovementAngle = ExtensionMethods.ClampAngleRange(Player.MovementAngle, Player.PathFollower.ForwardAngle, Mathf.Pi * .5f);

		Player.IsJumpDashing = true;
		Player.IsMovingBackward = false; // Can't jumpdash backwards!
		Player.MoveSpeed = jumpDashSpeed;
		Player.VerticalSpeed = jumpDashPower;
		Player.Lockon.IsMonitoring = false;

		Player.Effect.PlayActionSFX(Player.Effect.JumpDashSfx);
		Player.Effect.StartTrailFX();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.CrestFire))
			Player.Skills.ActivateFireCrest();

		Player.Animator.LaunchAnimation();
	}

	public override void ExitState()
	{
		Player.IsJumpDashing = false;
		Player.Effect.StopTrailFX();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.CrestFire))
			Player.Skills.DeactivateFireCrest();
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, -jumpDashMaxGravity, jumpDashGravity * PhysicsManager.physicsDelta);
		Player.ApplyMovement();
		Player.CheckGround();
		Player.CheckWall();
		Player.UpdateUpDirection(true);

		if (Player.IsOnGround)
			return landState;

		if (Player.IsOnWall && Player.WallRaycastHit.collidedObject.IsInGroup("splash jump"))
		{
			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SplashJump))
			{
				// Perform a splash jump
				Player.Lockon.IsHomingAttacking = false;
				Player.Lockon.ResetLockonTarget();
				Player.Effect.PlaySplashJumpFX();
				Player.Animator.SplashJumpAnimation();
				Player.VerticalSpeed = Runtime.CalculateJumpPower(Player.Stats.JumpHeight * .5f);
				return fallState;
			}

			// Kill speed when jump dashing into a wall to prevent splash jump from becoming obsolete
			Player.MoveSpeed = 0;
			Player.VerticalSpeed = Mathf.Clamp(Player.VerticalSpeed, -Mathf.Inf, 0);
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}

	protected override void ProcessMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (Mathf.IsZeroApprox(inputStrength) || !Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.MoveSpeed = Player.Stats.AirSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float targetMovementAngle = Player.Controller.GetTargetMovementAngle();
		float inputDot = ExtensionMethods.DotAngle(Player.MovementAngle, targetMovementAngle);
		if (inputDot < -.75f || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.AirSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0, Player.Stats.AirSettings.Friction * PhysicsManager.physicsDelta);
	}

	protected override void Accelerate(float _) => Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0, ActiveMovementSettings.Friction * PhysicsManager.physicsDelta);
}
