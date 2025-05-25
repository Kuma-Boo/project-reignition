using System;
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

		public bool IsReversingPath { get; private set; }
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
		/// <summary> The velocity of the player's local horizontal movement. </summary>
		public float LocalHorizontalVelocity { get; private set; }

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
			SetActivePath(StageSettings.Instance.CalculateStartingPath(GlobalPosition));
		}

		public bool SetActivePath(Path3D newPath, bool reversePath = false)
		{
			if (newPath == null ||
				(newPath == ActivePath && reversePath == IsReversingPath))
			{
				return false;
			}

			if (newPath != ActivePath)
			{
				if (IsInsideTree()) // Reparent if needed
					GetParent().RemoveChild(this);
				newPath.AddChild(this);
			}

			IsReversingPath = reversePath;
			PreviousPath = ActivePath;
			ActivePath = newPath;
			Loop = ActivePath.Curve.GetPointPosition(0).IsEqualApprox(ActivePath.Curve.GetPointPosition(ActivePath.Curve.PointCount - 1));
			Resync();
			return true;
		}

		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null) return;

			Vector3 syncPoint = Controller.GlobalPosition;
			Progress = ActivePath.Curve.GetClosestOffset(ActivePath.GlobalBasis.Inverse() * (syncPoint - ActivePath.GlobalPosition));
			RecalculateData();
		}

		public void RecalculateData()
		{
			ForwardAxis = IsReversingPath ? this.Back() : this.Forward();
			float newForwardAngle = ExtensionMethods.CalculateForwardAngle(ForwardAxis, this.Up());
			DeltaAngle = ExtensionMethods.SignedDeltaAngleRad(newForwardAngle, ForwardAngle);
			ForwardAngle = newForwardAngle;

			BackAngle = ForwardAngle + Mathf.Pi;
			LocalPlayerPositionDelta = GlobalBasis.Inverse() * (Controller.GlobalPosition - GlobalPosition);
			if (!IsReversingPath)
				LocalPlayerPositionDelta *= new Vector3(-1, 1, 1); // Convert to model space
			GlobalPlayerPositionDelta = CalculateDeltaPosition(Controller.GlobalPosition);

			// Update custom orientations
			Vector3 localForwardAxis = Vector3.Forward.Rotated(Vector3.Up, ForwardAngle).Normalized();
			float upDotProduct = ForwardAxis.Dot(Vector3.Up);
			if (upDotProduct < .9f)
				SideAxis = ForwardAxis.Cross(Vector3.Up).Normalized();
			else // Moving straight up/down
				SideAxis = ForwardAxis.Cross(localForwardAxis).Normalized();

			HeightAxis = ForwardAxis.Rotated(SideAxis, Mathf.Pi * .5f).Normalized();

			if (Controller.IsMovingBackward || Mathf.IsZeroApprox(Controller.MoveSpeed))
				LocalHorizontalVelocity = 0;
			else
				LocalHorizontalVelocity = Controller.MoveSpeed * ExtensionMethods.DotAngle(Controller.MovementAngle, ForwardAngle + Mathf.Pi * .5f);

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
