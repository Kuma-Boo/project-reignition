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

		public void SetActivePath(Path3D newPath)
		{
			if (newPath == null) return;
			if (IsInsideTree()) //Unparent
				GetParent().RemoveChild(this);

			ActivePath = newPath;

			newPath.AddChild(this);
			Resync();
		}

		public Vector3 LocalPlayerPosition => GlobalTransform.basis.Inverse() * (GlobalPosition - Character.GlobalPosition);
		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null) return;

			//GetClosestOffset() is broken in the Godot 4.0 update. Keep checking back later to see if it's been fixed.
			Progress = ActivePath.Curve.GetClosestOffset(Character.GlobalPosition - ActivePath.GlobalPosition);
			ForwardAngle = GetForwardAngle();
			BackAngle = ForwardAngle + Mathf.Pi;

			//if (_character.isSideScroller)
			//_character.RecenterStrafe();
		}

		private float GetForwardAngle()
		{
			Vector3 forwardDirection = this.Back();
			float dot = Mathf.Abs(forwardDirection.Dot(Vector3.Up));
			if (dot > .9f)//UNIMPLEMENTED - Moving vertically
			{

			}

			return forwardDirection.Flatten().AngleTo(Vector2.Up);
		}

		/// <summary> Difference between angle from the current frame to the previous frame. </summary>
		public float CalculateDeltaAngle()
		{
			float currentProgress = Progress;
			float spdDelta = Character.MoveSpeed * PhysicsManager.physicsDelta;
			spdDelta *= ExtensionMethods.DotAngle(Character.MovementAngle, ForwardAngle);
			Progress += spdDelta;
			float deltaAngle = GetForwardAngle() - ForwardAngle;

			Progress = currentProgress; //Reset
			return deltaAngle;
		}

		//Is the pathfollower ahead of the reference point?
		public bool IsAheadOfPoint(Vector3 globalPosition) => Mathf.Sign(Progress - ActivePath.Curve.GetClosestOffset(globalPosition - ActivePath.GlobalPosition)) > 0;
	}
}
