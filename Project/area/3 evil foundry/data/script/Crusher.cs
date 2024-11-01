using Godot;
using Project.Core;

namespace Project.Gameplay;

[Tool]
public partial class Crusher : Node3D
{
	[Export(PropertyHint.Range, "0, 20")] private int height;
	/// <summary> How long does falling take? </summary>
	[Export] private float fallingTime;
	/// <summary> How long to remain open. </summary>
	[Export] private float openTime;
	/// <summary> How long does rising take? </summary>
	[Export] private float risingTime;
	/// <summary> How long to stay closed. </summary>
	[Export] private float closedTime;
	/// <summary> Set this from the editor to change where the initial timer is. </summary>
	[Export(PropertyHint.Range, "0,1,.1")] private float currentRatio;
	[Export] private States currentState;
	private enum States
	{
		Open,
		Falling,
		Closed,
		Rising,
	}

	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath light;
	private Node3D _light;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath crusher;
	private Node3D _crusher;
	private readonly int PositionOffset = 8;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		_light = GetNode<Node3D>(light);
		_crusher = GetNode<Node3D>(crusher);
		// Start crusher at the correct position
		UpdateCrusherPosition();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) // Position crusher for easier editing
		{
			_crusher = GetNodeOrNull<Node3D>(crusher);
			if (_crusher != null)
				UpdateCrusherPosition();

			return;
		}

		_light.Visible = currentState == States.Falling;

		switch (currentState)
		{
			case States.Open:
				if (IsStateCompleted(openTime)) // Start Falling
					currentState = States.Falling;
				break;
			case States.Falling:
				if (IsStateCompleted(fallingTime))
					currentState = States.Closed;
				break;
			case States.Closed:
				if (IsStateCompleted(closedTime)) // Start rising
					currentState = States.Rising;
				break;
			case States.Rising:
				if (IsStateCompleted(risingTime))
					currentState = States.Open;
				break;
		}

		UpdateCrusherPosition();
	}

	/// <summary>
	/// Updates the state timer, returns true if state is over.
	/// </summary>
	private bool IsStateCompleted(float t)
	{
		currentRatio += 1f / t * PhysicsManager.physicsDelta;
		if (currentRatio >= 1f) // Start falling
		{
			currentRatio = 0;
			return true;
		}

		return false;
	}

	private void UpdateCrusherPosition()
	{
		float ratio = 0f;
		switch (currentState)
		{
			case States.Open:
				ratio = 1;
				break;
			case States.Closed:
				ratio = 0;
				break;
			case States.Falling:
				ratio = 1f - currentRatio;
				break;
			case States.Rising:
				ratio = currentRatio;
				break;
		}

		_crusher.Position = Vector3.Up * ((height * Mathf.SmoothStep(0, 1, ratio)) + PositionOffset);
	}
}