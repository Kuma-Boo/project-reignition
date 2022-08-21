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
		public int PathTravelDirection => isPathMovingForward ? 1 : -1; //TODO implement reverse paths later
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

			//Reset offset transform
			offsetExtension = 0;

			newPath.AddChild(this);
			Resync();
		}
		#endregion
		
		#region Path Positions
		//Offset used at the start/end of a non-looping path ends.
		public float offsetExtension;

		//Moves the pathfollower's offset by movementDelta w/ extension support
		public void UpdateOffset(float movementDelta)
		{
			if (Loop)
			{
				Offset += movementDelta * PathTravelDirection;
				return;
			}
			
			if (Mathf.IsZeroApprox(offsetExtension))
			{
				float oldOffset = Offset;
				Offset += movementDelta * PathTravelDirection;

				if (UnitOffset >= 1f || UnitOffset <= 0)
				{
					//Extrapolate path
					float extra = Mathf.Abs(movementDelta) - Mathf.Abs(oldOffset - Offset);
					offsetExtension += Mathf.Sign(movementDelta) * extra;
				}
			}
			else
			{
				int oldSign = Mathf.Sign(offsetExtension);
				offsetExtension += movementDelta;

				//Merge back onto the path
				if (Mathf.Sign(offsetExtension) != oldSign)
				{
					Offset += offsetExtension;
					offsetExtension = 0;
				}
			}

			if(offsetExtension != 0)
				GlobalTranslation += this.Forward() * offsetExtension;
		}

		//Position of player relative to PathFollower.
		public Vector3 LocalPlayerPosition => GlobalTransform.basis.XformInv(_character.GlobalTranslation - GlobalTranslation);

		public void Resync()
		{
			if (!IsInsideTree()) return;
			if (ActivePath == null || offsetExtension != 0) return;

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
