using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class BackflipState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;
	[Export]
	private float backflipHeight;

	private float turningVelocity;
	/// <summary> How much can the player adjust their angle while backflipping? </summary>
	private readonly float MaxBackflipAdjustment = Mathf.Pi * .25f;

	public override void EnterState()
	{
		turningVelocity = 0;
		Player.IsOnGround = false;
		Player.IsMovingBackward = true;
		Player.MovementAngle = Player.PathFollower.BackAngle;
		Player.MoveSpeed = Player.Stats.BackflipSettings.Speed;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(backflipHeight);

		Player.Lockon.IsMonitoring = true;
		Player.Animator.BackflipAnimation();
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.BackstepAttack))
		{
			Player.Effect.PlayFireFX();
			Player.State.AttackState = PlayerStateController.AttackStates.Weak;
		}
	}

	public override PlayerState ProcessPhysics()
	{
		UpdateMoveSpeed();
		UpdateVerticalSpeed();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.UpdateUpDirection(true, Player.PathFollower.HeightAxis);

		if (Player.IsOnGround)
			return landState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			if (Player.Lockon.Target != null && Player.Lockon.IsTargetAttackable)
				return homingAttackState;

			return jumpDashState;
		}

		return null;
	}

	private void UpdateMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		float targetMovementAngle = ExtensionMethods.ClampAngleRange(Player.Controller.GetTargetMovementAngle(), Player.PathFollower.BackAngle, MaxBackflipAdjustment);
		bool isHoldingForward = Player.Controller.IsHoldingDirection(targetMovementAngle, Player.PathFollower.ForwardAngle);// REFACTOR TODO: Extra arguments? , true, false);
		bool isHoldingBackward = Player.Controller.IsHoldingDirection(targetMovementAngle, Player.PathFollower.BackAngle);
		if (isHoldingForward || Input.IsActionPressed("button_brake"))
		{
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, -1);
			return;
		}

		if (isHoldingBackward)
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
		else if (Mathf.IsZeroApprox(inputStrength))
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, 0);

		UpdateTurning(targetMovementAngle);
	}

	private void UpdateTurning(float targetMovementAngle)
	{
		targetMovementAngle += Player.PathTurnInfluence;
		targetMovementAngle = Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.BackAngle);

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.BackAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.BackAngle);
		// Is the player trying to recenter themselves?
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float turnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle, targetMovementAngle, ref turningVelocity, turnAmount);
	}

	private void UpdateVerticalSpeed()
	{
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
	}
}
