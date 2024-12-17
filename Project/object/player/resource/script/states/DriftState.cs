using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class DriftState : PlayerState
{
	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState crouchState;
	[Export]
	private PlayerState slideState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private LockoutResource LockoutSettings { get; set; }

	public DriftTrigger Trigger { get; set; }

	/// <summary> Did the player enter the drift trigger charging a jump? </summary>
	private bool isChargingJump;
	private float entrySpeed;
	private enum DriftStatus
	{
		Processing, // Drift is being processed
		TimingFail, // The player mistimed the input
		WaitFail, // The player slid to a stop
		JumpFail, // Player jumped out of the drift
		Success, // Player successfully drifted
	}
	/// <summary> Result of the drift. </summary>
	private DriftStatus driftStatus;

	/// <summary> For smooth damping. </summary>
	private Vector3 driftVelocity;
	/// <summary> Positional smoothing. </summary>
	private readonly float DriftSmoothing = .25f;
	/// <summary> How generous the input window is (Due to player's decceleration, it's harder to get an early drift.) </summary>
	private readonly float InputWindowDistance = 1f;
	/// <summary> Delay animation state reset for this amount of time. </summary>
	private float driftAnimationTimer;
	/// <summary> Length of animation when player succeeds. </summary>
	private const float LaunchAnimationLength = .4f;
	/// <summary> Length of animation when player faceplants. </summary>
	private const float FailAnimationLength = .8f;

	public override void EnterState()
	{
		Trigger.Activate();

		driftAnimationTimer = 0;
		entrySpeed = Player.MoveSpeed;
		driftVelocity = Vector3.Zero;
		driftStatus = DriftStatus.Processing;

		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Effect.StartDust();
		Player.Animator.ExternalAngle = Player.MovementAngle;
		Player.Animator.StartDrift(Trigger.IsRightTurn);

		isChargingJump = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) && Input.IsActionPressed("button_jump");
	}

	public override void ExitState()
	{
		Player.Effect.StopDust();
		Player.Skills.IsSpeedBreakEnabled = true;

		if (driftStatus != DriftStatus.JumpFail)
			Player.Animator.ResetState(.4f);

		Trigger.Deactivate();
		Trigger = null;
	}

	public override PlayerState ProcessPhysics()
	{
		if (UpdateChargeJump())
			return jumpState;

		if (driftAnimationTimer > 0)
		{
			if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
				Player.Controller.IsJumpBufferActive)
			{
				Player.Controller.ResetJumpBuffer();
				return jumpState;
			}

			driftAnimationTimer = Mathf.MoveToward(driftAnimationTimer, 0, PhysicsManager.physicsDelta);
			Player.ApplyMovement();

			if (Mathf.IsZeroApprox(driftAnimationTimer))
			{
				if (isChargingJump)
					return driftStatus == DriftStatus.Success ? slideState : crouchState;
				else
					return driftStatus == DriftStatus.Success ? runState : idleState;
			}

			return null;
		}

		if (driftStatus == DriftStatus.Success)
			SuccessfulDrift();

		if (!SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ChargeJump) &&
			Player.Controller.IsJumpBufferActive) // Allow character to jump out of drift at any time
		{
			Player.Controller.ResetJumpBuffer();
			StartJumpFail();
			return jumpState;
		}

		Vector3 targetPosition = Trigger.MiddlePosition + (Trigger.Back() * InputWindowDistance);
		float distance = Player.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());
		ProcessEntranceMovement(targetPosition, distance);
		return null;
	}

	private void StartJumpFail()
	{
		driftStatus = DriftStatus.JumpFail;
		Trigger.ApplyBonus(false);

		Player.MoveSpeed = driftVelocity.Length(); // Keep speed from drift
		Player.Animator.ResetState(0f);
	}

	/// <summary> Returns true if the player releases a charge jump. </summary>
	private bool UpdateChargeJump()
	{
		if (Input.IsActionPressed("button_jump"))
		{
			isChargingJump = true;
			Player.Skills.ChargeJump();
			return false;
		}

		if (!isChargingJump)
			return false;

		if (Mathf.IsZeroApprox(driftAnimationTimer)) // Entry Jump
		{
			StartJumpFail();
			return true;
		}

		return true;
	}

	private void ProcessEntranceMovement(Vector3 targetPosition, float distance)
	{
		if (driftStatus != DriftStatus.Processing && driftStatus != DriftStatus.TimingFail)
			return;

		// Process drift
		Player.GlobalPosition = Player.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DriftSmoothing, entrySpeed);
		Player.UpDirection = Player.PathFollower.Up(); // Use pathfollower's up direction when drifting
		Player.UpdateExternalControl(true);
		Player.UpdateOrientation();

		// Fade out sfx based on distance
		Trigger.UpdateSfxVolume(distance);
		AttemptDrift(distance);
		AttemptWaitFail(distance);
	}

	private void AttemptDrift(float distance)
	{
		bool isManualDrift = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.DriftExp);
		bool isAttemptingDrift = ((Input.IsActionJustPressed("button_action") && isManualDrift) ||
			(!isManualDrift && distance <= InputWindowDistance)) && driftStatus != DriftStatus.TimingFail;

		if (!isAttemptingDrift)
			return;

		if (distance <= InputWindowDistance * 2f) // Successful drift
		{
			SuccessfulDrift();
			driftStatus = DriftStatus.Success;
			driftAnimationTimer = LaunchAnimationLength;

			if (isManualDrift)
			{
				BonusManager.instance.QueueBonus(new(BonusType.EXP, 100));
				Player.Skills.ModifySoulGauge(10); // Not written in skill description, but that's what the original game does -_-
				Player.Effect.PlayDarkSpiralFX();
			}
		}
		else // Too early! Fail drift attempt and play a special animation?
		{
			driftStatus = DriftStatus.TimingFail;
			driftAnimationTimer = FailAnimationLength;
			Trigger.FadeSfx();
			return;
		}

		Trigger.ApplyBonus(true);
	}

	private void AttemptWaitFail(float distance)
	{
		if (distance >= .3f)
			return;

		driftStatus = DriftStatus.WaitFail;
		driftAnimationTimer = PhysicsManager.physicsDelta;
		Player.MoveSpeed = 0f; // Reset Movespeed
		Trigger.FadeSfx();
	}

	private void SuccessfulDrift()
	{
		driftAnimationTimer = LaunchAnimationLength;
		Player.MoveSpeed = Player.Stats.GroundSettings.Speed;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Trigger.ExitDirection, Player.PathFollower.Up());
		Player.MovementAngle -= Mathf.Pi * .1f * Player.Controller.InputHorizontal;
		Player.AddLockoutData(LockoutSettings); // Apply lockout

		Player.Animator.LaunchDrift();
		Player.Animator.ExternalAngle = Player.MovementAngle;

		Trigger.FadeSfx();
	}
}
