using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class FallState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;

	private float turningVelocity;

	public override PlayerState ProcessPhysics()
	{
		UpdateMoveSpeed();
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		Player.ApplyMovement();

		if (Player.CheckGround())
			return landState;

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

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, targetMovementAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float maxTurnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;

		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
	}
}
