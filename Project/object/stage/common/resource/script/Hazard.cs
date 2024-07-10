using Godot;

namespace Project.Gameplay.Hazards;

public partial class Hazard : Node3D
{
	[Export]
	public bool isDisabled; //Is this hitbox active?

	private bool isInteractingWithPlayer;

	protected CharacterController Character => CharacterController.instance;

	[Signal]
	public delegate void DamagedPlayerEventHandler();

	public override void _PhysicsProcess(double _) => ProcessCollision();

	protected void ProcessCollision()
	{
		if (!isDisabled && isInteractingWithPlayer)
		{
			Character.StartKnockback();
			EmitSignal(SignalName.DamagedPlayer);
		}
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