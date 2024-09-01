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
	private float jumpHeight = 4.8f;
	[Export]
	private float jumpCurve = .95f;

	[Export]
	private float accelerationJumpSpeed = 36;
	[Export]
	private float accelerationJumpHeightVelocity = 5;

	private float jumpTimer;

	private bool isShortenedJump;
	private bool isAccelerationJump;
	private bool isAccelerationJumpQueued;

	/// <summary> How fast the jump button needs to be released to count as an Acceleration Jump. </summary>
	private readonly float AccelerationJumpLength = .1f;
	/// <summary> Maximum deviation from PathFollower.ForwardAngle allowed during an Acceleration Jump. </summary>
	private readonly float MaxAccelerationJumpTurnAmount = Mathf.Pi * .1f;

	public override void EnterState()
	{
		/* REFACTOR TODO
		currentJumpTime = ignoreAccelerationJump ? ACCELERATION_JUMP_LENGTH + PhysicsManager.physicsDelta : 0;
		allowLandingSkills = true;
		*/

		turningVelocity = 0;
		jumpTimer = 0;
		isShortenedJump = false;
		isAccelerationJump = false;
		isAccelerationJumpQueued = false;

		Player.IsOnGround = false;
		if (Player.IsMovingBackward) // Kill speed when jumping backwards
			Player.MoveSpeed = 0;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight);
		Player.ApplyMovement();

		Player.Lockon.IsMonitoring = true;
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
		Player.Animator.JumpAnimation();
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Input.IsActionPressed("button_jump"))
		{
			// Check for acceleration jump
			if (jumpTimer <= AccelerationJumpLength)
				isAccelerationJumpQueued = true;
			else
				isShortenedJump = true;
		}

		ProcessMoveSpeed();
		ProcessTurning();
		UpdateVerticalSpeed();
		Player.ApplyMovement();
		Player.IsMovingBackward = Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.BackAngle);

		if (Player.CheckGround())
			return landState;

		if (Player.VerticalSpeed <= 0 && !isAccelerationJump)
			return fallState;

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

		return null;
	}

	protected override void Accelerate(float inputStrength)
	{
		if (isAccelerationJump) // Clamp acceleration jumps so they don't get out of control
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

	private void UpdateVerticalSpeed()
	{
		if (isShortenedJump)
		{
			Player.VerticalSpeed *= jumpCurve; // Kill jump height
		}
		else if (!isAccelerationJump)
		{
			jumpTimer += PhysicsManager.physicsDelta;

			if (isAccelerationJumpQueued && jumpTimer > AccelerationJumpLength)
				StartAccelerationJump();
		}

		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
	}

	private void StartAccelerationJump()
	{
		isAccelerationJumpQueued = false;
		if (Player.DisableAccelerationJump)
			return;

		float inputAngle = Player.Controller.GetTargetMovementAngle();
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) &&
			Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
		{
			return;
		}

		if (!Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.ForwardAngle) ||
			Player.Controller.GetInputStrength() < .5f)
		{
			return;
		}

		isAccelerationJump = true;
		if (!Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.ForwardAngle))
			Player.MovementAngle = Player.PathFollower.ForwardAngle;

		// Keep acceleration jump heights consistent
		Player.MoveSpeed = accelerationJumpSpeed;
		Player.VerticalSpeed = accelerationJumpHeightVelocity;
		Player.Animator.JumpAccelAnimation();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AccelJumpAttack))
		{
			Player.Effect.PlayFireFX();
			Player.AttackState = PlayerController.AttackStates.Weak;
		}
	}
}
