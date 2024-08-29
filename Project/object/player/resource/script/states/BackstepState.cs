using Godot;

namespace Project.Gameplay;

public partial class BackstepState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState backflipState;

	private float turningVelocity;

	public override bool ProcessOnEnter => true;
	public override void EnterState()
	{
		turningVelocity = 0;

		Player.IsMovingBackward = true;
		Player.Effect.IsEmittingStepDust = false;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		Player.ApplyMovement();
		Player.Animator.BackstepAnimation();

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (ExtensionMethods.DotAngle(Player.MovementAngle, Player.PathFollower.ForwardAngle) > -.1f)
			return runState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();

			float inputAngle = Player.Controller.GetTargetMovementAngle();
			float inputStrength = Player.Controller.GetInputStrength();
			if (!Mathf.IsZeroApprox(inputStrength) &&
				Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			{
				return backflipState;
			}

			return jumpState;
		}

		return null;
	}

	private void ProcessMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (Mathf.IsZeroApprox(inputStrength))
		{
			Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float targetMovementAngle = Player.Controller.GetTargetMovementAngle();
		float inputDot = ExtensionMethods.DotAngle(Player.MovementAngle, targetMovementAngle);
		if ((inputDot <= 0f && !Mathf.IsZeroApprox(Player.MoveSpeed)) || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		ProcessTurning(targetMovementAngle);
		Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void ProcessTurning(float targetMovementAngle)
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed)) // Turn instantly
		{
			turningVelocity = 0;
			Player.MovementAngle = targetMovementAngle;
			return;
		}

		// Use GroundSettings so backstep turning feels consistent with the run state
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, Player.Stats.MaxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
	}
}