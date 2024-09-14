using Godot;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class LaunchRingState : PlayerState
{
	public LaunchRing Launcher { get; set; }

	[Export]
	private PlayerState fallState;

	public override void EnterState()
	{
		Player.MoveSpeed = Player.VerticalSpeed = 0;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Launcher.Forward().RemoveVertical().Normalized());

		Player.Lockon.IsMonitoring = false; // Disable homing reticle

		Player.Animator.ExternalAngle = Player.MovementAngle;
		Player.Animator.StartSpin();

		Player.Effect.StartSpinFX();
		Launcher.Damage += OnDamaged;
	}

	public override void ExitState()
	{
		Launcher.Damage -= OnDamaged;
		Launcher = null;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override PlayerState ProcessPhysics()
	{
		if (!Launcher.IsPlayerCentered)
		{
			Player.CenterPosition = Launcher.RecenterPlayer();
			Player.Controller.ResetJumpBuffer(); // Reset jump buffers just in case
			return null;
		}

		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Player.CanJumpDash = true;
			Player.Lockon.IsMonitoring = true;
			DropPlayer();
			return fallState;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			Player.Effect.StartTrailFX();
			Launcher.Activate();
			return null;
		}

		Player.Animator.SetSpinSpeed(1.5f + Launcher.LaunchRatio);
		return null;
	}

	private void DropPlayer()
	{
		Player.Animator.ResetState();
		Player.Effect.StopSpinFX();
	}

	private void OnDamaged()
	{
		DropPlayer();
		Player.StartKnockback(new()
		{
			ignoreMovementState = true,
		});
	}
}
