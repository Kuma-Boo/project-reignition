using Godot;
using Godot.Collections;

namespace Project.Core;

public partial class PhysicsManager : Node3D
{
	public static PhysicsManager Instance { get; private set; }

	public static float normalDelta;
	public static float physicsDelta;
	public static PhysicsDirectSpaceState3D physicsState;

	public override void _EnterTree() => Instance = this;

	private void UpdatePhysicsState() => physicsState = GetWorld3D().DirectSpaceState; //Update world's physics space

	public override void _Process(double delta) => normalDelta = (float)delta;

	public override void _PhysicsProcess(double delta)
	{
		physicsDelta = (float)delta;
		UpdatePhysicsState();
	}

	public const uint ENVIRONMENT_COLLISION_MASK = 1; //Collision mask for environment
	private const string RAYCAST_POSITION = "position";
	private const string RAYCAST_NORMAL = "normal";
	private const string RAYCAST_COLLIDER = "collider";

	public static RaycastHit CastRay(Vector3 pos, Vector3 dir, bool hitAreas, uint mask = 2147483647, Array<Rid> ex = null)
	{
		Vector3 endPos = pos + dir;

		PhysicsRayQueryParameters3D rayQuery = new()
		{
			From = pos,
			To = endPos,
			CollisionMask = mask,
			CollideWithBodies = true,
			CollideWithAreas = hitAreas,
			HitBackFaces = false
		};

		if (ex != null)
			rayQuery.Exclude = ex;

		Dictionary result = physicsState.IntersectRay(rayQuery);
		RaycastHit raycast = new()
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

		//Fix memory leaks
		if (ex != null)
		{
			ex.Clear();
			rayQuery.Exclude.Clear();
		}

		rayQuery.Dispose();
		result.Dispose();
		return raycast;
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

	/// <summary> Attempts to add hit2 to hit1. </summary>
	public static RaycastHit Add(RaycastHit hit1, RaycastHit hit2)
	{
		if (!hit2) // Invalid raycast
			return hit1;

		if (!hit1) // Overwrite hit1 with hit2.
			return hit2;

		hit1.normal += hit2.normal;
		hit1.point += hit2.point;
		hit1.direction += hit2.direction;
		hit1.distance += hit2.distance;
		return hit1;
	}

	/// <summary> Attempts to divide the values of a raycast. </summary>
	public static RaycastHit Divide(RaycastHit hit, float amount)
	{
		if (Mathf.IsZeroApprox(amount) || !hit)
			return hit;

		hit.normal /= amount;
		hit.point /= amount;
		hit.direction /= amount;
		hit.distance /= amount;
		return hit;
	}
}