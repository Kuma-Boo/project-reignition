using Godot;
using Godot.Collections;

namespace Project.Gameplay;

public partial class PlayerStateMachine : Node
{
	[Export]
	private NodePath startingState;
	private PlayerState currentState;
	[Export]
	private Array<NodePath> stateParents;

	public void Initialize(PlayerController player)
	{
		for (int i = 0; i < stateParents.Count; i++)
		{
			foreach (Node child in GetNode(stateParents[i]).GetChildren(true))
			{
				if (child is not PlayerState)
					continue;

				(child as PlayerState).Initialize(player);
			}
		}

		ChangeState(GetNode<PlayerState>(startingState));
	}

	/// <summary> Exit the current state and switch to a new state. </summary>
	public void ChangeState(PlayerState state)
	{
		if (currentState != state)
			currentState?.ExitState();

		currentState = state;
		currentState.EnterState();
		GD.Print($"State changed to {currentState.Name}.");
	}

	public void ProcessPhysics()
	{
		PlayerState newState = currentState.ProcessPhysics();
		if (newState != null)
			ChangeState(newState);
	}
}
