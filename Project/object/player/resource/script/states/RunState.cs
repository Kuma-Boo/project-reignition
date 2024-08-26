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
	private Curve turningSpeedLossCurve;

	/// <summary> What speed ratio should be considered as fully running? </summary>
	private readonly float RunRatio = .9f;
	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	private readonly float MaxTurningAdjustment = Mathf.Pi * .25f;
	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	private readonly float TurningDampingRange = Mathf.Pi * .35f;
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
			return jumpState;
		}

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (ExtensionMethods.DotAngle(Player.MovementAngle, Player.PathFollower.ForwardAngle) < 0)
			return backstepState;

		return null;
	}

	private void ProcessMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		if (inputStrength < Player.Controller.DeadZone)
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

		ProcessTurning(targetMovementAngle, inputDot); // Turning only updates when accelerating
		ApplySpeedLoss(targetMovementAngle, inputDot);
		Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void ProcessTurning(float targetMovementAngle, float inputDot)
	{
		targetMovementAngle += Player.PathTurnInfluence;
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.MovementAngle = targetMovementAngle;
			return;
		}

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, targetMovementAngle);
		if (Runtime.Instance.IsUsingController &&
			inputDot > .8f &&
			Mathf.Abs(inputDeltaAngle) < TurningDampingRange)
		{
			// Remap controls to provide more analog detail
			targetMovementAngle -= inputDeltaAngle * .5f;
		}

		if (Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed) > RunRatio)
			targetMovementAngle = ExtensionMethods.ClampAngleRange(targetMovementAngle, Player.PathFollower.ForwardAngle, MaxTurningAdjustment);

		float maxTurnAmount = Player.Stats.MaxTurnAmount;
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle);
		if (isRecentering)
			maxTurnAmount = Player.Stats.RecenterTurnAmount;

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
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