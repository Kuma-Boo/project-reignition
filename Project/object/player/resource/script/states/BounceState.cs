using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class BounceState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState stompState;
	[Export]
	private PlayerState jumpDashState;
	[Export]
	private PlayerState homingAttackState;

	[Export]
	public LockoutResource LockoutSettings { get; private set; }
	[Export]
	private float bounceSpeed;
	[Export]
	private float bounceHeight;

	public bool IsUpwardBounce { get; set; }
	/// <summary> Used to determine whether targeting is enabled or not. </summary>
	private float bounceInterruptTimer;

	public override void EnterState()
	{
		Player.Lockon.IsMonitoring = false;
		bounceInterruptTimer = LockoutSettings.length - .5f;

		if (IsUpwardBounce && Player.Lockon.Target != null) // Snap the player to the target
		{
			Player.MoveSpeed = 0; // Reset speed

			bool applySnapping = false;
			if (!Player.IsBouncing)
			{
				if (Player.Lockon.Target is Area3D)
					applySnapping = Player.Lockon.GetOverlappingAreas().Contains(Player.Lockon.Target as Area3D);
				else if (Player.Lockon.Target is PhysicsBody3D)
					applySnapping = Player.Lockon.GetOverlappingBodies().Contains(Player.Lockon.Target as PhysicsBody3D);
			}

			// Only snap when target being hit is correct
			if (applySnapping)
				Player.GlobalPosition = Player.Lockon.Target.GlobalPosition;
		}
		else // Only bounce the player backwards if bounceUpward is false
		{
			Player.MoveSpeed = -bounceSpeed;
		}

		if (Player.IsLockoutActive && Player.ActiveLockoutData == LockoutSettings) return;

		Player.CanJumpDash = true;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight);
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.AddLockoutData(LockoutSettings);

		Player.Animator.ResetState(0.1f);
		Player.Animator.BounceTrick();
		Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
	}

	public override void ExitState()
	{
		Player.IsBouncing = false;
		Player.RemoveLockoutData(LockoutSettings);
	}

	public override PlayerState ProcessPhysics()
	{
		Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, 0f, Player.Stats.GroundSettings.Friction * PhysicsManager.physicsDelta);
		Player.VerticalSpeed -= Runtime.Gravity * PhysicsManager.physicsDelta;
		Player.ApplyMovement();

		if (!Player.IsLockoutActive || Player.ActiveLockoutData != LockoutSettings) // Lockout has ended
			return fallState;

		if (!Player.Lockon.IsMonitoring)
		{
			UpdateBounceTimer();
			return null;
		}

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			if (Player.Lockon.Target != null && Player.Lockon.IsTargetAttackable)
				return homingAttackState;

			return jumpDashState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			return stompState;
		}

		return null;
	}

	private void UpdateBounceTimer()
	{
		bounceInterruptTimer = Mathf.MoveToward(bounceInterruptTimer, 0, PhysicsManager.physicsDelta);
		Player.Lockon.IsMonitoring = Mathf.IsZeroApprox(bounceInterruptTimer);
	}
}
