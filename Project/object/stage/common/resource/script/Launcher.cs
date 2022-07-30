using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class Launcher : Area //Similar to Character.JumpTo(), but jumps between static points w/ custom sfx support
	{
		[Export]
		public NodePath sfxPlayer; //Height at the beginning of the arc
		private AudioStreamPlayer _sfxPlayer; //Height at the beginning of the arc

		[Export]
		public float startingHeight; //Height at the beginning of the arc
		[Export]
		public float middleHeight; //Height at the highest point of the arc
		[Export]
		public float finalHeight; //Height at the end of the arc
		[Export]
		public float distance; //How far to travel

		public LaunchData GetData()
		{
			LaunchData data = new LaunchData
			{
				//gravity = GRAVITY,
				launchDirection = GetLaunchDirection(),

				startPosition = GlobalTranslation + Vector3.Up * startingHeight,
				startingHeight = startingHeight,
				middleHeight = middleHeight,
				finalHeight = finalHeight,

				distance = distance,
			};
			
			data.Calculate();
			return data;
		}

		[Export]
		public LaunchDirection launchDirection;
		public enum LaunchDirection
		{
			Forward,
			Up,
		}
		public Vector3 GetLaunchDirection()
		{
			if(launchDirection == LaunchDirection.Forward)
				return this.Forward();

			return this.Up();
		}

		public Vector3 StartingPoint => GlobalTranslation + Vector3.Up * startingHeight;
		public const float GRAVITY = -18.0f;

		public override void _Ready()
		{
			if (sfxPlayer != null && !sfxPlayer.IsEmpty())
				_sfxPlayer = GetNode<AudioStreamPlayer>(sfxPlayer);
		}

		public virtual void Activate(Area a)
		{
			if(_sfxPlayer != null)
				_sfxPlayer.Play();

			IsCharacterCentered = recenterSpeed == 0;
			Character.StartLauncher(GetData(), this);
		}

		[Export]
		public int recenterSpeed; //How fast to recenter the character
		public bool IsCharacterCentered { get; private set; }
		private CharacterController Character => CharacterController.instance;

		public Vector3 RecenterCharacter()
		{
			Vector3 pos = Character.GlobalTranslation.MoveToward(StartingPoint, recenterSpeed * PhysicsManager.physicsDelta);
			IsCharacterCentered = pos.IsEqualApprox(StartingPoint);
			return pos;
		}

		public struct LaunchData
		{
			public Vector3 launchDirection;
			public Vector3 startPosition;
			public const float gravity = -18.0f;

			public float distance;
			public float startingHeight;
			public float middleHeight;
			public float finalHeight;

			public Vector3 InitialVelocity { get; private set; }
			public float InitialHorizontalVelocity { get; private set; }
			public float InitialVerticalVelocity { get; private set; }
			public float FinalVerticalVelocity { get; private set; }

			public float FirstHalfTime { get; private set; }
			public float SecondHalfTime { get; private set; }
			public float TotalTravelTime { get; private set; }

			public bool IsLauncherFinished(float t) => t + PhysicsManager.physicsDelta >= TotalTravelTime;

			public Vector3 InterpolatePosition(float t)
			{
				Vector3 displacement = InitialVelocity * t + Vector3.Up * gravity * t * t / 2f;
				return startPosition + displacement;
			}

			public void Calculate()
			{
				FirstHalfTime = Mathf.Sqrt((-2 * middleHeight) / gravity);
				SecondHalfTime = Mathf.Sqrt((-2 * (middleHeight - finalHeight)) / gravity);
				TotalTravelTime = FirstHalfTime + SecondHalfTime;

				InitialHorizontalVelocity = distance / TotalTravelTime;
				InitialVerticalVelocity = Mathf.Sqrt(-2 * gravity * (middleHeight - startingHeight));
				FinalVerticalVelocity = gravity * SecondHalfTime;

				InitialVelocity = launchDirection.Flatten().Normalized() * InitialHorizontalVelocity + Vector3.Up * InitialVerticalVelocity;
			}
		}
	}
}
