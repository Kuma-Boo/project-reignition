using Godot;

namespace Project.Gameplay;

public partial class LandState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState backstepState;

	public override void EnterState()
	{
		// Snap to ground
		Vector3 originalVelocity = Player.Velocity;
		Player.Velocity = Player.UpDirection * Player.VerticalSpeed;
		Player.MoveAndSlide();
		Player.Velocity = originalVelocity;

		Player.VerticalSpeed = 0;
		Player.Lockon.IsMonitoring = false;
		Player.Lockon.ResetLockonTarget();

		/*
		REFACTOR TODO 
		if (allowLandingSkills && MovementState == MovementStates.Normal)
		{
			// Apply landing skills
			CheckLandingBoost();
			CheckLandingSoul();
		}

		allowLandingSkills = false;

		// Play FX
		Effect.PlayLandingFX();

		JustLandedOnGround = true;
		*/
	}

	public override PlayerState ProcessPhysics()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.MoveSpeed > 0)
			return runState;

		return backstepState;
	}
}
