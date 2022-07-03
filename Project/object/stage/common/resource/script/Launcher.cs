using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class Launcher : Area
	{
		[Export]
		public float travelDistance; //How far to travel
		[Export(PropertyHint.Range, ".2f, 5f")]
		public float travelTime = .5f; //How long to travel
		[Export]
		public float launchOffset = .25f; //Vertical position offset
		[Export]
		public Curve travelCurve; //Optional curve to control speed

		[Export]
		public int characterRecenterSpeed; //How fast to center the character

		[Export(PropertyHint.Range, "0, 1")]
		public float momentumMultiplier = 1f; //How much momentum to keep when the launch ends?

		public bool IsCharacterCentered { get; private set; }

		private float launcherTime; //Current launcher time, used for position calculations

		protected CharacterController Character => CharacterController.instance;

		public virtual void Activate(Area a)
		{
			launcherTime = 0; //Reset launcher time

			IsCharacterCentered = characterRecenterSpeed == 0;
			Character.StartLauncher(this);
		}

		public bool IsLaunchFinished => launcherTime + PhysicsManager.physicsDelta >= travelTime;

		public Vector3 CenterCharacter()
		{
			Vector3 pos = Character.GlobalTransform.origin.MoveToward(GetStartingPoint(), characterRecenterSpeed * PhysicsManager.physicsDelta);
			IsCharacterCentered = pos.IsEqualApprox(GetStartingPoint());
			return pos;
		}

		public virtual Vector3 InterpolatePosition(float t)
		{
			t = GetInterpolationRatio(t);
			Vector3 position = GetEndPoint() * t;
			return GetStartingPoint() + position;
		}

		public Vector3 CalculateMovementDelta()
		{
			Vector3 startingPosition = InterpolatePosition(launcherTime);
			launcherTime += PhysicsManager.physicsDelta;
			Vector3 targetPosition = InterpolatePosition(launcherTime);
			return (targetPosition - startingPosition) / PhysicsManager.physicsDelta;
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
