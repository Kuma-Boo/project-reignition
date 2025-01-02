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
		AttemptBounceSnapping();
		bounceInterruptTimer = LockoutSettings.length - .5f;

		Player.IsOnGround = false;
		Player.CanJumpDash = true;
		Player.Lockon.IsMonitoring = true;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight);
		Player.MovementAngle = Player.PathFollower.ForwardAngle;

		if (!Player.IsLockoutActive || Player.ActiveLockoutData != LockoutSettings)
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

		if (!Player.IsBounceInteruptable)
		{
			bounceInterruptTimer = Mathf.MoveToward(bounceInterruptTimer, 0, PhysicsManager.physicsDelta);
			Player.IsBounceInteruptable = Mathf.IsZeroApprox(bounceInterruptTimer);
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

	private void AttemptBounceSnapping()
	{
		if (!IsUpwardBounce) // Not a snap bounce -- bounce the player backwards
		{
			Player.MoveSpeed = -bounceSpeed;
			return;
		}

		Player.MoveSpeed = 0; // Reset speed

		if (Player.Lockon.Target == null) // Nothing to snap to
			return;

		if (!Mathf.IsZeroApprox(bounceInterruptTimer)) // Player is already bouncing -- don't snap
			return;

		if ((Player.Lockon.Target is Area3D && !Player.Lockon.GetOverlappingAreas().Contains(Player.Lockon.Target as Area3D)) ||
			(Player.Lockon.Target is PhysicsBody3D && !Player.Lockon.GetOverlappingBodies().Contains(Player.Lockon.Target as PhysicsBody3D)))
		{
			// Failed to find a target to snap to
			return;
		}

		// Only snap when target being hit is correct
		Player.GlobalPosition = Player.Lockon.Target.GlobalPosition;
	}
}
