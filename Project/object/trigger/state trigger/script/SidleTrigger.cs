using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Handles sidle behaviour.
/// </summary>
public partial class SidleTrigger : Area3D
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();

	/// <summary> Which way to sidle? </summary>
	[Export]
	private bool isFacingRight = true;
	public bool IsFacingRight => isFacingRight;
	[Export]
	private LockoutResource lockout;

	private bool isInteractingWithPlayer;
	private PlayerController Player => StageSettings.Player;

	public override void _PhysicsProcess(double _)
	{
		if (!isInteractingWithPlayer)
			return;

		AttemptSidle();
	}

	/// <summary> Checks whether the player is in a state where a sidle is possible. </summary>
	private void AttemptSidle()
	{
		if (Player.IsSidling)
			return;

		if (Player.DisableSidle)
			return;

		if (!Player.IsOnGround)
			return;

		if (Player.ExternalController != null)
			return; // Player is already busy

		Player.StartSidle(this);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = true;

		Player.Skills.IsSpeedBreakEnabled = false; // Disable speed break
		Player.AddLockoutData(lockout); // Apply lockout
		EmitSignal(SignalName.Activated); // Immediately emit signals to allow path changes, etc.
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = false;
		Player.RemoveLockoutData(lockout);
		// REFACTOR TODO Player.StopSidle();

		if (Player.ExternalController == null)
			Player.Skills.IsSpeedBreakEnabled = true; // Re-enable speed break

		EmitSignal(SignalName.Deactivated); // Deactivate signals
	}
}
