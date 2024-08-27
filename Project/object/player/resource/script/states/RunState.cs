using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class RunState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState backstepState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState backflipState;

	[Export]
	private Curve turningSpeedLossCurve;

	/// <summary> What speed ratio should be considered as fully running? </summary>
	private readonly float RunRatio = .9f;
	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	private readonly float MaxTurningAdjustment = Mathf.Pi * .25f;
	/// <summary> How much speed to lose when turning sharply. </summary>
	private readonly float TurningSpeedLoss = .02f;

	private float turningVelocity;

	public override void EnterState()
	{
		Player.IsMovingBackward = false;
		turningVelocity = 0;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		Player.ApplyMovement();

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();

			float inputAngle = Player.GetTargetMovementAngle();
			float inputStrength = Player.Controller.GetInputStrength();
			if (!Mathf.IsZeroApprox(inputStrength) &&
				Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			{
				return backflipState;
			}

			return jumpState;
		}

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (ExtensionMethods.DotAngle(Player.MovementAngle, Player.PathFollower.ForwardAngle) < -.1f)
			return backstepState;

		return null;
	}

	private void ProcessMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (Mathf.IsZeroApprox(inputStrength))
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float targetMovementAngle = Player.GetTargetMovementAngle();
		float inputDot = ExtensionMethods.DotAngle(Player.MovementAngle, targetMovementAngle);
		if ((inputDot < -.75f && !Mathf.IsZeroApprox(Player.MoveSpeed)) || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		ProcessTurning(targetMovementAngle); // Turning only updates when accelerating
		ApplySpeedLoss(targetMovementAngle, inputDot);
		Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void ProcessTurning(float targetMovementAngle)
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.MovementAngle = targetMovementAngle;
			return;
		}
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);

		targetMovementAngle += Player.PathTurnInfluence;
		targetMovementAngle = Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.ForwardAngle);
		if (speedRatio > RunRatio)
			targetMovementAngle = ExtensionMethods.ClampAngleRange(targetMovementAngle, Player.PathFollower.ForwardAngle, MaxTurningAdjustment);

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, targetMovementAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float maxTurnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;

		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
	}

	private void ApplySpeedLoss(float targetMovementAngle, float inputDot)
	{
		float pathDot = ExtensionMethods.DotAngle(targetMovementAngle, Player.PathFollower.ForwardAngle);
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		// Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
		if (pathDot >= .5f && speedRatio >= .5f)
			return;

		// Calculate turn delta, relative to ground speed
		float speedLossRatio = speedRatio * ((inputDot - 1.0f) * -.5f);
		Player.MoveSpeed -= Player.Stats.GroundSettings.Speed * turningSpeedLossCurve.Sample(speedLossRatio) * TurningSpeedLoss;
		Player.MoveSpeed = Mathf.Max(Player.MoveSpeed, 0);
	}
}