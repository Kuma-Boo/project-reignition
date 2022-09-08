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
		public bool clampTurnaround; //Don't allow reversing into negative numbers

		[Export(PropertyHint.Range, "-256, 256")]
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

		public float Interpolate(float spd, float signedValue)
		{
			float targetSpeed = speed * signedValue;
			float delta = traction;

			if (Mathf.Abs(spd) > speed)
				delta = overspeedFriction;

			if (signedValue == 0) //Deccelerate
			{
				targetSpeed = 0;
				delta = friction;
			}
			else if(Mathf.Sign(signedValue) != Mathf.Sign(spd)) //Turnaround
			{
				delta = turnaround;
				if (clampTurnaround)
					targetSpeed = 0;
			}

			spd = Mathf.MoveToward(spd, targetSpeed, delta * PhysicsManager.physicsDelta);
			return spd;
		}

		public float GetSpeedRatio(float spd) => spd / speed;
		public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), -1f, 1f);
	}
}
