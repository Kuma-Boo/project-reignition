using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class JumpState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;

	[Export]
	private float accelerationJumpSpeed = 25f;
	[Export]
	private float accelerationJumpHeightVelocity = 5;

	private float jumpTimer;

	private bool isShortenedJump;
	private bool isAccelerationJump;
	private bool isAccelerationJumpQueued;
	/// <summary> Cached acceleration jump height. Used to determine when the acceleration jump should start slowing down. </summary>
	private float accelerationJumpHeight;

	private readonly float JumpCurve = .95f;
	/// <summary> How fast the jump button needs to be released to count as an Acceleration Jump. </summary>
	private readonly float AccelerationJumpLength = .1f;
	/// <summary> Maximum deviation from PathFollower.ForwardAngle allowed during an Acceleration Jump. </summary>
	private readonly float MaxAccelerationJumpTurnAmount = Mathf.Pi * .1f;

	public override void EnterState()
	{
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.resetFlags.HasFlag(LockoutResource.ResetFlags.OnJump))
		{
			Player.RemoveLockoutData(Player.ActiveLockoutData);
		}

		if (Player.Skills.IsSpeedBreakActive)
			Player.Skills.ToggleSpeedBreak();

		Player.IsJumping = true;
		Player.AllowLandingSkills = true;

		turningVelocity = 0;
		jumpTimer = 0;
		isShortenedJump = false;
		isAccelerationJump = false;
		accelerationJumpHeight = Player.GlobalPosition.Y;
		isAccelerationJumpQueued = Player.ForceAccelerationJump;

		// Decide accleration jump based on jump charge
		if (!Player.ForceAccelerationJump && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
			isAccelerationJumpQueued = !Player.Skills.ConsumeJumpCharge();

		Player.ForceAccelerationJump = false;

		Player.IsOnGround = false;
		Player.CanJumpDash = true;
		if (Player.IsMovingBackward) // Kill speed when jumping backwards
			Player.MoveSpeed = 0;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(Player.Stats.JumpHeight);
		Player.ApplyMovement();

		Player.Lockon.IsMonitoring = true;
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
		Player.Animator.JumpAnimation();
		Player.Controller.ResetJumpBuffer();
	}

	public override void ExitState()
	{
		Player.IsJumping = false;
		Player.IsAccelerationJumping = false;

		// Reset attack state
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AccelJumpAttack))
			Player.AttackState = PlayerController.AttackStates.None;
	}

	public override PlayerState ProcessPhysics()
	{
		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) && !Input.IsActionPressed("button_jump"))
		{
			// Check for acceleration jump
			if (jumpTimer <= AccelerationJumpLength)
				isAccelerationJumpQueued = true;
			else
				isShortenedJump = true;
		}

		ProcessMoveSpeed();
		ProcessTurning();
		ProcessGravity();
		Player.ApplyMovement();
		Player.IsMovingBackward = Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.BackAngle);
		Player.CheckGround();
		Player.CheckWall(Vector3.Zero, !isAccelerationJump);
		if (Player.CheckCeiling())
			return null;
		Player.UpdateUpDirection();

		if (isAccelerationJump)
		{
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
				Player.VerticalSpeed = Mathf.Clamp(Player.VerticalSpeed, -Mathf.Inf, 0);
			}
		}
		else
		{
			jumpTimer += PhysicsManager.physicsDelta;

			if (jumpTimer > AccelerationJumpLength)
			{
				if (isAccelerationJumpQueued)
					StartAccelerationJump();

				if (!isAccelerationJump && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SpinJump))
				{
					Player.StartSpinJump(isShortenedJump);
					return null;
				}
			}
		}

		if (Player.IsOnGround)
			return landState;

		if (Player.VerticalSpeed <= 0 && !isAccelerationJump)
			return fallState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			if (SaveManager.Config.useStompJumpButtonMode)
				return stompState;

			return Player.Lockon.IsTargetAttackable ? homingAttackState : jumpDashState;
		}

		if (Player.Controller.IsAttackBufferActive)
		{
			Player.Controller.ResetAttackBuffer();
			return Player.Lockon.IsTargetAttackable ? homingAttackState : jumpDashState;
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

	protected override void AccelerateLockout()
	{
		if (jumpTimer <= AccelerationJumpLength ||
			(isAccelerationJump && Player.GlobalPosition.Y >= accelerationJumpHeight))
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 1f);
			return;
		}

		base.AccelerateLockout();
	}

	protected override void Accelerate(float inputStrength)
	{
		if (jumpTimer <= AccelerationJumpLength ||
			(isAccelerationJump && Player.GlobalPosition.Y >= accelerationJumpHeight))
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
			return;
		}

		base.Accelerate(inputStrength);
	}

	protected override void ProcessTurning()
	{
		base.ProcessTurning();

		if (isAccelerationJump) // Clamp acceleration jumps so they don't get out of control
			Player.MovementAngle = ExtensionMethods.ClampAngleRange(Player.MovementAngle, Player.PathFollower.ForwardAngle, MaxAccelerationJumpTurnAmount);
	}

	protected override void ProcessGravity()
	{
		if (isShortenedJump && Player.VerticalSpeed > 0)
			Player.VerticalSpeed *= JumpCurve; // Kill jump height

		base.ProcessGravity();
	}

	private void StartAccelerationJump()
	{
		isAccelerationJumpQueued = false;
		if (Player.DisableAccelerationJump)
			return;

		float inputAngle = Player.Controller.GetTargetMovementAngle();
		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
			Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
		{
			return;
		}

		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
			!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) &&
			(!Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.ForwardAngle) ||
			Player.Controller.GetInputStrength() < .5f))
		{
			return;
		}

		isAccelerationJump = true;
		Player.IsAccelerationJumping = true;
		if (ExtensionMethods.DeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle) > Mathf.Pi * .5f)
			Player.MovementAngle = Player.PathFollower.ForwardAngle;

		// Keep acceleration jump heights consistent
		Player.MoveSpeed = Mathf.Max(accelerationJumpSpeed, Player.MoveSpeed);
		Player.VerticalSpeed = accelerationJumpHeightVelocity;
		Player.Animator.JumpAccelAnimation();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AccelJumpAttack))
		{
			Player.Effect.PlayFireFX();
			Player.AttackState = PlayerController.AttackStates.Weak;
		}
	}
}
