using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class FlyingPotState : PlayerState
{
	public FlyingPot Pot { get; set; }

	[Export] private PlayerState jumpState;
	[Export] private PlayerState fallState;

	private float flapSpeed = 1;
	private float flapTimer;
	private const float MaxSpeed = 8.0f;
	private const float MaxAngle = Mathf.Pi * .2f;
	private const float WingAcceleration = 3f;
	private const float FlapInterval = .5f; // How long is a single flap?
	private const float FlapAccelerationLength = .4f; // How long does a flap accelerate?

	private readonly StringName AscendAction = "action_ascend";
	private readonly StringName ExitAction = "action_exit";

	public override void EnterState()
	{
		flapTimer = 0;
		Player.StartExternal(Pot, Pot.Root);
		Player.Animator.Visible = false;
		Player.MoveSpeed = Player.VerticalSpeed = 0;

		HeadsUpDisplay.Instance.SetPrompt(AscendAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(ExitAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
		Player.Knockback += OnPlayerDamaged;
	}

	public override void ExitState()
	{
		Player.CanJumpDash = true; // So the player isn't completely helpless
		Player.Skills.IsSpeedBreakEnabled = true;
		Player.Animator.Visible = true;
		Player.StopExternal();

		HeadsUpDisplay.Instance.HidePrompts();

		Pot = null;
		Player.Knockback -= OnPlayerDamaged;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.ExternalController != Pot) // Pot must have been shattered
			return fallState;

		Pot.UpdateAngle(Player.Controller.InputHorizontal * MaxAngle);

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Player.DisableAccelerationJump = true;
			LeavePot();
			return jumpState;
		}

		if (Player.Controller.IsActionBufferActive) // Move upwards
		{
			Player.Controller.ResetActionBuffer();

			Pot.Flap();
			flapSpeed = 1.5f + (flapTimer / FlapInterval);
			flapTimer = FlapInterval;
			return null;
		}

		UpdateFlap();
		UpdateAnimation();
		Pot.ApplyMovement();
		Player.UpdateExternalControl(); // Sync player object
		return null;
	}

	private void UpdateFlap()
	{
		if (Mathf.IsZeroApprox(flapTimer))
			return;

		if (Pot.Velocity < 0) // More responsive flap when falling
			Pot.Velocity *= .2f;

		flapTimer = Mathf.MoveToward(flapTimer, 0, PhysicsManager.physicsDelta);
		if (flapTimer < FlapAccelerationLength)
			return;

		// Accelerate
		Pot.Velocity += WingAcceleration;
		Pot.Velocity = Mathf.Min(Pot.Velocity, MaxSpeed);
	}

	private void OnPlayerDamaged()
	{
		if (Pot == null)
			return;

		Pot.Velocity = 0f;
		Pot.Shatter();

		ShowPlayer();
		Player.Knockback -= OnPlayerDamaged;
		Player.StartKnockback(new()
		{
			ignoreMovementState = true,
			disableDamage = true,
		});
	}

	private void UpdateAnimation()
	{
		flapSpeed = Mathf.Lerp(flapSpeed, 1, .1f);
		Pot.UpdateFlap(flapSpeed);
	}

	private void LeavePot()
	{
		Pot.Velocity = 0f; // Kill all velocity
		Pot.PlayExitFX();

		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Pot.Back());
		Player.VerticalSpeed = Runtime.CalculateJumpPower(Player.Stats.JumpHeight);

		Player.Animator.JumpAnimation();
		ShowPlayer();
	}

	private void ShowPlayer()
	{
		float angleRatio = Pot.Angle / MaxAngle;
		Player.Animator.SnapRotation(Player.MovementAngle - (Mathf.Pi * angleRatio));
		Player.Animator.Visible = true;
		Player.StopExternal();
	}
}
