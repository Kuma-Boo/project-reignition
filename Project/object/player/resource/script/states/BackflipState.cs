using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class BackflipState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;
	[Export]
	private PlayerState stompState;
	[Export]
	private float backflipHeight;

	/// <summary> How much can the player adjust their angle while backflipping? </summary>
	private readonly float MaxBackflipAdjustment = Mathf.Pi * .25f;

	public override void EnterState()
	{
		if (Player.Skills.IsSpeedBreakActive)
			Player.Skills.ToggleSpeedBreak();

		turningVelocity = 0;
		Player.IsOnGround = false;
		Player.IsMovingBackward = true;
		Player.IsBackflipping = true;
		Player.MovementAngle = Player.PathFollower.BackAngle;
		Player.MoveSpeed = Player.Stats.BackflipSettings.Speed;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(backflipHeight);

		Player.Lockon.IsMonitoring = true;
		Player.Animator.BackflipAnimation();
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.BackstepAttack))
		{
			Player.Effect.PlayFireFX();
			Player.AttackState = PlayerController.AttackStates.Weak;
		}
	}

	public override void ExitState()
	{
		Player.IsBackflipping = false;
		Player.AttackState = PlayerController.AttackStates.None;
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMoveSpeed();
		ProcessTurning();
		ProcessGravity();
		Player.ApplyMovement();
		Player.CheckGround();
		Player.CheckWall(Vector3.Zero, false);
		if (Player.CheckCeiling())
			return null;
		Player.UpdateUpDirection(true, Player.PathFollower.HeightAxis);

		if (Player.IsOnGround)
			return landState;

		if (Player.Controller.IsJumpBufferActive || Player.Controller.IsAttackBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Player.Controller.ResetAttackBuffer();
			if (Player.Lockon.IsTargetAttackable)
				return homingAttackState;

			return jumpDashState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LightSpeedDash) &&
			Player.Controller.IsLightDashBufferActive)
		{
			Player.StartLightSpeedDash();
		}

		return null;
	}

	protected override void ProcessMoveSpeed()
	{
		float inputAngle = Player.Controller.GetTargetInputAngle();
		float inputStrength = Player.Controller.GetInputStrength();

		if (Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.ForwardAngle) ||
			Player.Controller.IsBrakeHeld())
		{
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, -1);
			return;
		}

		if (Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
		else if (Mathf.IsZeroApprox(inputStrength))
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, 0);
	}

	protected override void ProcessTurning()
	{
		float pathControlAmount = Player.Controller.CalculatePathControlAmount();
		float targetMovementAngle = Player.Controller.GetTargetMovementAngle() + pathControlAmount;
		if (DisableTurning(targetMovementAngle))
			return;

		// Use GroundSettings so backstep turning feels consistent with the run state
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, Player.Stats.MaxTurnAmount, speedRatio);
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle + Player.PathTurnInfluence, targetMovementAngle, ref turningVelocity, turnSmoothing);
	}

	protected override bool DisableTurning(float targetMovementAngle)
	{
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace) // Direction is being overridden
		{
			Player.MovementAngle = targetMovementAngle;
			return true;
		}

		if (Player.Controller.IsHoldingDirection(targetMovementAngle, Player.MovementAngle + Mathf.Pi, Mathf.Pi * .2f))
		{
			// Check for turning around
			if (!Player.IsLockoutActive || Player.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Strafe)
				return true;
		}

		return false;
	}
}
