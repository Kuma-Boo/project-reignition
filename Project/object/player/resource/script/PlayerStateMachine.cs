using Godot;

namespace Project.Gameplay;

public partial class PlayerStateMachine : Node
{
	[Export]
	private NodePath startingState;
	private PlayerState _currentState;

	public void Initialize(PlayerController controller, PlayerInputController input)
	{
		foreach (Node child in GetChildren())
		{
			if (child is not PlayerState)
				continue;

			(child as PlayerState).Initialize(controller, input);
		}

		ChangeState(GetNode<PlayerState>(startingState));
	}

	/// <summary> Exit the current state and switch to a new state. </summary>
	private void ChangeState(PlayerState state)
	{
		_currentState?.ExitState();

		_currentState = state;
		_currentState.EnterState();
	}

	public void ProcessPhysics()
	{
		PlayerState newState = _currentState.ProcessPhysics();

		if (newState != null)
			ChangeState(newState);
	}
}
