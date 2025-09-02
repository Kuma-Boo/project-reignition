using Godot;

namespace Project.Gameplay;

public partial class PetrifyState : PlayerState
{
	[Export] private PlayerState idleState;

	private float animationVelocity;
	private int currentPetrificationStrength;

	private readonly float AnimationSmoothing = 2f;
	private readonly int MaxPetrificationStrength = 10;

	private readonly string EscapeAction = "action_escape";

	public override void EnterState()
	{
		currentPetrificationStrength = MaxPetrificationStrength;

		Player.MoveSpeed = 0;
		Player.VerticalSpeed = 0;

		Player.Animator.StartPetrify();

		HeadsUpDisplay.Instance.SetPrompt(EscapeAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(null, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		Player.Animator.StopPetrify();
		Player.Effect.PetrifyShatterFX();
		HeadsUpDisplay.Instance.HidePrompts();

		if (Player.IsOnGround) // Ensure landing animation is played properly
			Player.Animator.SnapToGround();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		ProcessGravity();
		Player.CheckGround();
		Player.ApplyMovement();

		if (Player.Controller.IsGimmickBufferActive)
		{
			Player.Animator.ShakePetrify();
			Player.Controller.ResetGimmickBuffer();
			currentPetrificationStrength--;

			if (currentPetrificationStrength == 0)
				return idleState;
		}

		return null;
	}
}
