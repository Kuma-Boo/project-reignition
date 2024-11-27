using Godot;

namespace Project.Gameplay.Objects;

public partial class Hazard : Node3D
{
	/// <summary> Should this hitbox be disabled? </summary>
	[Export] public bool isDisabled;

	private bool isInteractingWithPlayer;
	protected PlayerController Player => StageSettings.Player;

	[Signal]
	public delegate void DamagedPlayerEventHandler();

	public override void _PhysicsProcess(double _) => ProcessCollision();

	protected void ProcessCollision()
	{
		if (isDisabled || !isInteractingWithPlayer)
			return;

		Player.StartKnockback();
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