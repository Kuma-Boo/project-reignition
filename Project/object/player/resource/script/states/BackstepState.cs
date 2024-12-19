using Godot;
using Project.Core;

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
	private PlayerState crouchState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState backflipState;

	public override void EnterState()
	{
		turningVelocity = 0;
		Player.IsMovingBackward = true;
		ProcessPhysics();
	}

	public override void ExitState()
	{
		Player.Effect.IsEmittingStepDust = false;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.CheckWall();
		Player.CheckCeiling();

		if (!Player.IsOnGround)
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.Controller.GetHoldingDistance(Player.MovementAngle, Player.PathFollower.ForwardAngle) < 1.0f)
			return runState;

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();

			float inputAngle = Player.Controller.GetTargetInputAngle();
			float inputStrength = Player.Controller.GetInputStrength();
			if (!Mathf.IsZeroApprox(inputStrength) &&
				Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			{
				return backflipState;
			}

			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
				return crouchState;

			return jumpState;
		}

		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
			Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return crouchState;
		}

		Player.Animator.BackstepAnimation();
		Player.Effect.IsEmittingStepDust = true;
		return null;
	}

	protected override void Deccelerate() => Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, 0);
	protected override void Accelerate(float inputStrength) => Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
	protected override void Brake() => Player.MoveSpeed = Player.Stats.BackstepSettings.UpdateInterpolate(Player.MoveSpeed, -1);

	protected override void ProcessTurning()
	{
		float pathControlAmount = Player.Controller.CalculatePathControlAmount();
		float targetMovementAngle = Player.Controller.GetTargetMovementAngle() + pathControlAmount;
		if (DisableTurning(targetMovementAngle))
			return;

		// Use GroundSettings so backstep turning feels consistent with the run state
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, Player.Stats.MaxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
	}

	protected override float ProcessTargetMovementAngle(float targetMovementAngle) => Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.BackAngle);
}