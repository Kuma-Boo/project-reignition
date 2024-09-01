using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class GrindStepState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState stompState;

	/// <summary> How high to jump during a grindstep. </summary>
	[Export]
	private float GrindStepHeight = 1.6f;
	/// <summary> How fast to move during a grindstep. </summary>
	[Export]
	private float GrindStepSpeed = 28.0f;

	public override void EnterState()
	{
		// Delta angle to rail's movement direction (NOTE - Due to Godot conventions, negative is right, positive is left)
		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.Controller.GetTargetInputAngle(), Player.MovementAngle);
		// Calculate how far player is trying to go
		float horizontalTarget = GrindStepSpeed * Mathf.Sign(inputDeltaAngle);
		horizontalTarget *= Mathf.SmoothStep(0.5f, 1f, Player.Controller.GetInputStrength(true)); // Give some smoothing based on controller strength

		// Keep some speed forward
		turningVelocity = 0;
		Player.MovementAngle += Mathf.Pi * .25f * Mathf.Sign(inputDeltaAngle);
		Player.VerticalSpeed = Runtime.CalculateJumpPower(GrindStepHeight);
		Player.MoveSpeed = new Vector2(horizontalTarget, Player.MoveSpeed).Length();
		turnInstantly = true;

		Player.CanJumpDash = false; // Disable jumpdashing
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
		Player.Animator.StartGrindStep();
	}

	public override void ExitState()
	{
		Player.MovementAngle = Player.Animator.VisualAngle;
		Player.Animator.ResetState(.1f);
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		ProcessTurning();
		Player.ApplyMovement();

		if (Player.CheckGround())
			return landState;

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}
}
