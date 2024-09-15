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

	protected override void ProcessTurning()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed) && Input.IsActionPressed("button_brake"))
			return;

		bool isUsingStrafeControls = Player.Skills.IsSpeedBreakActive ||
			SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) ||
			(Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe); // Ignore path delta under certain lockout situations

		float pathControlAmount = Player.PathTurnInfluence;
		if (isUsingStrafeControls || Player.IsLockoutActive)
			pathControlAmount = 0; // Don't use path influence during speedbreak/autorun

		float targetMovementAngle = Player.Controller.GetTargetMovementAngle() + pathControlAmount;
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace) // Direction is being overridden
		{
			Player.MovementAngle = targetMovementAngle;
		}

		if (turnInstantly) // Instantly set movement angle to target movement angle
		{
			SnapRotation(targetMovementAngle);
			return;
		}

		if (Player.Controller.IsHoldingDirection(targetMovementAngle, Player.MovementAngle + Mathf.Pi))
		{
			// Check for turning around
			if (!Player.IsLockoutActive || Player.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Strafe)
				return;
		}

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		targetMovementAngle = ProcessTargetMovementAngle(targetMovementAngle);

		// Normal turning
		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.ForwardAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float maxTurnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;

		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle += pathControlAmount;
		Turn(targetMovementAngle, turnSmoothing);

		// Strafe implementation
		if (isUsingStrafeControls)
			ProcessStrafe(targetMovementAngle);
	}
}
