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
	private PlayerState slideState;
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
	/// <summary> Minimum speed needed to finish the braking animation. </summary>
	private readonly float BrakeDeadzone = 5f;

	private float turningVelocity;

	public override bool ProcessOnEnter => true;
	public override void EnterState()
	{
		turningVelocity = 0;

		Player.IsMovingBackward = false;
		Player.Effect.IsEmittingStepDust = true;
	}

	public override void ExitState()
	{
		if (Player.Animator.IsBrakeAnimationActive)
			Player.Animator.StopBrake();
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.AddSlopeSpeed();
		Player.ApplyMovement();

		Player.Animator.RunAnimation();
		ProcessBrakeAnimation();

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

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return slideState;
		}

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (ExtensionMethods.DotAngle(Player.MovementAngle, Player.PathFollower.ForwardAngle) < -.1f)
			return backstepState;

		return null;
	}

	protected override void Turn(float targetMovementAngle, float turnSmoothing)
	{
		if (IsSpeedLossActive(targetMovementAngle))
			ApplySpeedLoss(targetMovementAngle);

		base.Turn(targetMovementAngle, turnSmoothing);
	}

	private bool IsSpeedLossActive(float targetMovementAngle)
	{
		// Speedbreak is overriding speed
		if (Player.Skills.IsSpeedBreakActive) return false;

		// Autorun disables speed loss
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun)) return false;

		// Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
		if (Player.Controller.IsHoldingDirection(targetMovementAngle, Player.PathFollower.ForwardAngle) &&
			Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) > .5f)
		{
			return false;
		}

		// Or when overriding speed/direction
		if (Player.State.IsLockoutActive &&
			(Player.State.ActiveLockoutData.overrideSpeed ||
			Player.State.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free))
		{
			return false;
		}

		return true;
	}

	private void ApplySpeedLoss(float targetMovementAngle)
	{
		float deltaAngle = Player.Controller.GetHoldingDistance(targetMovementAngle, Player.MovementAngle);
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);

		// Calculate turn delta, relative to ground speed
		float speedLossRatio = speedRatio * deltaAngle;
		Player.MoveSpeed -= Player.Stats.GroundSettings.Speed * turningSpeedLossCurve.Sample(speedLossRatio) * TurningSpeedLoss;
		Player.MoveSpeed = Mathf.Max(Player.MoveSpeed, 0);
	}

	protected override void Brake()
	{
		base.Brake();

		if (Player.Animator.IsBrakeAnimationActive) // Already active
			return;

		if (Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) < RunRatio)
			return;

		Player.Animator.StartBrake();
		Player.Effect.PlayActionSFX(Player.Effect.SlideSfx);
	}

	private void ProcessBrakeAnimation()
	{
		if (!Player.Animator.IsBrakeAnimationActive)
			return;

		if (Player.MoveSpeed > BrakeDeadzone)
			return;

		Player.Animator.StopBrake();
	}

	protected override float ProcessTargetMovementAngle(float targetMovementAngle)
	{
		targetMovementAngle = base.ProcessTargetMovementAngle(targetMovementAngle);

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		if (speedRatio > RunRatio)
			targetMovementAngle = ExtensionMethods.ClampAngleRange(targetMovementAngle, Player.PathFollower.ForwardAngle, MaxTurningAdjustment);

		return targetMovementAngle;
	}
}