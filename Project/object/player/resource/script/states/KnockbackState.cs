using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class KnockbackState : PlayerState
{
	[Export] private PlayerState landState;
	[Export] private PlayerState jumpState;
	public KnockbackSettings Settings { get; set; }
	public KnockbackSettings PreviousSettings { get; set; }
	private readonly float DamageFriction = 20f;

	public override void EnterState()
	{
		Player.Camera.SetLockonTarget(null);

		if (Player.Skills.IsSpeedBreakActive) // Disable speedbreak
			Player.Skills.ToggleSpeedBreak();

		Player.IsKnockback = true;
		Player.MovementAngle = Player.PathFollower.ForwardAngle; // Prevent being knocked sideways

		Player.Animator.StartHurt(Settings.knockForward);
		Player.Animator.ResetState();
		PreviousSettings = Settings;

		Player.MoveSpeed = Settings.overrideKnockbackSpeed ? Settings.knockbackSpeed : 8f;
		if (!Settings.knockForward)
			Player.MoveSpeed *= -1;

		if (!Settings.stayOnGround)
		{
			Player.IsOnGround = false;
			Player.VerticalSpeed = Runtime.CalculateJumpPower(Settings.overrideKnockbackHeight ? Settings.knockbackHeight : 1);
		}

		if (Player.ExternalController != null)
			return; // Only allow autorespawning when not using external controller

		if (Player.IsInvincible)
			return;
		Player.StartInvincibility();

		if (Settings.disableDamage)
			return;

		Player.TakeDamage();
	}

	public override void ExitState()
	{
		if (Settings.knockForward) // NOTE: This is handled in LandState if we're being knocked forward
			return;

		Player.IsKnockback = false;
		Player.Animator.StopHurt(Settings.knockForward);
		Player.Animator.ResetState();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0, DamageFriction * PhysicsManager.physicsDelta);
		Player.VerticalSpeed -= Runtime.Gravity * PhysicsManager.physicsDelta;
		Player.ApplyMovement();
		Player.UpdateUpDirection();

		if (!Settings.disableDownCancel &&
			SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.DownCancel) &&
			Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Player.ForceAccelerationJump = true;
			return jumpState;
		}

		if (!Settings.stayOnGround && Player.CheckGround())
			return landState;

		return null;
	}
}

public struct KnockbackSettings
{
	/// <summary> Should the player be knocked forward? Default is false. </summary>
	public bool knockForward;
	/// <summary> Knock the player around without bouncing them into the air. </summary>
	public bool stayOnGround;
	/// <summary> Apply knockback even when invincible? </summary>
	public bool ignoreInvincibility;
	/// <summary> Don't damage the player? </summary>
	public bool disableDamage;
	/// <summary> Always apply knockback, regardless of state. </summary>
	public bool ignoreMovementState;

	/// <summary> Don't allow the player to down cancel damage? </summary>
	public bool disableDownCancel;

	/// <summary> Override default knockback amount? </summary>
	public bool overrideKnockbackSpeed;
	/// <summary> Speed to assign to player. </summary>
	public float knockbackSpeed;

	/// <summary> Override default knockback height? </summary>
	public bool overrideKnockbackHeight;
	/// <summary> Height to move player by. </summary>
	public float knockbackHeight;
}