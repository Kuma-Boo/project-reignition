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
	private readonly float FallPreventionLength = 0.1f;
	public bool IsSteppingRight { get; set; }

	public override void EnterState()
	{
		currentStepLength = 0.0f;

		Player.Animator.StartQuickStep(IsSteppingRight);
		Player.Effect.PlayQuickStepFX(IsSteppingRight);
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Player.IsQuickStepValid) // Exit quick step state
			return runState;

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
		{
			Player.Velocity = Player.PathFollower.Right() * currentSpeed;
			Player.MoveAndSlide(); // Force player off the ledge
			return fallState;
		}

		if (!Player.Skills.IsSpeedBreakActive && Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (currentStepLength <= FallPreventionLength)
			return null;

		// Prevent player from flying off the ground if they're "close enough" to the grind step ending
		Vector3 groundCheckPosition = Player.CenterPosition;
		groundCheckPosition += Player.PathFollower.Right() * (currentSpeed * PhysicsManager.physicsDelta + Mathf.Sign(currentSpeed) * Player.CollisionSize.X);
		RaycastHit groundCheck = Player.CastRay(groundCheckPosition, -Player.UpDirection * Player.CollisionSize.Y * 2f, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(groundCheckPosition, -Player.UpDirection * Player.CollisionSize.Y * 2f, groundCheck ? Colors.Red : Colors.Pink);
		if (!groundCheck || !groundCheck.collidedObject.IsInGroup("floor"))
			currentStepLength = StepLength;

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

	protected override void ProcessTurning() => Player.MovementAngle = Player.PathFollower.ForwardAngle;
}
