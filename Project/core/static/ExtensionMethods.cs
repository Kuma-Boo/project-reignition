using Godot;
using Godot.Collections;
using Project.Core;

namespace Project
{
	public static class ExtensionMethods
	{
		/// <summary> Casts a ray from a Node3D. </summary>
		public static RaycastHit CastRay(this Node3D s, Vector3 pos, Vector3 dir, uint mask = 2147483647, bool hitArea = false, Array<PhysicsBody3D> ex = null)
		{
			if (ex != null) // Reduce memory leakage
			{
				Array<Rid> excluded = new();
				for (int i = 0; i < ex.Count; i++)
					excluded.Add(ex[i].GetRid());

				return PhysicsManager.CastRay(pos, dir, hitArea, mask, excluded);
			}

			return PhysicsManager.CastRay(pos, dir, hitArea, mask);
		}


		/// <summary> Creates a property dictionary to be used in _GetPropertyList(). </summary>
		public static Dictionary CreateProperty(string name, Variant.Type type, PropertyHint hint = PropertyHint.None, string hint_string = "")
		{
			Dictionary dictionary = new()
			{
				{ "name", name },
				{ "type", (long)type },
				{ "hint", (long)hint },
				{ "hint_string", hint_string }
			};
			return dictionary;
		}

		/// <summary> Returns a string containing all enum values. For Inspector. </summary>
		public static string EnumToString<T>(this T e)
		{
			System.Type t = e.GetType();
			string[] names = System.Enum.GetNames(t);
			string output = "";

			for (int i = 0; i < names.Length; i++)
			{
				if (i != 0)
					output += ",";

				output += names[i];
			}

			return output;
		}


		public static float CalculateForwardAngle(Vector3 forwardVector, Vector3 upVector)
		{
			float dot = forwardVector.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > .9f) // Moving vertically
				forwardVector = -upVector * Mathf.Sign(dot);

			return forwardVector.Flatten().Normalized().AngleTo(Vector2.Down);
		}

		/// <summary> Overload method that uses Vector3.Up as upVector. </summary>
		public static float CalculateForwardAngle(Vector3 forwardVector) => CalculateForwardAngle(forwardVector, Vector3.Up);


		/// <summary>
		/// Checks if a uint flag is set.
		/// Note: Flags must be set/unset manually. Use "flags |= flag" to set and "flags &= ~flag" to unset.
		/// </summary>
		public static bool HasFlag(this uint flags, uint flag) => (flags & flag) != 0;


		//Global Directions
		public static Vector3 Up(this Node3D s) => s.GlobalTransform.Basis.Y.Normalized();
		public static Vector3 Down(this Node3D s) => -s.GlobalTransform.Basis.Y.Normalized();
		public static Vector3 Forward(this Node3D s) => s.GlobalTransform.Basis.Z.Normalized();
		public static Vector3 Back(this Node3D s) => -s.GlobalTransform.Basis.Z.Normalized();
		public static Vector3 Right(this Node3D s) => s.GlobalTransform.Basis.X.Normalized();
		public static Vector3 Left(this Node3D s) => -s.GlobalTransform.Basis.X.Normalized();

		public static Vector3 RemoveVertical(this Vector3 v) => new(v.X, 0, v.Z);
		public static Vector2 Flatten(this Vector3 v) => new(v.X, v.Z);


		/// <summary> Adds an explosive force to RigidBody3D. </summary>
		public static void AddExplosionForce(this RigidBody3D body, Vector3 explosionPoint, float power)
		{
			Vector3 blastDir = body.GlobalPosition - explosionPoint;
			float distance = blastDir.Length();

			if (Mathf.IsZeroApprox(distance)) return;
			float invDistance = 1 / distance;
			float impulseMag = power * invDistance * invDistance;
			body.ApplyImpulse(impulseMag * blastDir.Normalized());
		}


		/// <summary> Manual implementation since Array.IndexOf() doesn't seem to work on StringNames. </summary>
		public static int GetStringNameIndex(this Array<StringName> a, StringName s)
		{
			for (int i = 0; i < a.Count; i++)
			{
				if (s == a[i])
					return i;
			}

			return -1;
		}


