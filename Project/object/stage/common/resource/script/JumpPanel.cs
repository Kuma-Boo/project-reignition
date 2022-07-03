using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class JumpPanel : Launcher
	{
		[Export]
		public float travelHeight; //How high to travel
		[Export]
		public float endingHeight; //How high to end
		[Export]
		public float hOffset; //Skew amount

		public override Vector3 InterpolatePosition(float t)
		{
			t = GetInterpolationRatio(t);
			Vector3 middlePoint = this.Up() * travelHeight + this.Back() * travelDistance * .5f + this.Right() * hOffset * .5f;
			Vector3 position = (middlePoint * t).LinearInterpolate(GetEndPoint() * t, t);
			return GetStartingPoint() + position;
		}

		protected override Vector3 GetEndPoint() => this.Back() * travelDistance + this.Up() * endingHeight + this.Right() * hOffset;
	}
}
