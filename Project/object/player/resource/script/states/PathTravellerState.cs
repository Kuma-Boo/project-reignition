using Project.Gameplay.Objects;
using Godot;

namespace Project.Gameplay;

public partial class PathTravellerState : PlayerState
{
	public PathTraveller Traveller { get; set; }
	private bool isRespawning;
	private bool isSpeedBreaking;

	public override void EnterState()
	{
		isSpeedBreaking = Player.Skills.IsSpeedBreakActive;

		Player.MoveSpeed = isSpeedBreaking ? Player.Skills.speedBreakSpeed : 0f;
		Player.Skills.AllowExternalSpeedBreak = true;
		Player.VerticalSpeed = 0;
		Player.Animator.StartBalancing(); // Carpet uses balancing animations
		Player.Animator.UpdateBalanceSpeed(1f, 0f);
		Player.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		Player.StartExternal(this, Traveller.PlayerStandin, .1f);

		Player.Knockback += OnDamage;
		Traveller.Advanced += OnAdvance;
		Traveller.Staggered += OnStagger;
		Traveller.Damaged += OnDamage;
	}

	public override void ExitState()
	{
		isRespawning = false;
		Player.Skills.AllowExternalSpeedBreak = false;

		Player.StopExternal();
		Player.Animator.ResetState();

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

		float targetBalanceDirection = Player.Controller.InputAxis.X - (Player.PathFollower.DeltaAngle * 5.0f);
		float targetBalanceSpeed = 1f;
		if (!isSpeedBreaking)
		{
			targetBalanceDirection = Player.Controller.InputAxis.X - (Player.PathFollower.DeltaAngle * 20.0f);
			targetBalanceSpeed += Player.Stats.GroundSettings.GetSpeedRatio(Traveller.CurrentSpeed);
		}

		Player.Animator.UpdateBalancing(targetBalanceDirection);
		Player.Animator.UpdateBalanceSpeed(targetBalanceSpeed);
		Player.Animator.UpdateBalanceCrouch(isSpeedBreaking || Traveller.IsOverSpeeding() || Traveller.IsCrouching);

		isSpeedBreaking = Player.Skills.IsSpeedBreakActive;
		return null;
	}

	private void OnAdvance() => Player.UpdateExternalControl(true);

	private void OnStagger()
	{
		if (isSpeedBreaking) // Disable staggering during a speedbreak
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

	private void ForceRespawn()
	{
		Player.StartRespawn();
		Player.LaunchFinished -= ForceRespawn;
	}
}
