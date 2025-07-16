using Project.Gameplay.Objects;
using Godot;

namespace Project.Gameplay;

public partial class PathTravellerState : PlayerState
{
	public PathTraveller Traveller { get; set; }
	private bool isRespawning;

	public override void EnterState()
	{
		Player.MoveSpeed = Player.VerticalSpeed = 0;
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Animator.StartBalancing(); // Carpet uses balancing animations
		Player.Animator.UpdateBalanceSpeed(1f, 0f);
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Player.StartExternal(this, Traveller.PlayerStandin, .1f);

		Traveller.Advanced += OnAdvance;
		Traveller.Staggered += OnStagger;
		Traveller.Damaged += OnDamage;
	}

	public override void ExitState()
	{
		isRespawning = false;

		Player.StopExternal();
		Player.Animator.ResetState();
		Player.Skills.IsSpeedBreakEnabled = true;

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
		Player.Animator.UpdateBalancing(Player.Controller.InputAxis.X - (Player.PathFollower.DeltaAngle * 20.0f));
		Player.Animator.UpdateBalanceSpeed(1f + Player.Stats.GroundSettings.GetSpeedRatio(Traveller.CurrentSpeed));
		return null;
	}

	private void OnAdvance() => Player.UpdateExternalControl(true);

	private void OnStagger()
	{
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
		LaunchSettings launchSettings = LaunchSettings.Create(Player.GlobalPosition, Traveller.GetDamageEndPosition(), 2);
		Player.StartLauncher(launchSettings);
		Player.Animator.ResetState(0.1f);
		Player.Animator.StartSpin(3.0f);
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);
	}
}