		/// <summary> Returns the dot product of two angles (in radians) </summary>
		public static float DotAngle(float a, float b)
		{
			float dot = (DeltaAngleRad(a, b) / Mathf.Pi) * 2;
			dot = dot <= 1 ? 1 - dot : -(dot - 1);
			return dot;
		}


		/// <summary> Clamps an angle's distance to the reference angle, in radians. </summary>
		public static float ClampAngleRange(float value, float reference, float range)
		{
			range = Mathf.Abs(range); //Ensure range is positive
			reference = ModAngle(reference);
			value = ModAngle(value);
			//Attempt to keep value and reference within range of Mathf.PI
			if (value < reference - Mathf.Pi)
				value += Mathf.Tau;
			else if (value > reference + Mathf.Pi)
				value -= Mathf.Tau;

			float min = reference - range;
			float max = reference + range;

			if (value > min && value < max) //Input is between the two angles, no need to clamp
				return value;

			//Clamping needed
			if (DeltaAngleRad(value, min) < DeltaAngleRad(value, max)) //Closer to min angle
				return min;
			else //Closer to max angle
				return max;
		}


		/// <summary> Converts an angle to exist between 0 <-> Mathf.Tau
		public static float ModAngle(float angle)
		{
			angle %= Mathf.Tau;
			if (angle < 0)
				angle += Mathf.Tau;
			return angle;
		}


		/// <summary> Reflects an angle across angles -Mathf.Pi/Mathf.Pi.
		public static float ReflectAngle(float angle, float referenceAngle = 0)
		{
			float delta = angle - referenceAngle;
			int sign = Mathf.Sign(delta);
			return referenceAngle + (Mathf.Pi * sign - delta); // Reflect targetAngle
		}


		/// <summary> Returns the absolute delta between two angles (in radians) </summary>
		public static float DeltaAngleRad(float firstAngle, float secondAngle) => Mathf.Abs(SignedDeltaAngleRad(firstAngle, secondAngle));


		/// <summary> Returns the delta between two angles (in radians) </summary>
		public static float SignedDeltaAngleRad(float firstAngle, float secondAngle)
		{
			float delta = ModAngle(firstAngle) - ModAngle(secondAngle);
			if (delta > Mathf.Pi)
				delta -= Mathf.Tau;
			else if (delta < -Mathf.Pi)
				delta += Mathf.Tau;
			return delta;
		}


		/// <summary> Moves toward an angle (in radians) </summary>
		public static float MoveTowardAngleRad(float from, float to, float delta)
		{
			from %= Mathf.Tau;
			to %= Mathf.Tau;
			return from + ((to - from) * delta);
		}


		//Smooth damp functions
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
			Vector2 output = new(SmoothDamp(current.X, target.X, ref currentVelocity.X, smoothTime, maxSpeed),
				SmoothDamp(current.Y, target.Y, ref currentVelocity.Y, smoothTime, maxSpeed));
			return output;
		}


		public static Vector3 SmoothDamp(this Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed = Mathf.Inf)
		{
			Vector3 output = new(SmoothDamp(current.X, target.X, ref currentVelocity.X, smoothTime, maxSpeed),
				SmoothDamp(current.Y, target.Y, ref currentVelocity.Y, smoothTime, maxSpeed),
				SmoothDamp(current.Z, target.Z, ref currentVelocity.Z, smoothTime, maxSpeed));
			return output;
		}


		/// <summary> Formats exp into the typical format displayed on menus. </summary>
		public static string FormatEXP(int exp) => exp.ToString("0000000") + "e";
		/// <summary> Formats a score into the typical format displayed on menus. </summary>
		public static string FormatScore(int score) => score.ToString("00000000");
		/// <summary> Formats a number of seconds into the typical format displayed on menus. </summary>
		public static string FormatTime(float time)
		{
			System.TimeSpan span = System.TimeSpan.FromSeconds(time);
			return span.ToString("mm':'ss'.'ff");
		}
	}
}
