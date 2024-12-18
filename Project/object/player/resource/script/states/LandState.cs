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
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.resetFlags.HasFlag(LockoutResource.ResetFlags.OnLand))
		{
			Player.RemoveLockoutData(Player.ActiveLockoutData);
		}

		Player.VerticalSpeed = 0;
		Player.UpdateOrientation();
		Player.SnapToGround();
		Player.DisableAccelerationJump = false;
		Player.Lockon.IsMonitoring = false;
		Player.Animator.LandingAnimation();
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
		Player.Effect.PlayLandingFX();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.CheckGround();
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

		if (Input.IsActionPressed("button_brake"))
			applyLandingBoost = false;

		if (!applyLandingBoost)
			return;

		// Only apply landing boost when holding forward to avoid accidents (See Sonic and the Black Knight)
		float inputStrength = Player.Controller.GetInputStrength();
		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) && Mathf.IsZeroApprox(inputStrength))
			return;

		float inputAngle = Player.Controller.GetTargetInputAngle();
		if (!Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.ForwardAngle))
			return;

		Player.Effect.PlayWindFX();
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.MoveSpeed = Mathf.Max(Player.MoveSpeed, Player.Skills.landingDashSpeed);
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
