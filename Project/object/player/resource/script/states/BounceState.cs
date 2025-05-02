using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class BounceState : PlayerState
{
	public enum SnapMode
	{
		SnappingEnabled, // Snap player to the target's exact position
		SnappingEnabledNoHeight, // Snap player, but ignore height (useful for cages)
		Disabled,
	}

	private SnapMode snapMode;
	public void SetBounceSnapping(SnapMode mode) => snapMode = mode;

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

	/// <summary> Used to override how high the bounce takes the player. </summary>
	public float BounceHeightScale { get; set; }
	/// <summary> Used to determine whether targeting is enabled or not. </summary>
	private float bounceInterruptTimer;

	public override void EnterState()
	{
		AttemptBounceSnapping();
		bounceInterruptTimer = LockoutSettings.length - .5f;

		Player.IsOnGround = false;
		Player.CanJumpDash = true;
		Player.Lockon.IsMonitoring = true;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(bounceHeight * BounceHeightScale);
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

		return null;
	}

	private void AttemptBounceSnapping()
	{
		if (snapMode == SnapMode.Disabled) // Not a snap bounce -- bounce the player backwards
		{
			GD.Print("Wrong mode");
			Player.MoveSpeed = -bounceSpeed;
			return;
		}

		Player.MoveSpeed = 0; // Reset speed

		if (Player.Lockon.Target == null) // Nothing to snap to
		{
			GD.Print("No target");
			return;
		}

		if (!Mathf.IsZeroApprox(bounceInterruptTimer)) // Player is already bouncing -- don't snap
		{
			GD.Print("Already bouncing");
			return;
		}

		if ((Player.Lockon.Target is Area3D && !Player.Lockon.GetOverlappingAreas().Contains(Player.Lockon.Target as Area3D)) ||
			(Player.Lockon.Target is PhysicsBody3D && !Player.Lockon.GetOverlappingBodies().Contains(Player.Lockon.Target as PhysicsBody3D)))
		{
			// Failed to find a target to snap to
			GD.Print("Couldn't find target");
			return;
		}

		GD.Print("Snapped");

		// Only snap when target being hit is correct
		Vector3 targetSnapPosition = Player.Lockon.Target.GlobalPosition;
		if (snapMode == SnapMode.SnappingEnabledNoHeight)
			targetSnapPosition.Y = Player.GlobalPosition.Y;

		Player.GlobalPosition = targetSnapPosition;
	}
}
