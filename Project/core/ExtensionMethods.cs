using Godot;
using Godot.Collections;

namespace Project.Core
{
	public static class ExtensionMethods
	{
		//Casts a ray excluding this object
		public static RaycastHit CastRay(this Spatial s, Vector3 pos, Vector3 dir, uint mask = 2147483647, bool hitArea = false, Array ex = null)
		{
			AddObjectToArray(s, ref ex);
			return PhysicsManager.CastRay(pos, dir, hitArea, mask, ex);
		}
		public static Node[] OverlapShape(this Spatial s, RID rid, Vector3 pos, Basis basis, int maxCollisionCount, uint mask = 2147483647, Array ex = null)
		{
			AddObjectToArray(s, ref ex);
			return PhysicsManager.OverlapShape(rid, pos, basis, maxCollisionCount, mask, ex);
		}

		public static void AddObjectToArray(object o, ref Array i)
		{
			if (i != null)
				i.Add(o);
			else
				i = new Array { o };
		}

		/*
		Note that Godot uses OpenGL's transformation, so "Forward" is actually backwards from the editor's POV.
		Yes, I hate it. No, there's nothing I can do about it.
		*/
		//Global Directions
		public static Vector3 Up(this Spatial s) => s.GlobalTransform.basis.y.Normalized();
		public static Vector3 Down(this Spatial s) => -s.GlobalTransform.basis.y.Normalized();
		public static Vector3 Forward(this Spatial s) => s.GlobalTransform.basis.z.Normalized();
		public static Vector3 Back(this Spatial s) => -s.GlobalTransform.basis.z.Normalized();
		public static Vector3 Right(this Spatial s) => s.GlobalTransform.basis.x.Normalized();
		public static Vector3 Left(this Spatial s) => -s.GlobalTransform.basis.x.Normalized();

		//Local directions
		public static Vector3 LocalUp(this Spatial s) => s.Transform.basis.y.Normalized();
		public static Vector3 LocalDown(this Spatial s) => -s.Transform.basis.y.Normalized();
		public static Vector3 LocalForward(this Spatial s) => s.Transform.basis.z.Normalized();
		public static Vector3 LocalBack(this Spatial s) => -s.Transform.basis.z.Normalized();
		public static Vector3 LocalRight(this Spatial s) => s.Transform.basis.x.Normalized();
		public static Vector3 LocalLeft(this Spatial s) => -s.Transform.basis.x.Normalized();

		//Directly from the transforms
		public static Vector3 Up(this Transform t) => t.basis.y.Normalized();
		public static Vector3 Down(this Transform t) => -t.basis.y.Normalized();
		public static Vector3 Forward(this Transform t) => t.basis.z.Normalized();
		public static Vector3 Back(this Transform t) => -t.basis.z.Normalized();
		public static Vector3 Right(this Transform t) => t.basis.x.Normalized();
		public static Vector3 Left(this Transform t) => -t.basis.x.Normalized();

		public static void SetGlobalPosition(this Spatial s, Vector3 p)
		{
			Transform t = s.GlobalTransform;
			t.origin = p;
			s.GlobalTransform = t;
		}

		public static Vector2 RemoveVertical(this Vector3 v) => new Vector2(v.x, v.z);
		public static Vector3 Flatten(this Vector3 v) => new Vector3(v.x, 0, v.z);
		public static Vector3 Unflatten(this Vector2 v) => new Vector3(v.x, 0, v.y); //Convert 2d to 3d by adding y axis
		public static Vector2 RotateLinear(this Vector2 v, Vector2 t, float spd)
		{
			float ang = v.AngleTo(t);
			int sign = Mathf.Sign(ang);
			ang = Mathf.Abs(ang);
			return v.Rotated(Mathf.Min(ang, spd) * sign);
		}
		public static Vector2 RotateSmooth(this Vector2 v, Vector2 t, float spd) => v.Rotated(v.AngleTo(t) * spd);

		//Rotates a vector to align to a transform
		public static Vector3 AlignVectorToTransform(this Vector3 v, Transform t)
		{
			v = v.Rotated(t.Up(), t.Forward().SignedAngleTo(Vector3.Forward, t.Up()));
			v = v.Rotated(t.Right(), t.Up().SignedAngleTo(Vector3.Up, t.Right()));
			return v;
		}

		public static void AddExplosionForce(this RigidBody body, Vector3 explosionPoint, float power)
		{
			Vector3 blastDir = body.GlobalTransform.origin - explosionPoint;
			float distance = blastDir.Length();

			if (Mathf.IsZeroApprox(distance)) return;
			float invDistance = 1 / distance;
			float impulseMag = power * invDistance * invDistance;
			body.AddCentralForce(impulseMag * blastDir);
		}

		//Returns the absolute angle between 2 angles
		public static float DeltaAngleDegrees(float firstAngle, float secondAngle)
		{
			float d = Mathf.Abs(firstAngle - secondAngle) % 360;
			if (d > 180)
				d -= 360;
			return Mathf.Abs(d);
		}
		public static float MoveTowardAngleDegrees(float from, float to, float delta)
		{
			if (Mathf.Abs(to - from) > 180)
			{
				if (to > from)
					from += 360;
				else
					to += 360;
			}

			float value = (from + ((to - from) * delta));
			if (value >= 0 && value <= 360)
				return value;

			return (value % 360);
		}

		public static bool IsLoopingPath(this Curve3D c) => c.GetPointPosition(0).IsEqualApprox(c.GetPointPosition(c.GetPointCount() - 1));

		//In Radians
		public static float DeltaAngleRad(float firstAngle, float secondAngle)
		{
			return Mathf.Deg2Rad(DeltaAngleDegrees(Mathf.Rad2Deg(firstAngle), Mathf.Rad2Deg(secondAngle)));
		}

		public static float MoveTowardAngleRad(float firstAngle, float secondAngle, float delta)
		{
			return Mathf.Deg2Rad(MoveTowardAngleDegrees(Mathf.Rad2Deg(firstAngle), Mathf.Rad2Deg(secondAngle), delta));
		}

		//For Flags
		public static bool IsSet<T>(this T flags, T flag) where T : struct
		{
			int flagsValue = (int)(object)flags;
			int flagValue = (int)(object)flag;

			return (flagsValue & flagValue) != 0;
		}

		public static void Set<T>(this T flags, T flag) where T : struct
		{
			int flagsValue = (int)(object)flags;
			int flagValue = (int)(object)flag;

			flags = (T)(object)(flagsValue | flagValue);
		}

		public static void Unset<T>(this T flags, T flag) where T : struct
		{
			int flagsValue = (int)(object)flags;
			int flagValue = (int)(object)flag;

			flags = (T)(object)(flagsValue & (~flagValue));
		}
	}
}
