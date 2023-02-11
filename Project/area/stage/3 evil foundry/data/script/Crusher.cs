using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public partial class Crusher : Node3D
	{
		[Export(PropertyHint.Range, "0, 10")]
		private int height;
		[Export]
		private float travelTime; //How long to take to crush/rise
		[Export]
		private float openTime; //How long to remain open.
		[Export]
		private float closedTime = -1; //How long to stay closed. -1 to use the same value as openLength.
		[Export(PropertyHint.Range, "0,1,.1")]
		private float currentRatio; //Set this from the editor to change where the initial timer is
		[Export]
		private States currentState;
		private enum States
		{
			Open,
			Falling,
			Closed,
			Rising,
		}

		[Export]
		private Node3D light;
		[Export]
		private NodePath crusher;
		private Node3D _crusher;
		private readonly int POSITION_OFFSET = 8;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			//Start crusher at the correct position
			_crusher = GetNode<Node3D>(crusher);
			UpdateCrusherPosition();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) //Position crusher for easier editing
			{
				_crusher = GetNodeOrNull<Node3D>(crusher);
				if (_crusher != null)
					UpdateCrusherPosition();

				return;
			}

			light.Visible = currentState == States.Falling;

			switch (currentState)
			{
				case States.Open:
					if (IsStateCompleted(openTime)) //Start Falling
						currentState = States.Falling;
					break;
				case States.Closed:
					if (IsStateCompleted(closedTime)) //Start rising
						currentState = States.Rising;
					break;
				default:
					if (IsStateCompleted(travelTime))
					{
						if (currentState == States.Falling && !Mathf.IsZeroApprox(closedTime))
							currentState = States.Closed;
						else
							currentState = States.Open;
					}
					break;
			}

			UpdateCrusherPosition();
		}

		/// <summary>
		/// Updates the state timer, returns True if state is over.
		/// </summary>
		private bool IsStateCompleted(float t)
		{
			currentRatio += (1f / t) * PhysicsManager.physicsDelta;
			if (currentRatio >= 1f) //Start falling
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

			_crusher.Position = Vector3.Up * (height * Mathf.SmoothStep(0, 1, ratio) + POSITION_OFFSET);
		}
	}
}
