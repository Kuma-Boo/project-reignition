using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class DriftCorner : StageObject
	{
		public static float MINIMUM_ENTRANCE_SPEED = 20f;

		//When the player enters this trigger, they will skid to a stop before dashing in a 90 degree angle
		public override bool IsRespawnable() => false;

		[Export]
		public float slideDistance = 10;
		[Export]
		public bool isRightTurn;
		public Vector3 EndPosition => MiddlePosition + GlobalTransform.Right() * (isRightTurn ? 1 : -1) * slideDistance;
		public Vector3 MiddlePosition => GlobalTransform.origin + GlobalTransform.Forward() * slideDistance;

		private float entranceSpeed;

		public override void OnStay()
		{
			if (Character.IsOnGround && Character.MoveSpeed > MINIMUM_ENTRANCE_SPEED)
			{
				entranceSpeed = Character.MoveSpeed;
				Character.StartDrift(this);
			}
		}
	}
}