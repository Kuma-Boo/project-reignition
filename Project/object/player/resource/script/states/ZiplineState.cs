using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class ZiplineState : PlayerState
{
	public Zipline Trigger { get; set; }

	private float damageLockout;
	private float animationVelocity;
	private readonly float AnimationSmoothing = 2f;
	private readonly float DamageLockoutLength = 0.5f;

	public override void EnterState()
	{
		damageLockout = 0;
		Player.Animator.StartZipline();
		Player.Animator.SetZiplineBlend(0f);
		Player.StartExternal(Trigger, Trigger.FollowObject, .5f);
		Player.Knockback += OnPlayerDamaged;
	}

	public override void ExitState()
	{
		Player.StopExternal();
		Trigger.StopZipline();
		Trigger = null;
		Player.Knockback -= OnPlayerDamaged;
	}

	public override PlayerState ProcessPhysics()
	{
		damageLockout = Mathf.MoveToward(damageLockout, 0, PhysicsManager.physicsDelta);
		float input = Mathf.IsZeroApprox(damageLockout) ? Player.Controller.InputAxis.X : 0;
		Trigger.SetInput(input);
		Trigger.SetSpeed(Trigger.ZiplineSpeed);
		Trigger.ProcessZipline();

		float animationBlend = Player.Animator.GetZiplineBlend();
		animationBlend = ExtensionMethods.SmoothDamp(animationBlend, input, ref animationVelocity, AnimationSmoothing * PhysicsManager.physicsDelta);
		Player.Animator.SetZiplineBlend(animationBlend);

		Player.UpdateExternalControl(false);
		return null;
	}

	private void OnPlayerDamaged()
	{
		if (Player.IsDefeated || Player.IsInvincible) return;

		if (StageSettings.Instance.CurrentRingCount == 0)
		{
			Player.Knockback -= OnPlayerDamaged;
			Player.StartKnockback(new()
			{
				ignoreMovementState = true
			});
			return;
		}

		damageLockout = DamageLockoutLength;

		Player.TakeDamage();
		Player.StartInvincibility();
		Player.Camera.StartMediumCameraShake();

		Trigger.SetSpeed(0, true); // Kill speed
	}
}
