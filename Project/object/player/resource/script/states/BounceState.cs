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
	private bool isSnapSuccessful;
	private Vector3 snapPosition;
	private Node3D snapTarget;
	public void SetSnapTarget(Node3D target) => snapTarget = target;
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
		if (isSnapSuccessful)
		{
			isSnapSuccessful = false;
			Player.CenterPosition = snapPosition;
			return null;
		}

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
			if (SaveManager.Config.useStompJumpButtonMode)
				return stompState;

			return Player.Lockon.IsTargetAttackable ? homingAttackState : jumpDashState;
		}

		if (Player.Controller.IsAttackBufferActive)
		{
			Player.Controller.ResetAttackBuffer();
			return Player.Lockon.IsTargetAttackable ? homingAttackState : jumpDashState;
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
		isSnapSuccessful = false;

		if (snapMode == SnapMode.Disabled) // Not a snap bounce -- bounce the player backwards
		{
			Player.MoveSpeed = -bounceSpeed;
			return;
		}

		Player.MoveSpeed = 0; // Reset speed

		if (snapTarget == null) // Nothing to snap to
			return;

		if (!Mathf.IsZeroApprox(bounceInterruptTimer)) // Player is already bouncing -- don't snap
			return;

		if (!Player.Lockon.IsCollidingWithTarget()) // Failed to find a target to snap to
			return;

		// Only snap when target being hit is correct
		Vector3 targetSnapPosition = snapTarget.GlobalPosition;
		if (snapMode == SnapMode.SnappingEnabledNoHeight)
			targetSnapPosition.Y = Player.CenterPosition.Y;

		isSnapSuccessful = true;
		snapPosition = targetSnapPosition;
	}
}
