using Godot;

namespace Project.Gameplay
{
	[Tool]
	public class Spring : Launcher
	{
		//For more control over curve
		[Export]
		public Curve gravityCurve;
		[Export]
		public int gravityAmount;

		public override Vector3 CalculatePosition(float t)
		{
			t = GetInterpolationRatio(t);
			Vector3 position = GetEndPoint() * t;
			if (gravityCurve != null)
				position += Vector3.Down * gravityAmount * gravityCurve.InterpolateBaked(t);
			return GetStartingPoint() + position;
		}
	}
}
