using Godot;

namespace Project.Gameplay;

public partial class PlayerStateMachine : Node
{
	[Export]
	private NodePath startingState;
	private PlayerState currentState;

	public void Initialize(PlayerController controller)
	{
		foreach (Node child in GetChildren())
		{
			if (child is not PlayerState)
				continue;

			(child as PlayerState).Initialize(controller);
		}

		ChangeState(GetNode<PlayerState>(startingState));
	}

	/// <summary> Exit the current state and switch to a new state. </summary>
	private void ChangeState(PlayerState state)
	{
		currentState?.ExitState();

		currentState = state;
		currentState.EnterState();
	}

	public void ProcessPhysics()
	{
		PlayerState newState = currentState.ProcessPhysics();
		if (newState != null)
			ChangeState(newState);
	}
}
