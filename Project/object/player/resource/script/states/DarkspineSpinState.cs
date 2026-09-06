using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Handles the darkspine soul charging state. </summary>
public partial class DarkspineSpinState : PlayerState
{
	[Export] private PlayerState landState;
	[Export] private PlayerState fallState;

	private float slowChargeTimer;
	private float deactivateTimer;

	/// <summary> Amount to instantly charge when the button is pressed. </summary>
	private readonly int BurstChargeAmount = 2;
	/// <summary> How quickly to charge when held down. </summary>
	private readonly float SlowChargeInterval = 0.1f;
	/// <summary> How long to remain in the Spin State after the button is released (to allow for mashing). </summary>
	private readonly float DeactivationLength = 0.2f;
	private readonly float SpeedLoss = 40f;

	public override void EnterState()
	{
		deactivateTimer = DeactivationLength;
		slowChargeTimer = SlowChargeInterval;
		Player.Animator.StartSpin(5f);
		Player.Effect.StartDarkspineSpinFX(true);
		Player.Skills.ModifySoulGauge(BurstChargeAmount);
	}

	public override void ExitState()
	{
		Player.Animator.ResetState(0.2f);
		Player.Effect.StopDarkspineSpinFX();
	}

	public override PlayerState ProcessPhysics()
	{
		if (CheckExit())
			return Player.IsOnGround ? landState : fallState;

		if (Input.IsActionJustPressed("button_attack") || Player.Controller.IsDownShakeRegistered(0.2f)) // Provide more soul power when mashing
		{
			deactivateTimer = DeactivationLength;

			if (!Player.Skills.IsSpeedBreakActive) // Only allow mash charging when speedbreak isn't active (prevent carpel tunnel)
				Player.Skills.ModifySoulGauge(BurstChargeAmount);
		}

		ProcessGravity();
		ProcessMoveSpeed();
		ProcessTurning();
		Player.ApplyMovement();
		Player.CheckGround();

		slowChargeTimer = Mathf.MoveToward(slowChargeTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(slowChargeTimer))
		{
			Player.Skills.ModifySoulGauge(1);
			slowChargeTimer = SlowChargeInterval;
		}

		return null;
	}

	protected override void ProcessGravity()
	{
		if (Player.IsOnGround)
			return;

		if (Player.Skills.IsSpeedBreakActive)
		{
			// Slight gravity to prevent breaking stages
			Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * 0.5f * PhysicsManager.physicsDelta);
			return;
		}

		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, 0, SpeedLoss * PhysicsManager.physicsDelta);
		if (Player.VerticalSpeed > 0f) // Kill upward speed faster
			Player.VerticalSpeed *= 0.5f;
	}

	protected override void ProcessMoveSpeed()
	{
		if (!Player.Skills.IsSpeedBreakActive)
		{
			Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0, SpeedLoss * PhysicsManager.physicsDelta);
			float moveSpeedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
			Player.StrafeSpeed = Player.Stats.StrafeSettings.UpdateInterpolate(Player.StrafeSpeed * moveSpeedRatio, -1.0f); // Reset to 0 quickly
			return;
		}

		ProcessAutorunStrafeSpeed();
	}

	protected override void ProcessTurning()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return;

		base.ProcessTurning();
	}

	private bool CheckExit()
	{
		if (Input.IsActionPressed("button_attack"))
			return false;

		deactivateTimer = Mathf.MoveToward(deactivateTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(deactivateTimer))
			return true;

		return false;
	}
}
