using Godot;
using Project.Core;

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
		Vector3 originalVelocity = Player.Velocity;
		Player.Velocity = Player.UpDirection * Player.VerticalSpeed;
		Player.MoveAndSlide();
		Player.Velocity = originalVelocity;
		Player.UpdateOrientation();

		Player.VerticalSpeed = 0;
		Player.Lockon.IsMonitoring = false;
		Player.DisableAccelerationJump = false;
	}

	public override void ExitState()
	{
		// Snap to ground
		if (Player.IsGrinding)
			return;

		// IsStomping is set false here so LandingSkills can check against it
		Player.IsStomping = false;
		Player.IsGrindstepping = false;
		Player.AllowLandingGrind = false;
		Player.Animator.LandingAnimation();
		Player.Effect.PlayLandingFX();
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.AllowLandingSkills)
		{
			// Apply landing skills
			CheckLandingBoost();
			CheckLandingSoul();

			Player.AllowLandingSkills = false;
		}

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		if (Player.IsMovingBackward)
			return backstepState;

		return runState;
	}

	private void CheckLandingBoost()
	{
		bool applyLandingBoost = (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.StompDash) && Player.IsStomping) ||
			(SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LandDash) && !Player.IsStomping);

		if (!applyLandingBoost)
			return;

		// Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
		if (Player.Controller.IsHoldingDirection(Player.Controller.GetTargetInputAngle(), Player.PathFollower.ForwardAngle))
		{
			Player.Effect.PlayWindFX();
			Player.MovementAngle = Player.PathFollower.ForwardAngle;
			Player.MoveSpeed = Mathf.Max(Player.MoveSpeed, Player.Skills.landingDashSpeed);
		}
	}

	private void CheckLandingSoul()
	{
		// Bonus EXP
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.StompExp) && Player.IsStomping)
		{
			Player.Effect.PlayDarkSpiralFX();
			StageSettings.Instance.CurrentEXP += 2;
		}

		// Increase soul gauge
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LandSoul) && !Player.IsStomping)
		{
			Player.Effect.PlayDarkSpiralFX();

			switch (SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.LandSoul))
			{
				case 0:
					Player.Skills.ModifySoulGauge(1);
					break;
				case 1:
					Player.Skills.ModifySoulGauge(2);
					break;
				case 2:
					Player.Skills.ModifySoulGauge(4);
					break;
				case 3:
					Player.Skills.ModifySoulGauge(4 + (Mathf.Min(StageSettings.Instance.CurrentRingCount, 5) * 2));
					StageSettings.Instance.UpdateRingCount(5, StageSettings.MathModeEnum.Subtract, true);
					break;
			}
		}
	}
}
