using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class TeleportState : PlayerState
{
	private TeleportTrigger Trigger { get; set; }
	public void UpdateTrigger(TeleportTrigger trigger) => Trigger = trigger;

	[Export]
	private PlayerState idleState;

	private States currentState;
	private enum States
	{
		Start,
		Crossfade,
		Stop,
		Completed
	}

	private float teleportTimer;
	private readonly float TeleportStartFXLength = .2f;
	private readonly float TeleportEndFXLength = .5f;

	public override void EnterState()
	{
		Player.IsTeleporting = true;
		if (Player.IsKnockback)
		{
			Player.IsKnockback = false;
			Player.Animator.StopHurt(false);
		}

		Player.ChangeHitbox("disable");

		if (Trigger.resetMovespeed)
		{
			Player.MoveSpeed = Player.VerticalSpeed = 0;
			Player.Animator.DisabledSpeedSmoothing = true;
		}

		if (StartTeleportFX())
			return;

		if (StartCrossfade())
			return;

		ApplyTeleport();

		if (StopTeleportFX())
			return;

		currentState = States.Completed;
	}

	public override void ExitState()
	{
		Player.ChangeHitbox("RESET");
		Player.IsTeleporting = false;
	}

	public override PlayerState ProcessPhysics()
	{
		switch (currentState)
		{
			case States.Start:
				ProcessStartFX();
				return null;
			case States.Crossfade:
				ApplyTeleport();
				return null;
			case States.Stop:
				ProcessStopFX();
				return null;
			default:
				return idleState;
		}
	}

	private bool StartTeleportFX()
	{
		if (!Trigger.enableStartFX)
			return false;

		// Begin StartFX state
		teleportTimer = 0;
		currentState = States.Start;
		Player.Effect.StartTeleport();
		Player.Animator.StartTeleport();
		return true;
	}

	private void ProcessStartFX()
	{
		teleportTimer = Mathf.MoveToward(teleportTimer, TeleportStartFXLength, PhysicsManager.physicsDelta);

		if (!Mathf.IsEqualApprox(teleportTimer, TeleportStartFXLength))
			return;

		if (StartCrossfade())
			return;
	}

	private bool StartCrossfade()
	{
		if (!Trigger.crossfade)
			return false;

		Player.Camera.StartCrossfade();
		currentState = States.Crossfade;
		return true;
	}

	private void ApplyTeleport()
	{
		Player.GlobalPosition = Trigger.WarpPosition;
		Player.ResetPhysicsInterpolation();

		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.SnapToGround();
		Player.CheckGround();
		Player.PathFollower.Resync();
		Player.Animator.ResetState(0);
		Player.Animator.IdleAnimation();
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);

		Trigger.ApplyTeleport(); // Apply any signals/path changes

		if (StopTeleportFX())
			return;

		currentState = States.Completed;
	}

	private bool StopTeleportFX()
	{
		if (!Trigger.enableEndFX)
			return false;

		teleportTimer = 0;
		currentState = States.Stop;
		Player.Effect.StopTeleport();
		Player.Animator.StopTeleport();
		return true;
	}

	private void ProcessStopFX()
	{
		teleportTimer = Mathf.MoveToward(teleportTimer, TeleportEndFXLength, PhysicsManager.physicsDelta);

		if (!Mathf.IsEqualApprox(teleportTimer, TeleportEndFXLength))
			return;

		currentState = States.Completed;
	}
}
