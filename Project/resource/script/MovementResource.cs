using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Contains data of movement settings. Leave values at -1 to ignore (primarily for skill overrides)
/// </summary>
[GlobalClass]
public partial class MovementResource : Resource
{
	[Export]
	public bool clampTurnaround; //Don't allow reversing into negative numbers

	[Export(PropertyHint.Range, "-1, 256")]
	public int speed = -1;
	[Export(PropertyHint.Range, "-1, 256")]
	public int traction = -1; //Speed up rate
	[Export(PropertyHint.Range, "-1, 256")]
	public int friction = -1; //Slow down rate
	[Export(PropertyHint.Range, "-1, 256")]
	public int overspeedFriction = -1; //Slow down rate when going faster than speed
	[Export(PropertyHint.Range, "-1, 256")]
	public int turnaround = -1; //Skidding

	public MovementResource()
	{
		speed = 0;
		traction = 0;
		friction = 0;
	}

	//Figures out whether to speed up or slow down depending on the input
	public float Interpolate(float spd, float input)
	{
		float targetSpeed = speed * input;
		float delta = traction;

		if (Mathf.Abs(spd) > speed)
			delta = overspeedFriction;

		if (input == 0) //Deccelerate
		{
			targetSpeed = 0;
			delta = friction;
		}
		else if (!Mathf.IsZeroApprox(spd) && Mathf.Sign(targetSpeed) != Mathf.Sign(speed)) //Turnaround
		{
			delta = turnaround;
			if (clampTurnaround)
				targetSpeed = 0;
		}

		return Mathf.MoveToward(spd, targetSpeed, delta * PhysicsManager.physicsDelta);
	}

	public float GetSpeedRatio(float spd) => spd / speed;
	public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), -1f, 1f);
}