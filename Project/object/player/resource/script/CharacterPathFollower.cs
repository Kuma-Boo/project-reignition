using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Helps keep track of where the player is relative to the level's path
	/// </summary>
	public partial class CharacterPathFollower : PathFollow3D
	{
		public CharacterController Character => CharacterController.instance;

		/// <summary> Current rotation of the pathfollower, in global radians. </summary>
		public float ForwardAngle { get; private set; }
		/// <summary> Current backwards rotation of the pathfollower, always equal to ForwardAngle + Mathf.Pi. </summary>
		public float BackAngle { get; private set; }
		public Path3D ActivePath { get; private set; }

		public void Initialize()
		{
			if (StageSettings.instance.StartingPath != null) //Auto assign path
				SetActivePath(StageSettings.instance.StartingPath);
		}

		public void SetActivePath(Path3D newPath)
		{
			if (newPath == null || newPath == ActivePath) return;

			if (IsInsideTree()) //Unparent
				GetParent().RemoveChild(this);
			newPath.AddChild(this);

			ActivePath = newPath;
			CallDeferred(MethodName.Resync);
		}

		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null) return;

			//GetClosestOffset() is broken in the Godot 4.0 update. Keep checking back later to see if it's been fixed.
			Progress = ActivePath.Curve.GetClosestOffset(Character.GlobalPosition - ActivePath.GlobalPosition);
			ForwardAngle = CharacterController.CalculateForwardAngle(this.Forward());
			BackAngle = ForwardAngle + Mathf.Pi;
		}

		/// <summary> Difference between angle from the current frame to the next frame. </summary>
		public float CalculateDeltaAngle(float speed)
		{
			float currentProgress = Progress;
			Progress += speed * PhysicsManager.physicsDelta;
			float deltaAngle = CharacterController.CalculateForwardAngle(this.Forward()) - ForwardAngle;
			Progress = currentProgress; //Reset

			return deltaAngle;
		}

		//Is the pathfollower ahead of the reference point?
		public bool IsAheadOfPoint(Vector3 globalPosition) => Mathf.Sign(Progress - ActivePath.Curve.GetClosestOffset(globalPosition - ActivePath.GlobalPosition)) > 0;
	}
}
