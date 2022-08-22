using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for keeping the player orientated on the path.
	/// </summary>
	public class CharacterPathFollower : PathFollow
	{
		[Export]
		public NodePath character;
		public CharacterController _character;

		public override void _Ready() => _character = GetNode<CharacterController>(character);

		#region Path Data
		public bool isPathMovingForward = true; //Set this to false to move backwards along the path (Useful for reverse acts)
		public int PathTravelDirection => isPathMovingForward ? 1 : -1;
		public Vector3 Xform(Vector3 v) => GlobalTransform.basis.Xform(v);
		public Vector3 ForwardDirection => this.Forward() * PathTravelDirection;
		public Vector3 StrafeDirection => ForwardDirection.Cross(_character.worldDirection).Normalized();

		public Path ActivePath { get; private set; }

		public void SetActivePath(Path newPath)
		{
			if (newPath == null) return;

			if (IsInsideTree())
				GetParent().RemoveChild(this);

			ActivePath = newPath;
			Loop = newPath.Curve.IsLoopingPath();

			newPath.AddChild(this);
			Resync();
		}
		#endregion
		
		#region Path Positions
		//Moves the pathfollower's offset by movementDelta using the path travel direction
		public void UpdateOffset(float movementDelta) => Offset += movementDelta * PathTravelDirection;

		//Position of player relative to PathFollower.
		public Vector3 LocalPlayerPosition => GlobalTransform.basis.XformInv(_character.GlobalTranslation - GlobalTranslation);

		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null) return;

			Vector3 p = _character.GlobalTranslation - ActivePath.GlobalTranslation;
			Offset = ActivePath.Curve.GetClosestOffset(p);

			if (_character.isSideScroller)
				_character.RecenterStrafe();
		}

		public bool IsAheadOfPoint(Vector3 position) //Is the player moving forward compared to the reference point? (Based on Path and PathTravelDirection)
		{
			Vector3 p = position - ActivePath.GlobalTranslation;
			float comparisonOffset = ActivePath.Curve.GetClosestOffset(p);
			return Mathf.Sign(Offset - comparisonOffset) * PathTravelDirection > 0;
		}
		#endregion
	}
}
