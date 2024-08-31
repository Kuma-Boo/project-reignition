using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class SlideState : PlayerState
{
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState crouchState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		if (Player.MoveSpeed <= Player.Stats.InitialSlideSpeed)
			Player.MoveSpeed = Player.Stats.InitialSlideSpeed;

		Player.Animator.StartSliding();
		Player.Effect.PlayActionSFX(Player.Effect.SlideSfx);
		Player.State.ChangeHitbox("slide");

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideDefense))
			Player.Effect.StartAegisFX();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideAttack))
		{
			Player.Effect.PlayFireFX();
			Player.Effect.StartVolcanoFX();
			Player.State.AttackState = PlayerStateController.AttackStates.Weak;
			Player.State.ChangeHitbox("volcano-slide");
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideExp))
		{
			Player.Skills.StartSoulSlide();
			Player.Effect.StartSoulSlideFX();
			Player.Effect.PlayDarkSpiralFX();
		}
	}

	public override void ExitState()
	{
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideDefense))
			Player.Effect.StopAegisFX();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideAttack))
		{
			Player.Effect.StopVolcanoFX();
			Player.State.AttackState = PlayerStateController.AttackStates.None;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideExp))
			Player.Effect.StopSoulSlideFX();
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		Player.AddSlopeSpeed(true);
		Player.ApplyMovement();
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideExp))
			Player.Skills.UpdateSoulSlide();

		if (!Player.CheckGround())
			return fallState;

		if (!Input.IsActionPressed("button_action") && !Player.Animator.IsSlideTransitionActive)
		{
			Player.Animator.StopCrouching(0.2f);
			Player.Animator.CrouchToMoveTransition();
			return runState;
		}

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.Animator.SlideToCrouch();
			Player.State.ChangeHitbox("crouch");
			return crouchState;
		}

		return null;
	}

	protected override void ProcessMoveSpeed()
	{
		Player.Stats.UpdateSlideSpeed(Player.SlopeRatio);

		// Influence speed based on input strength
		float inputAmount = -.5f; // Default to halfway
		float inputStrength = Player.Controller.GetInputStrength();
		float inputAngle = Player.Controller.GetTargetMovementAngle();
		if (Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			inputAmount = -(1 + inputStrength) * .5f; // -0.5 to -1
		else if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun))
			inputAmount = 0;
		else if (Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.ForwardAngle))
			inputAmount = -(1 - inputStrength) * .5f; // 0 to -0.5

		inputAmount -= Player.SlopeRatio * Player.Stats.slopeInfluence;
		inputAmount = Mathf.Clamp(inputAmount, 0, 1);
		Player.MoveSpeed = Player.Stats.SlideSettings.UpdateSlide(Player.MoveSpeed, inputAmount);
	}
}
