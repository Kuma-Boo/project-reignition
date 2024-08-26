using Godot;

namespace Project.Gameplay;

public partial class PlayerState : Node
{
	protected PlayerController Player { get; private set; }

	public void Initialize(PlayerController player) => Player = player;

	/// <summary> Called when this state is entered. </summary>
	public virtual void EnterState() { }

	/// <summary> Called when this state is exited. </summary>
	public virtual void ExitState() { }

	/// <summary> Called on each frame update. </summary>
	public virtual PlayerState ProcessFrame() => null;

	/// <summary> Called on each physics update. </summary>
	public virtual PlayerState ProcessPhysics() => null;
}
