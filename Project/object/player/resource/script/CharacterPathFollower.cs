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
		/// <summary> Delta between last frame and current frame. Updates on Resync(). </summary>
		public float DeltaAngle { get; private set; }
		public Path3D ActivePath { get; private set; }
		public Vector3 PlayerPositionDelta { get; private set; } //Local delta to player position

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

			UpdatePosition();
			//Progress = ActivePath.Curve.GetClosestOffset(Character.GlobalPosition - ActivePath.GlobalPosition);

			float newForwardAngle = CharacterController.CalculateForwardAngle(this.Forward());
			DeltaAngle = newForwardAngle - ForwardAngle;
			ForwardAngle = newForwardAngle;

			BackAngle = ForwardAngle + Mathf.Pi;
			PlayerPositionDelta = (Character.GlobalPosition - GlobalPosition).Rotated(Vector3.Up, -ForwardAngle);
		}

		/// <summary>
		/// GetClosestOffset() seems to be broken in 4.0, so here's a more accurate (allbeit slower) method.
		/// Loops over all baked points in a path, so try to restrict it's usage as much as possible.
		/// </summary>
		private void UpdatePosition()
		{
			Vector3 targetPosition = Character.GlobalPosition - ActivePath.GlobalPosition;
			Vector3[] points = ActivePath.Curve.GetBakedPoints();
			float closestPointDistance = Mathf.Inf;
			int closestPointIndex = -1;

			//Get the closest baked point
			for (int i = 0; i < points.Length; i++)
			{
				float distance = points[i].DistanceTo(targetPosition);
				if (distance < closestPointDistance)
				{
					closestPointIndex = i;
					closestPointDistance = distance;
				}
			}

			Progress = ActivePath.Curve.GetClosestOffset(targetPosition); //Estimate for external objects to reference

			if (closestPointIndex >= points.Length - 1) //Limit point index
				closestPointIndex--;

			//Assign transform
			Vector3 position = points[closestPointIndex];
			Vector3 nextPoint = points[closestPointIndex + 1];
			Vector3 forwardDirection = (position - nextPoint).Normalized();

			//Attempt to interpolate between baked points (Spaghetti code that seems to work alright)
			if (closestPointIndex != 0 && closestPointIndex != points.Length)
			{
				float nextDistance = nextPoint.DistanceTo(position);
				targetPosition = (targetPosition - position).Rotated(Vector3.Up, -forwardDirection.SignedAngleTo(Vector3.Forward, Vector3.Up));

				float t = 1.0f - Mathf.Clamp(targetPosition.x / nextDistance, 0f, 1f);
				position = position.Lerp(nextPoint, t);
			}

			position += ActivePath.GlobalPosition;
			LookAtFromPosition(position, position + forwardDirection, this.Up());
		}

		//Is the pathfollower ahead of the reference point?
		public bool IsAheadOfPoint(Vector3 globalPosition) => Mathf.Sign(Progress - ActivePath.Curve.GetClosestOffset(globalPosition - ActivePath.GlobalPosition)) > 0;
	}
}
