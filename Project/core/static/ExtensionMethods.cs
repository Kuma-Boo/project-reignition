using Godot;
using Godot.Collections;

namespace Project
{
	public static class ExtensionMethods
	{
		/// <summary> Casts a ray from a Node3D </summary>
		public static Core.RaycastHit CastRay(this Node3D s, Vector3 pos, Vector3 dir, uint mask = 2147483647, bool hitArea = false, Array ex = null)
		{
			if (ex != null)
				ex.Add(s);
			else
				ex = new Array { s };
			return Core.PhysicsManager.CastRay(pos, dir, hitArea, mask, ex);
		}

		/// <summary> Creates a property dictionary to be used in _GetPropertyList() </summary>
		public static Dictionary CreateProperty(string name, Variant.Type type, PropertyHint hint = PropertyHint.None, string hint_string = "")
		{
			Dictionary dictionary = new Dictionary();
			dictionary.Add("name", name);
			dictionary.Add("type", (long)type);
			dictionary.Add("hint", (long)hint);
			dictionary.Add("hint_string", hint_string);
			return dictionary;
		}

		//Global Directions
		public static Vector3 Up(this Node3D s) => s.GlobalTransform.basis.y.Normalized();
		public static Vector3 Down(this Node3D s) => -s.GlobalTransform.basis.y.Normalized();
		public static Vector3 Forward(this Node3D s) => s.GlobalTransform.basis.z.Normalized();
		public static Vector3 Back(this Node3D s) => -s.GlobalTransform.basis.z.Normalized();
		public static Vector3 Right(this Node3D s) => s.GlobalTransform.basis.x.Normalized();
		public static Vector3 Left(this Node3D s) => -s.GlobalTransform.basis.x.Normalized();

		public static Vector3 RemoveVertical(this Vector3 v) => new Vector3(v.x, 0, v.z);
		public static Vector2 Flatten(this Vector3 v) => new Vector2(v.x, v.z);
		public static float InverseLerp(this Vector3 a, Vector3 b, Vector3 v)
		{
			Vector3 ab = b - a;
			Vector3 av = v - a;
			return av.Dot(ab) / ab.Dot(ab);
		}

		public static void AddExplosionForce(this RigidBody3D body, Vector3 explosionPoint, float power)
		{
			Vector3 blastDir = body.GlobalPosition - explosionPoint;
			float distance = blastDir.Length();

			if (Mathf.IsZeroApprox(distance)) return;
			float invDistance = 1 / distance;
			float impulseMag = power * invDistance * invDistance;
			body.ApplyCentralImpulse(impulseMag * blastDir);
		}

		/// <summary> Converts angle to 0 <-> Mathf.Tau </summary>
		public static float ModAngle(float value)
		{
			value %= Mathf.Tau; //Mod by Tau
			if (value < 0) //Ensure value is positive
				value += Mathf.Tau;
			return value;
		}

		/// <summary> Returns the dot product of two angles (in radians) </summary>
		public static float DotAngle(float a, float b)
		{
			float dot = (DeltaAngleRad(a, b) / Mathf.Pi) * 2;
			dot = dot <= 1 ? 1 - dot : -(dot - 1);
			return dot;
		}

		/// <summary> Clamps an angle between two angles, in radians. </summary>
		public static float ClampAngle(float value, float min, float max)
		{
			value = ModAngle(value);
			min = ModAngle(min);
			max = ModAngle(max);

			if (min > max)
				min -= Mathf.Tau;

			float minDelta = DeltaAngleRad(value, min);
			float maxDelta = DeltaAngleRad(value, max);
			if (minDelta < maxDelta && value > max)
				value -= Mathf.Tau;

			return Mathf.Clamp(value, min, max);
		}

		/// <summary> For when you have an reference angle and a range, but are too lazy to calculate a min and max. </summary>
		public static float ClampAngleRange(float value, float reference, float range)
		{
			float clampMin = reference - Mathf.Abs(range);
			float clampMax = reference + Mathf.Abs(range);
			return ClampAngle(value, clampMin, clampMax);
		}

		/// <summary> Returns the absolute delta between two angles (in radians) </summary>
		public static float DeltaAngleRad(float firstAngle, float secondAngle)
		{
			firstAngle = ModAngle(firstAngle);
			secondAngle = ModAngle(secondAngle);
			float delta = Mathf.Abs(firstAngle - secondAngle);
			if (delta > Mathf.Pi)
				delta = Mathf.Tau - delta;
			return delta;
		}

		/// <summary> Moves toward an angle (in radians) </summary>
		public static float MoveTowardAngleRad(float from, float to, float delta)
		{
			from = ModAngle(from);
			to = ModAngle(to);
			return from + ((to - from) * delta);
		}

		//Smooth damp functions
		public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			float deltaTime = Core.PhysicsManager.physicsDelta;
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
		public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			current %= Mathf.Tau;
			target %= Mathf.Tau;

			if (Mathf.Abs(target - current) > Mathf.Pi)
			{
				if (target > current)
					current += Mathf.Tau;
				else
					target += Mathf.Tau;
			}
			float result = SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed);
			if (result > Mathf.Pi) //Keep return angle between -Mathf.Pi <-> Mathf.Pi
				result -= Mathf.Tau;

			return result;
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

		//For flag modification
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
