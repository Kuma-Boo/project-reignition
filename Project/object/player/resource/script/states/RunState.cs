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

	public override void EnterState()
	{
		turningVelocity = 0;
		Player.IsMovingBackward = false;
		ProcessPhysics();
	}

	public override void ExitState()
	{
		if (Player.Animator.IsBrakeAnimationActive)
			Player.Animator.StopBrake();

		Player.Effect.IsEmittingStepDust = false;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.AddSlopeSpeed();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.CheckWall();
		Player.CheckCeiling();

		if (!Player.Skills.IsSpeedBreakActive)
		{
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

				return jumpState;
			}

			if (Player.Controller.IsActionBufferActive)
			{
				Player.Controller.ResetActionBuffer();
				return slideState;
			}
		}

		if (!Player.IsOnGround)
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.Controller.GetHoldingDistance(Player.MovementAngle, Player.PathFollower.ForwardAngle) >= 1.0f)
			return backstepState;

		if (Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed) > RunRatio &&
			StageSettings.Instance.IsLevelIngame)
		{
			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.CrestWind))
				Player.Skills.ActivateWindCrest();

			if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.CrestDark))
				Player.Skills.ActivateDarkCrest();
		}
		else
		{
			Player.Skills.ResetCrestTimer();
		}

		Player.Animator.RunAnimation();
		Player.Effect.IsEmittingStepDust = true;
		ProcessBrakeAnimation();
		return null;
	}

	protected override void Accelerate(float targetMovementAngle)
	{
		base.Accelerate(targetMovementAngle);

		if (!Player.Animator.IsBrakeAnimationActive)
			return;

		Player.Animator.StopBrake();
	}

	protected override void Turn(float targetMovementAngle, float turnSmoothing)
	{
		if (IsSpeedLossActive())
			ApplySpeedLoss(targetMovementAngle);

		base.Turn(targetMovementAngle, turnSmoothing);
	}

	private bool IsSpeedLossActive()
	{
		// Speedbreak is overriding speed
		if (Player.Skills.IsSpeedBreakActive) return false;

		// Autorun disables speed loss
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun)) return false;

		// Don't apply turning speed loss when moving quickly and holding the direction of the pathfollower
		if (Player.Controller.IsHoldingDirection(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle) &&
			Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) > .5f)
		{
			return false;
		}

		// Or when overriding speed/direction
		if (Player.IsLockoutActive &&
			(Player.ActiveLockoutData.overrideSpeed ||
			Player.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Free))
		{
			return false;
		}

		return true;
	}

	private void ApplySpeedLoss(float targetMovementAngle)
	{
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		float deltaAngle = Player.Controller.GetHoldingDistance(targetMovementAngle, Player.MovementAngle);

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

		if (Player.MoveSpeed > BrakeDeadzone && !StageSettings.Instance.IsLevelIngame)
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