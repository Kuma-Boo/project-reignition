using Project.Gameplay.Objects;
using Godot;

namespace Project.Gameplay;

public partial class PathTravellerState : PlayerState
{
	public PathTraveller Traveller { get; set; }
	public bool IsCrouching { get; private set; }
	private bool isRespawning;

	public override void EnterState()
	{
		IsCrouching = Player.Skills.IsSpeedBreakActive;

		Player.IsOnGround = true;
		Player.MoveSpeed = IsCrouching ? Player.Skills.speedBreakSpeed : 0f;
		Player.VerticalSpeed = 0;
		Player.Animator.StartBalancing(); // Carpet uses balancing animations
		Player.Animator.UpdateBalanceSpeed(1f, 0f);
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Player.StartExternal(this, Traveller.PlayerStandin, .1f);

		Player.Skills.SpeedBreakStarted += ProcessSpeedbreakCrouch;
		Player.Skills.SpeedBreakStopped += ProcessSpeedbreakCrouch;

		Player.Knockback += OnDamage;
		Traveller.Advanced += OnAdvance;
		Traveller.Staggered += OnStagger;
		Traveller.Damaged += OnDamage;
	}

	public override void ExitState()
	{
		isRespawning = false;
		Player.IsOnGround = false;

		Player.StopExternal();
		Player.Animator.ResetState();

		Player.Skills.SpeedBreakStarted -= ProcessSpeedbreakCrouch;
		Player.Skills.SpeedBreakStopped -= ProcessSpeedbreakCrouch;

		Player.Knockback -= OnDamage;
		Traveller.StopMovement();
		Traveller.Advanced -= OnAdvance;
		Traveller.Staggered -= OnStagger;
		Traveller.Damaged -= OnDamage;
		Traveller = null;
	}

	public override PlayerState ProcessPhysics()
	{
		if (isRespawning)
			return null;

		Traveller.ProcessPathTraveller();
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

		float targetBalanceDirection = Player.Controller.InputAxis.X - (Player.PathFollower.DeltaAngle * 20.0f);
		float targetBalanceSpeed = 1f;
		if (!Player.Skills.IsSpeedBreakActive)
			targetBalanceSpeed += Player.Stats.GroundSettings.GetSpeedRatio(Traveller.CurrentSpeed);

		Player.Animator.UpdateBalancing(targetBalanceDirection);
		Player.Animator.UpdateBalanceSpeed(targetBalanceSpeed);
		Player.Animator.UpdateBalanceCrouch(IsCrouching || Traveller.IsCrouching);
		return null;
	}

	private void OnAdvance() => Player.UpdateExternalControl(true);

	private void OnStagger()
	{
		if (Player.Skills.IsSpeedBreakActive) // Disable staggering during a speedbreak
			return;

		Player.StartInvincibility();
		Player.Animator.StartBalanceStagger();
		Player.TakeDamage(true);
	}

	private void OnDamage()
	{
		if (isRespawning || Traveller == null)
			return;

		isRespawning = true;
		Traveller.Deactivate();
		Traveller.Despawn();

		// Bump the player off
		Vector3 targetEndPosition = Traveller.GetDamageEndPosition();

		if (Traveller.AutoDefeat)
		{
			targetEndPosition += Vector3.Down * Traveller.Bounds.Y;
			Player.Camera.IsDefeatFreezeActive = true;
			Player.LaunchFinished += ForceRespawn;
		}

		LaunchSettings launchSettings = LaunchSettings.Create(Player.GlobalPosition, targetEndPosition, 2);
		launchSettings.AllowDamage = !Traveller.AutoDefeat;

		if (Player.Skills.IsSpeedBreakActive)
			Player.Skills.ToggleSpeedBreak();

		Player.StartLauncher(launchSettings);
		Player.Animator.ResetState(0.1f);
		Player.Animator.StartSpin(3.0f);
		Player.Animator.CallDeferred(PlayerAnimator.MethodName.SnapRotation, Player.Animator.ExternalAngle);
	}

	private void ProcessSpeedbreakCrouch() => IsCrouching = Player.Skills.IsSpeedBreakActive;

	private void ForceRespawn()
	{
		Player.StartRespawn();
		Player.LaunchFinished -= ForceRespawn;
	}
}
