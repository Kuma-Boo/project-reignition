using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class QuickStepState : PlayerState
{
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState backflipState;
	[Export]
	private PlayerState slideState;
	[Export]
	private PlayerState jumpState;

	[Export]
	private Curve movementCurve;

	private float currentStepLength;
	private readonly float StepLength = 0.3f;
	private readonly float InterruptLength = 0.2f;
	public bool IsSteppingRight { get; set; }

	public override void EnterState()
	{
		currentStepLength = 0.0f;

		Player.Animator.StartQuickStep(IsSteppingRight);
		Player.Effect.PlayQuickStepFX(IsSteppingRight);
	}

	public override PlayerState ProcessPhysics()
	{
		// TODO Use External Velocity from the movement
		currentStepLength += PhysicsManager.physicsDelta;
		float currentSpeed = -movementCurve.Sample(Mathf.Clamp(currentStepLength / StepLength, 0f, 1f));
		if (!IsSteppingRight)
			currentSpeed *= -1;

		Player.Velocity = Player.PathFollower.Right() * currentSpeed;
		Player.MoveAndSlide();

		ProcessTurning();
		Player.AddSlopeSpeed();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.CheckWall();
		if (Player.CheckCeiling())
			return null;

		if (!Player.IsOnGround)
			return fallState;

		if (!Player.Skills.IsSpeedBreakActive && Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (currentStepLength >= InterruptLength)
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

				if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
					return slideState;

				return jumpState;
			}

			if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
				Player.Controller.IsActionBufferActive)
			{
				Player.Controller.ResetActionBuffer();
				return slideState;
			}
		}

		if (currentStepLength >= StepLength)
			return runState;

		return null;
	}
}
