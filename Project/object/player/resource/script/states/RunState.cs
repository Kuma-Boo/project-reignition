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
	private PlayerState jumpState;
	/// <summary> What speed ratio should be considered as fully running? </summary>
	private readonly float RunRatio = .9f;
	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	private readonly float MaxTurningAdjustment = Mathf.Pi * .25f;
	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	private readonly float TurningDampingRange = Mathf.Pi * .35f;

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

		return null;
	}

	private void ProcessMoveSpeed()
	{
		float inputStrength = Mathf.Min(Player.Controller.CameraInputAxis.Length(), 1f);
		if (inputStrength < Player.Controller.DeadZone)
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, 0);
			return;
		}

		float targetMovementAngle = Player.GetTargetMovementAngle();
		float inputDot = ExtensionMethods.DotAngle(targetMovementAngle, Player.PathFollower.ForwardAngle);
		if (inputDot < -.5f || Input.IsActionPressed("button_brake")) // Turning around
		{
			Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, -inputStrength);
			return;
		}

		ProcessTurning(targetMovementAngle, inputDot); // Turning only updates when accelerating
		Player.MoveSpeed = Player.Stats.GroundSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	}

	private void ProcessTurning(float targetMovementAngle, float inputDot)
	{
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

		// Normal turning
		float maxTurnAmount = Player.Stats.MaxTurnAmount;
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = (Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle));
		if (isRecentering)
			maxTurnAmount = Player.Stats.RecenterTurnAmount;

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
		Player.MovementAngle = ExtensionMethods.ClampAngleRange(Player.MovementAngle, Player.PathFollower.ForwardAngle, Mathf.Pi * .5f);
	}
}
