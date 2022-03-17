using Godot;
using Godot.Collections;

namespace Project.Core
{
	public class PhysicsManager : Spatial
	{
		public static float normalDelta;
		public static float physicsDelta;
		private static PhysicsDirectSpaceState physicsState;
		public override void _Ready() => UpdatePhysicsState();

		private void UpdatePhysicsState() => physicsState = GetWorld().DirectSpaceState; //Update world space

		public override void _Process(float delta) => normalDelta = delta;

		public override void _PhysicsProcess(float delta)
		{
			physicsDelta = delta;
			UpdatePhysicsState();
		}

		public const float GRAVITY = 400; //Gravity for generic objects
		public const float MAX_GRAVITY = 80; //Gravity for generic objects
		public static float AddGravity(float i) => Mathf.Clamp(i + GRAVITY * physicsDelta, -Mathf.Inf, MAX_GRAVITY);

		public const string PLAYER_GROUP = "player";
		public const string ENEMY_TAG = "enemy";

		public const float COLLISION_PADDING = 0.02f;

		public const uint ENVIRONMENT_COLLISION_MASK = 1; //Collision mask for environment
		private const string RAYCAST_POSITION = "position";
		private const string RAYCAST_NORMAL = "normal";
		private const string RAYCAST_COLLIDER = "collider";

		public static RaycastHit CastRay(Vector3 pos, Vector3 dir, bool hitAreas, uint mask = 2147483647, Array ex = null)
		{
			Vector3 endPos = pos + dir;
			Dictionary result = physicsState.IntersectRay(pos, endPos, ex, mask, true, hitAreas);
			RaycastHit raycast = new RaycastHit()
			{
				startPoint = pos,
				endPoint = endPos,
				direction = dir
			};

			if (result.Count != 0)
			{
				raycast.point = (Vector3)result[RAYCAST_POSITION];
				raycast.normal = (Vector3)result[RAYCAST_NORMAL];
				raycast.collidedObject = (Node)result[RAYCAST_COLLIDER];
				if (raycast)
					raycast.distance = pos.DistanceTo(raycast.point);
			}

			return raycast;
		}

		public static Node[] OverlapShape(RID shape, Vector3 pos, Basis basis, int maxCollisionCount, uint mask = 2147483647, Array ex = null)
		{
			Array result = physicsState.IntersectShape(new PhysicsShapeQueryParameters()
			{
				ShapeRid = shape,
				Transform = new Transform(basis, pos),
				Exclude = ex,
				CollisionMask = mask
			}, maxCollisionCount);

			if (result.Count != 0)
			{
				Node[] colliders = new Node[result.Count];
				for (int i = 0; i < result.Count; i++)
					colliders[i] = (result[0] as Dictionary)["collider"] as Node;

				return colliders;
			}

			return null;
		}
	}

	public struct RaycastHit
	{
		public static implicit operator bool(RaycastHit sp) => sp.collidedObject != null;

		public Vector3 startPoint;
		public Vector3 endPoint;
		public Vector3 direction;
		public Node collidedObject;
		public Vector3 normal;
		public Vector3 point;
		public float distance;
		public int zIndex;

		public bool Add(RaycastHit hit) //Attempts to add the values of 2 raycasts together. Returns TRUE if successful
		{
			if (!hit) //Invalid raycast
				return false;

			if (!this)
				collidedObject = hit.collidedObject;

			normal += hit.normal;
			point += hit.point;
			direction += hit.direction;
			distance += hit.distance;
			return true;
		}

		public bool Divide(float amount) //Attempts to divide the values of a raycast. Returns TRUE if successful
		{
			if (Mathf.IsZeroApprox(amount) || !this)
				return false;

			normal /= amount;
			point /= amount;
			direction /= amount;
			distance /= amount;
			return true;
		}
	}
}