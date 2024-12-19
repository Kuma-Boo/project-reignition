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
	private PlayerState jumpState;
	[Export]
	private PlayerState backflipState;
	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		if (Player.MoveSpeed <= Player.Stats.InitialSlideSpeed)
			Player.MoveSpeed = Player.Stats.InitialSlideSpeed;

		Player.DisableSidle = true;
		Player.Animator.StartSliding();
		Player.Effect.PlayActionSFX(Player.Effect.SlideSfx);
		Player.ChangeHitbox("slide");

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideDefense))
		{
			Player.DisableDamage = true;
			Player.Effect.StartAegisFX();
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideAttack))
		{
			Player.Effect.PlayFireFX();
			Player.Effect.StartVolcanoFX();
			Player.AttackState = PlayerController.AttackStates.Weak;
			Player.ChangeHitbox("volcano-slide");
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
		Player.DisableSidle = false;
		Player.ChangeHitbox("RESET");

		if (!Player.IsDrifting &&
			Player.StateMachine.QueuedState != jumpState &&
			Player.StateMachine.QueuedState != crouchState)
		{
			Player.Skills.ConsumeJumpCharge();
		}

		if (!Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.Animator.StopCrouching();
			Player.Animator.CrouchToMoveTransition();
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideDefense))
		{
			Player.DisableDamage = false;
			Player.Effect.StopAegisFX();
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideAttack))
		{
			Player.Effect.StopVolcanoFX();
			Player.AttackState = PlayerController.AttackStates.None;
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
		Player.CheckWall();
		Player.CheckCeiling();

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.SlideExp))
			Player.Skills.UpdateSoulSlide();

		if (!Player.CheckGround())
			return fallState;

		if (Player.Skills.IsSpeedBreakActive)
			return runState;

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump))
		{
			Player.Skills.ChargeJump();
			if (!Input.IsActionPressed("button_jump"))
			{
				if (!Player.Controller.IsBrakeHeld())
					return jumpState;

				Player.Skills.ConsumeJumpCharge();
				return runState;
			}
		}
		else
		{
			if (!Input.IsActionPressed("button_action") && !Player.Animator.IsSlideTransitionActive)
				return runState;

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

				return jumpState;
			}
		}

		if (Player.IsLockoutDisablingActions)
			return runState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
		{
			Player.Animator.SlideToCrouch();
			Player.ChangeHitbox("crouch");
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
