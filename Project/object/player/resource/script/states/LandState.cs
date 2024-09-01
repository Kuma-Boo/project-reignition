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
		Player.UpdateOrientation();

		Player.VerticalSpeed = 0;
		Player.Lockon.IsMonitoring = false;
		Player.Lockon.ResetLockonTarget();
		Player.Animator.LandingAnimation();
		Player.Effect.PlayLandingFX();
		Player.State.DisableAccelerationJump = false;
		/*
		REFACTOR TODO
		if (allowLandingSkills && MovementState == MovementStates.Normal)
		{
			// Apply landing skills
			CheckLandingBoost();
			CheckLandingSoul();
		}

		allowLandingSkills = false;
		JustLandedOnGround = true;

		if (IsGrindstepBonusActive)
			IsGrindstepBonusActive = false;
		*/
	}

	public override PlayerState ProcessPhysics()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.IsMovingBackward)
			return backstepState;

		return runState;
	}
}
