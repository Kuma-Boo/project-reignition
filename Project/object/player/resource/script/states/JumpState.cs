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

	private float turningVelocity;

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

		UpdateMoveSpeed();
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

	private void UpdateMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (Mathf.IsZeroApprox(inputStrength))
		{
			Player.MoveSpeed = Player.Stats.AirSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float targetMovementAngle = Player.Controller.GetTargetMovementAngle();
		float inputDot = ExtensionMethods.DotAngle(Player.MovementAngle, targetMovementAngle);
		if ((inputDot < -.75f && !Mathf.IsZeroApprox(Player.MoveSpeed)) || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.AirSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		UpdateTurning(targetMovementAngle);
		if (isAccelerationJump)
		{
			// Prevent the player from losing speed during an acceleration jump
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
			return;
		}

		Player.MoveSpeed = Player.Stats.AirSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void UpdateTurning(float targetMovementAngle)
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.MovementAngle = targetMovementAngle;
			return;
		}
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);

		targetMovementAngle += Player.PathTurnInfluence;
		targetMovementAngle = Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.ForwardAngle);

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.ForwardAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float maxTurnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;

		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
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
		isAccelerationJump = true;
		isAccelerationJumpQueued = false;

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

		if (!Player.Controller.IsHoldingDirection(Player.MovementAngle, Player.PathFollower.ForwardAngle))
			Player.MovementAngle = Player.PathFollower.ForwardAngle;

		// Keep acceleration jump heights consistent
		Player.MoveSpeed = accelerationJumpSpeed;
		Player.VerticalSpeed = accelerationJumpHeightVelocity;
		Player.Animator.JumpAccelAnimation();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.AccelJumpAttack))
		{
			Player.Effect.PlayFireFX();
			Player.State.AttackState = PlayerStateController.AttackStates.Weak;
		}
	}
}
