using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class Launcher : StageObject
	{
		[Export]
		public float travelDistance; //How far to travel
		[Export(PropertyHint.Range, ".2f, 5f")]
		public float travelTime = .5f; //How long to travel
		[Export]
		public float launchOffset = .25f; //Vertical position offset
		[Export]
		public Curve travelCurve; //Optional curve to control speed

		[Export(PropertyHint.Range, "0, 1")]
		public float momentumMultiplier = 1f; //How much momentum to keep when the launch ends?
		[Export]
		public bool refreshJumpDash; //Refresh the jumpdash post launch?

		[Export]
		public float centerCharacterSpeed; //How fase to center the character
		public bool IsCharacterCentered { get; private set; }

		public override bool IsRespawnable() => false; //Launchers normally don't need to be respawned

		public override void OnEnter()
		{
			IsCharacterCentered = centerCharacterSpeed == 0;
			Character.StartLauncher(this);

		}
		public bool IsLaunchFinished(float t) => t + PhysicsManager.physicsDelta >= travelTime;

		public Vector3 CenterCharacter()
		{
			Vector3 pos = Character.GlobalTransform.origin.MoveToward(GetStartingPoint(), centerCharacterSpeed * PhysicsManager.physicsDelta);
			IsCharacterCentered = pos.IsEqualApprox(GetStartingPoint());
			return pos;
		}

		public virtual Vector3 CalculatePosition(float t)
		{
			t = GetInterpolationRatio(t);
			Vector3 position = GetEndPoint() * t;
			return GetStartingPoint() + position;
		}

		protected float GetInterpolationRatio(float t)
		{
			t = Mathf.Clamp(t / travelTime, 0, 1f);
			if (travelCurve != null)
				t = travelCurve.InterpolateBaked(t);
			return t;
		}

		protected virtual Vector3 GetStartingPoint() => GlobalTransform.origin + this.Up() * launchOffset;
		protected virtual Vector3 GetEndPoint() => this.Up() * travelDistance;
	}
}
