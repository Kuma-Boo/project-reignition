using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class LaunchState : PlayerState
{
	[Export] private PlayerState landState;
	[Export] private PlayerState fallState;

	private float launcherTime;
	private LaunchSettings settings;
	public Launcher ActiveLauncher => settings.Launcher;
	private RaycastHit wallHit;
	public bool UpdateSettings(LaunchSettings settings)
	{
		if (settings.startPosition.IsEqualApprox(settings.endPosition) &&
			Mathf.IsZeroApprox(settings.middleHeight)) // Launcher initialization error
		{
			return false;
		}

		if (Player.IsLaunching &&
			this.settings.Launcher != null &&
			this.settings.Launcher == settings.Launcher) // Already launching
		{
			return false;
		}

		this.settings = settings;
		return true;
	}

	public override void EnterState()
	{
		launcherTime = 0;
		wallHit = new();

		Player.IsOnGround = false;
		Player.IsLaunching = true;
		Player.IsMovingBackward = false;
		Player.AllowLandingGrind = true;
		Player.AllowLandingSkills = false;
		Player.MoveSpeed = settings.HorizontalVelocity;
		Player.VerticalSpeed = settings.InitialVerticalVelocity;

		Player.Lockon.IsMonitoring = false; // Disable lockon monitoring while launch is active
		Player.AttackState = ActiveLauncher.oneshotEnemies ? PlayerController.AttackStates.OneShot : PlayerController.AttackStates.None; // Launchers always oneshot all enemies

		Player.UpDirection = Vector3.Up;
		Player.Rotation = Vector3.Zero; // Reset rotation

		if (settings.UseAutoAlign)
		{
			Player.MovementAngle = GetLaunchFacingAngle();
			Player.Animator.SnapRotation(Player.MovementAngle);
		}

		if (settings.IsJump) // Play jump effects
		{
			Player.Animator.AutoJumpAnimation();
			Player.Effect.PlayActionSFX(Player.Effect.JumpSfx);
		}

		if (settings.IgnoreCollisions)
			Player.ChangeHitbox("disable-environment");
	}

	public override void ExitState()
	{
		Player.IsLaunching = false;
		if (!wallHit)
		{
			Player.MoveSpeed = settings.HorizontalVelocity * .5f; // Prevent too much movement
			Player.VerticalSpeed = Player.IsOnGround ? 0 : settings.FinalVerticalVelocity;
		}

		Player.AttackState = PlayerController.AttackStates.None;
		Player.Lockon.IsMonitoring = !Player.IsOnGround && settings.AllowJumpDash;

		Player.Effect.StopSpinFX();
		Player.Effect.StopTrailFX();
		Player.Animator.ResetState();

		Player.ChangeHitbox("RESET");
		Player.EmitSignal(PlayerController.SignalName.LaunchFinished);
		settings.Launcher?.Deactivate();
	}

	public override PlayerState ProcessPhysics()
	{
		if (IsRecenteringPlayer())
			return null;

		launcherTime = Mathf.Min(launcherTime + PhysicsManager.physicsDelta, settings.TotalTravelTime);
		Vector3 targetPosition = settings.InterpolatePositionTime(launcherTime);
		float heightDelta = Mathf.IsZeroApprox(launcherTime) ? 0 : targetPosition.Y - Player.GlobalPosition.Y;

		if (!settings.IgnoreCollisions)
		{
			UpdateWallHit(targetPosition);
			if (wallHit)
				return fallState;
		}

		Player.GlobalPosition = targetPosition;
		Player.VerticalSpeed = heightDelta / PhysicsManager.physicsDelta;
		Player.PathFollower.Resync();

		if (heightDelta < 0 && Player.CheckGround()) // Only check ground when falling
		{
			Player.MoveSpeed = 0;
			return landState;
		}

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

		Player.GlobalPosition = settings.Launcher.RecenterPlayer();
		return true;
	}

	private void UpdateWallHit(Vector3 targetPosition)
	{
		wallHit = new();
		Vector3 direction = targetPosition - Player.GlobalPosition;
		RaycastHit hit = Player.CastRay(Player.GlobalPosition, direction, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.GlobalPosition, direction, hit ? Colors.Red : Colors.White);

		if (!hit || !hit.collidedObject.IsInGroup("wall"))
			return;

		wallHit = hit;
		Player.MoveSpeed = 0;
		Player.GlobalPosition = wallHit.point - (wallHit.direction * Player.CollisionSize.X);
	}
}