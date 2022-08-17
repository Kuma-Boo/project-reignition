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

		public static Vector2 Flatten(this Vector3 v) => new Vector2(v.x, v.z);
		public static Vector3 RemoveVertical(this Vector3 v) => new Vector3(v.x, 0, v.z);
		public static Vector2 RotateLinear(this Vector2 v, Vector2 t, float spd)
		{
			float ang = v.AngleTo(t);
			int sign = Mathf.Sign(ang);
			ang = Mathf.Abs(ang);
			return v.Rotated(Mathf.Min(ang, spd) * sign);
		}
		public static Vector2 RotateSmooth(this Vector2 v, Vector2 t, float spd) => v.Rotated(v.AngleTo(t) * spd);

		public static void AddExplosionForce(this RigidBody body, Vector3 explosionPoint, float power)
		{
			Vector3 blastDir = body.GlobalTranslation - explosionPoint;
			float distance = blastDir.Length();

			if (Mathf.IsZeroApprox(distance)) return;
			float invDistance = 1 / distance;
			float impulseMag = power * invDistance * invDistance;
			body.ApplyCentralImpulse(impulseMag * blastDir);
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
		public static float DeltaAngleRad(float firstAngle, float secondAngle) => Mathf.Deg2Rad(DeltaAngleDegrees(Mathf.Rad2Deg(firstAngle), Mathf.Rad2Deg(secondAngle)));
		public static float MoveTowardAngleRad(float firstAngle, float secondAngle, float delta) => Mathf.Deg2Rad(MoveTowardAngleDegrees(Mathf.Rad2Deg(firstAngle), Mathf.Rad2Deg(secondAngle), delta));

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

		public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			float deltaTime = PhysicsManager.physicsDelta;
			smoothTime = Mathf.Max(0.0001f, smoothTime);
			float omega = 2f / smoothTime;

			float x = omega * deltaTime;
			float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
			float change = current - target;
			float originalTo = target;

			// Clamp maximum speed
			float maxChange = maxSpeed * smoothTime;
			change = Mathf.Clamp(change, -maxChange, maxChange);
			target = current - change;

			float temp = (currentVelocity + omega * change) * deltaTime;
			currentVelocity = (currentVelocity - omega * temp) * exp;
			float output = target + (change + temp) * exp;

			// Prevent overshooting
			if (originalTo - current > 0.0f == output > originalTo)
			{
				output = originalTo;
				currentVelocity = (output - originalTo) / deltaTime;
			}

			return output;
		}

		//Probably broken -_-
		public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			if (Mathf.Abs(target - current) > Mathf.Pi)
			{
				if (target > current)
					current += Mathf.Tau;
				else
					target += Mathf.Tau;
			}

			return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed);
		}

		public static Vector2 SmoothDamp(this Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			Vector2 output = new Vector2(SmoothDamp(current.x, target.x, ref currentVelocity.x, smoothTime, maxSpeed),
				SmoothDamp(current.y, target.y, ref currentVelocity.y, smoothTime, maxSpeed));
			return output;
		}

		public static Vector3 SmoothDamp(this Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			Vector3 output = new Vector3(SmoothDamp(current.x, target.x, ref currentVelocity.x, smoothTime, maxSpeed),
				SmoothDamp(current.y, target.y, ref currentVelocity.y, smoothTime, maxSpeed),
				SmoothDamp(current.z, target.z, ref currentVelocity.z, smoothTime, maxSpeed));
			return output;
		}
	}
}
