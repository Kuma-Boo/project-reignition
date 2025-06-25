using Godot;

namespace Project.Gameplay.Objects;

public partial class Hazard : Node3D
{
	/// <summary> Should this hitbox be disabled? </summary>
	[Export] public bool isDisabled;
	/// <summary> Should this hitbox ignore character interactions when speedbreak is active? </summary>
	[Export] public bool ignoreSpeedbreakingCharacters;

	private bool isInteractingWithPlayer;
	protected PlayerController Player => StageSettings.Player;

	[Signal] public delegate void DamagedPlayerEventHandler();
	[Signal] public delegate void KnockbackFailedEventHandler();

	public override void _Ready() => StageSettings.Instance.Respawned += Respawn;

	public override void _PhysicsProcess(double _) => ProcessCollision();

	private void Respawn() => isInteractingWithPlayer = false;

	protected void ProcessCollision()
	{
		if (isDisabled || !isInteractingWithPlayer)
			return;

		if (Player.IsAutomationActive) // Prevent unfair damage
			return;

		if (Player.IsLaunching && !Player.ActiveLaunchSettings.AllowDamage)
			return;

		if (ignoreSpeedbreakingCharacters && Player.Skills.IsSpeedBreakActive)
			return;

		if (!Player.StartKnockback())
		{
			EmitSignal(SignalName.KnockbackFailed);
			return;
		}

		EmitSignal(SignalName.DamagedPlayer);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = false;
	}
}