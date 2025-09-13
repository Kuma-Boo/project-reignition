using Godot;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class LaunchRingState : PlayerState
{
	public LaunchRing Launcher { get; set; }

	[Export]
	private PlayerState fallState;

	private readonly string LaunchAction = "action_launch";
	private readonly string ExitAction = "action_exit";

	public override void EnterState()
	{
		Player.MoveSpeed = Player.VerticalSpeed = 0;

		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Launcher.GetLaunchDirection());
		Player.Lockon.IsMonitoring = false; // Disable homing reticle

		Player.Animator.ExternalAngle = Player.MovementAngle;
		Player.Animator.StartSpin();

		Player.Effect.StartSpinFX();
		Player.Skills.IsSpeedBreakEnabled = false;

		HeadsUpDisplay.Instance.SetPrompt(LaunchAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(ExitAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();

		Launcher.Damage += OnDamaged;
	}

	public override void ExitState()
	{
		Player.Skills.IsSpeedBreakEnabled = true;

		HeadsUpDisplay.Instance.HidePrompts();

		Launcher.Damage -= OnDamaged;
		Launcher = null;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override PlayerState ProcessPhysics()
	{
		if (!Launcher.IsPlayerCentered)
		{
			Player.GlobalPosition = Launcher.RecenterPlayer();
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

		if (Player.Controller.IsGimmickBufferActive)
		{
			Player.Controller.ResetGimmickBuffer();
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
