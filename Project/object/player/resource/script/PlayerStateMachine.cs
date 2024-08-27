using Godot;

namespace Project.Gameplay;

public partial class PlayerStateMachine : Node
{
	[Export]
	private NodePath startingState;
	private PlayerState currentState;

	public void Initialize(PlayerController player)
	{
		foreach (Node child in GetChildren())
		{
			if (child is not PlayerState)
				continue;

			(child as PlayerState).Initialize(player);
		}

		ChangeState(GetNode<PlayerState>(startingState));
	}

	/// <summary> Exit the current state and switch to a new state. </summary>
	public void ChangeState(PlayerState state)
	{
		currentState?.ExitState();

		currentState = state;
		currentState.EnterState();
	}

	public void ProcessPhysics()
	{
		GD.Print(currentState.Name);
		PlayerState newState = currentState.ProcessPhysics();
		if (newState != null)
			ChangeState(newState);
	}
}
