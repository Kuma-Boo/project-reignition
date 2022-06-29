using Godot;
using Project.Core;

//Data for movement
namespace Project.Gameplay
{
	public class MovementResource : Resource
	{
		[Export]
		public bool isTwoWay; //Allow negative values
		[Export]
		public bool isInverted; //Flips speed to be negative.
		[Export]
		public bool clampTurnaround; //Don't allow reversing into negative numbers

		[Export(PropertyHint.Range, "0, 256")]
		public int speed;
		[Export(PropertyHint.Range, "0, 256")]
		public int traction; //Speed up rate
		[Export(PropertyHint.Range, "0, 256")]
		public int friction; //Slow down rate
		[Export(PropertyHint.Range, "0, 256")]
		public int overspeedFriction; //Slow down rate when going faster than speed
		[Export(PropertyHint.Range, "0, 256")]
		public int turnaround; //Skidding

		public MovementResource()
		{
			speed = 0;
			traction = 0;
			friction = 0;
		}

		public float Interpolate(float spd, int sign, bool inverted = default)
		{
			if (inverted)
				spd *= -1;

			float targetSpeed = speed;
			float delta = traction;

			if (Mathf.Abs(spd) > speed)
				delta = overspeedFriction;

			if (sign == 0)
			{
				targetSpeed = 0;
				delta = friction;
			}
			else if (sign < 0 && !isTwoWay) //Turning around
			{
				delta = turnaround;
				targetSpeed = clampTurnaround ? 0 : -Mathf.Inf;
			}

			if (isTwoWay)
				targetSpeed *= sign;

			spd = Mathf.MoveToward(spd, targetSpeed, delta * PhysicsManager.physicsDelta);
			return inverted ? -spd : spd;
		}

		public float GetSpeedRatio(float spd) => spd / speed;
		public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), 0f, 1f);
	}
}
