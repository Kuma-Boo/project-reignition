using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Helps keep track of where the player is relative to the level's path.
	/// NOTE: Paths with vertical surfaces tilted at 180 degrees are unsupported.
	/// </summary>
	public partial class PlayerPathController : PathFollow3D
	{
		private PlayerController Controller { get; set; }

		/// <summary> PathFollower's current path. </summary>
		public Path3D ActivePath { get; private set; }
		/// <summary> PathFollower's previous path. </summary>
		public Path3D PreviousPath { get; private set; }

		/// <summary> Current rotation of the pathfollower, in global radians. </summary>
		public float ForwardAngle { get; private set; }
		/// <summary> Current backwards rotation of the pathfollower, always equal to ForwardAngle + Mathf.Pi. </summary>
		public float BackAngle { get; private set; }
		/// <summary> Delta between last frame and current frame. Updates on Resync(). </summary>
		public float DeltaAngle { get; private set; }
		/// <summary> Local delta to player position. </summary>
		public Vector3 LocalPlayerPositionDelta { get; private set; }
		/// <summary> Absolute delta to player position. </summary>
		public Vector3 GlobalPlayerPositionDelta { get; private set; }

		/// <summary> Custom up axis. Equal to Forward() rotated 90 degrees around RightAxis. </summary>
		public Vector3 HeightAxis { get; private set; }
		/// <summary> Custom right axis. Cross product of Forward() and Vector3.Up [Fallback: ForwardAxis] </summary>
		public Vector3 SideAxis { get; private set; }
		/// <summary> Custom forward axis. Equal to Vector3.Forward.Rotated(Vector3.Up, ForwardAngle) </summary>
		public Vector3 ForwardAxis { get; private set; }

		/// <summary> The change in progress from the previous Resync() call. </summary>
		private float progressDelta;

		public void Initialize(PlayerController controller)
		{
			Controller = controller;
			SetActivePath(StageSettings.instance.CalculateStartingPath(GlobalPosition));
		}

		public void SetActivePath(Path3D newPath)
		{
			if (newPath == null || newPath == ActivePath) return;

			if (IsInsideTree()) //Unparent
				GetParent().RemoveChild(this);
			newPath.AddChild(this);

			PreviousPath = ActivePath;
			ActivePath = newPath;
			Resync();
		}

		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null) return;

			Vector3 syncPoint = Controller.GlobalPosition;
			Progress = ActivePath.Curve.GetClosestOffset(syncPoint - ActivePath.GlobalPosition);

			Loop = ActivePath.Curve.GetPointPosition(0).IsEqualApprox(ActivePath.Curve.GetPointPosition(ActivePath.Curve.PointCount - 1));
			RecalculateData();
		}

		public void RecalculateData()
		{
			float newForwardAngle = ExtensionMethods.CalculateForwardAngle(this.Forward(), this.Up());
			DeltaAngle = ExtensionMethods.SignedDeltaAngleRad(newForwardAngle, ForwardAngle);
			ForwardAngle = newForwardAngle;

			BackAngle = ForwardAngle + Mathf.Pi;
			LocalPlayerPositionDelta = GlobalBasis.Inverse() * (Controller.GlobalPosition - GlobalPosition);
			LocalPlayerPositionDelta *= new Vector3(-1, 1, 1); // Convert to model space
			GlobalPlayerPositionDelta = CalculateDeltaPosition(Controller.GlobalPosition);

			// Update custom orientations
			ForwardAxis = Vector3.Forward.Rotated(Vector3.Up, ForwardAngle).Normalized();
			float upDotProduct = this.Forward().Dot(Vector3.Up);
			if (upDotProduct < .9f)
				SideAxis = this.Forward().Cross(Vector3.Up).Normalized();
			else // Moving straight up/down
				SideAxis = this.Forward().Cross(ForwardAxis).Normalized();

			HeightAxis = this.Forward().Rotated(SideAxis, Mathf.Pi * .5f).Normalized();

			DebugManager.DrawRay(GlobalPosition, HeightAxis, Colors.Green);
			DebugManager.DrawRay(GlobalPosition, ForwardAxis, Colors.Blue);
			DebugManager.DrawRay(GlobalPosition, SideAxis, Colors.Red);
		}

		/// <summary> Calculates the delta position using Basis.Inverse(). </summary>
		public Vector3 CalculateDeltaPosition(Vector3 globalPosition) => Basis.Inverse() * (globalPosition - GlobalPosition);

		/// <summary> Is the pathfollower ahead of the reference point? </summary>
		public bool IsAheadOfPoint(Vector3 pos)
		{
			if (Progress > GetProgress(pos))
				return true;

			// Fallback -- Check the previous position
			float comparisionPoint = Progress - progressDelta + progressDelta;
			return comparisionPoint > GetProgress(pos);
		}

		/// <summary> Returns the progress of a given position, from [0 <-> PathLength]. </summary>
		public float GetProgress(Vector3 pos) => ActivePath.Curve.GetClosestOffset(pos - ActivePath.GlobalPosition);
	}
}
