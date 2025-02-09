using Godot;

namespace Project.Gameplay;

public partial class PetrifyState : PlayerState
{
	[Export] private PlayerState idleState;

	private float animationVelocity;
	private readonly float AnimationSmoothing = 2f;
	private int currentPetrificationStrength;
	private readonly int MaxPetrificationStrength = 10;

	public override void EnterState()
	{
		currentPetrificationStrength = MaxPetrificationStrength;

		Player.MoveSpeed = 0;
		Player.VerticalSpeed = 0;

		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		Player.Animator.StartPetrify();
	}

	public override void ExitState()
	{
		Player.Animator.StopPetrify();
		Player.Effect.StartPetrifyShatterFX();

		if (Player.IsOnGround) // Ensure landing animation is played properly
		{
			Player.Animator.ResetState(0);
			Player.Animator.SnapToGround();
		}
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessGravity();
		Player.CheckGround();
		Player.ApplyMovement();

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Animator.ShakePetrify();
			Player.Controller.ResetActionBuffer();
			currentPetrificationStrength--;

			if (currentPetrificationStrength == 0)
				return idleState;
		}

		return null;
	}
}
