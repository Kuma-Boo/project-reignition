using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class LaunchState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState fallState;

	private float launcherTime;
	private LaunchSettings settings;
	public bool UpdateSettings(LaunchSettings settings)
	{
		if (settings.startPosition.IsEqualApprox(settings.endPosition)) // Launcher initialization error
			return false;

		if (this.settings.Launcher != null && this.settings.Launcher == settings.Launcher) // Already launching
			return false;

		this.settings = settings;
		return true;
	}

	public override void EnterState()
	{
		launcherTime = 0;

		Player.IsOnGround = false;
		Player.IsMovingBackward = false;
		Player.MoveSpeed = Player.VerticalSpeed = 0;

		Player.Lockon.IsMonitoring = false; // Disable lockon monitoring while launch is active
		Player.State.AttackState = PlayerStateController.AttackStates.OneShot; // Launchers always oneshot all enemies

		if (settings.UseAutoAlign)
		{
			Player.MovementAngle = GetLaunchFacingAngle();
			Player.Animator.SnapRotation(Player.MovementAngle);
		}

		if (settings.IsJump) // Play jump effects
		{
			Player.Animator.JumpAnimation();
			Player.UpDirection = Vector3.Up;
			Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
		}
	}

	public override void ExitState()
	{
		settings.Launcher?.Deactivate();

		Player.MoveSpeed = settings.HorizontalVelocity * .5f; // Prevent too much movement
		Player.VerticalSpeed = Player.IsOnGround ? 0 : settings.FinalVerticalVelocity;

		Player.State.AttackState = PlayerStateController.AttackStates.None;
		Player.Lockon.IsMonitoring = !Player.IsOnGround && settings.AllowJumpDash;

		Player.Effect.StopSpinFX();
		Player.Effect.StopTrailFX();
		Player.Animator.ResetState();

		settings = new();
		Player.EmitSignal(PlayerController.SignalName.LaunchFinished);
	}

	public override PlayerState ProcessPhysics()
	{
		if (IsRecenteringPlayer())
			return null;

		Vector3 targetPosition = settings.InterpolatePositionTime(launcherTime);
		float heightDelta = Mathf.IsZeroApprox(launcherTime) ? 0 : targetPosition.Y - Player.GlobalPosition.Y;

		if (CheckWall(targetPosition))
			return fallState;

		Player.GlobalPosition = targetPosition;
		Player.VerticalSpeed = heightDelta;
		Player.PathFollower.Resync();

		if (heightDelta < 0 && Player.CheckGround()) // Only check ground when falling
			return landState;

		launcherTime += PhysicsManager.physicsDelta;
		if (settings.IsLauncherFinished(launcherTime)) // Revert to normal state
			return fallState;

		return null;
	}

	private float GetLaunchFacingAngle()
	{
		Vector3 launchDirection;
		if (settings.Launcher == null)
			launchDirection = settings.InitialVelocity.RemoveVertical();
		else
			launchDirection = settings.Launcher.GetLaunchDirection().RemoveVertical();

		if (launchDirection.IsEqualApprox(Vector3.Zero)) // Fallback to PathFollower's forward angle
			return Player.PathFollower.ForwardAngle;

		return ExtensionMethods.CalculateForwardAngle(launchDirection);
	}

	private bool IsRecenteringPlayer()
	{
		if (settings.Launcher?.IsPlayerCentered != false)
			return false;

		Player.GlobalPosition = settings.Launcher.RecenterCharacter();
		return true;
	}

	private bool CheckWall(Vector3 targetPosition)
	{
		RaycastHit hit = Player.CastRay(Player.GlobalPosition, targetPosition - Player.GlobalPosition, Runtime.Instance.environmentMask);
		return hit && hit.collidedObject.IsInGroup("wall");
	}
}