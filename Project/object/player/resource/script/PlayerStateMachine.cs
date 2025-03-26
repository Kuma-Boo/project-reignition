using Godot;
using Godot.Collections;

namespace Project.Gameplay;

public partial class PlayerStateMachine : Node
{
	[Export]
	private NodePath startingState;
	public PlayerState CurrentState { get; private set; }
	public PlayerState QueuedState { get; private set; }
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
		QueuedState = state;
		if (CurrentState != state)
			CurrentState?.ExitState();

		QueuedState = null;
		CurrentState = state;
		CurrentState.EnterState();
		GD.Print($"State changed to {CurrentState.Name}.");
	}

	public void ProcessPhysics()
	{
		if (StageSettings.Instance.IsLevelLoading)
			return;

		PlayerState newState = CurrentState.ProcessPhysics();
		if (newState != null)
			ChangeState(newState);
	}
}
