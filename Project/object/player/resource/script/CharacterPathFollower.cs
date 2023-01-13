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
		/// <summary> Local delta to player position. </summary>
		public Vector3 FlatPlayerPositionDelta { get; private set; }
		/// <summary> "True" local delta to player position using Basis.Inverse(). </summary>
		public Vector3 TruePlayerPositionDelta { get; private set; }

		/// <summary> Custom up axis. Equal to Forward() rotated 90 degrees around RightAxis. </summary>
		public Vector3 UpAxis { get; private set; }
		/// <summary> Custom right axis. Cross product of Forward() and Vector3.Up [Fallback: ForwardAxis] </summary>
		public Vector3 RightAxis { get; private set; }
		/// <summary> Custom forward axis. Equal to Vector3.Forward.Rotated(Vector3.Up, ForwardAngle) </summary>
		public Vector3 ForwardAxis { get; private set; }

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

			Vector3 syncPoint = Character.GlobalPosition;
			Progress = ActivePath.Curve.GetClosestOffset(syncPoint - ActivePath.GlobalPosition);

			float newForwardAngle = Character.CalculateForwardAngle(this.Forward());
			DeltaAngle = ExtensionMethods.SignedDeltaAngleRad(newForwardAngle, ForwardAngle) * .5f;
			ForwardAngle = newForwardAngle;

			BackAngle = ForwardAngle + Mathf.Pi;
			FlatPlayerPositionDelta = (Character.GlobalPosition - GlobalPosition).Rotated(Vector3.Up, -ForwardAngle);
			TruePlayerPositionDelta = Basis.Inverse() * (Character.GlobalPosition - GlobalPosition);

			//Update custom orientations
			ForwardAxis = Vector3.Forward.Rotated(Vector3.Up, BackAngle).Normalized();
			float upDotProduct = this.Forward().Dot(Vector3.Up);
			if (upDotProduct < .9f)
				RightAxis = this.Forward().Cross(Vector3.Up).Normalized();
			else //Moving straight up/down
				RightAxis = this.Back().Cross(ForwardAxis).Normalized();
			UpAxis = this.Forward().Rotated(RightAxis, Mathf.Pi * .5f).Normalized();

			Debug.DrawRay(GlobalPosition, ForwardAxis, Colors.Blue);
			Debug.DrawRay(GlobalPosition, RightAxis, Colors.Red);
			Debug.DrawRay(GlobalPosition, UpAxis, Colors.Green);
		}

		//Is the pathfollower ahead of the reference point?
		public bool IsAheadOfPoint(Vector3 globalPosition) => Mathf.Sign(Progress - ActivePath.Curve.GetClosestOffset(globalPosition - ActivePath.GlobalPosition)) > 0;
	}
}
