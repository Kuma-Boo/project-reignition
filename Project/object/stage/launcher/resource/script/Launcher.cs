using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Launches the player. Use <see cref="CreateLaunchData(Vector3, Vector3, float, bool)"/> to bypass needing a Launcher node.
	/// </summary>
	[Tool]
	public class Launcher : Area //Similar to Character.JumpTo(), but jumps between static points w/ custom sfx support
	{
		[Export]
		public NodePath sfxPlayer; //Height at the beginning of the arc
		private AudioStreamPlayer _sfxPlayer; //Height at the beginning of the arc

		[Signal]
		public delegate void Activated();

		[Export]
		public float startingHeight; //Height at the beginning of the arc
		[Export]
		public float middleHeight; //Height at the highest point of the arc
		[Export]
		public float finalHeight; //Height at the end of the arc
		[Export]
		public float distance; //How far to travel

		public Vector3 travelDirection; //Direction the player should face when being launched

		public LaunchData GetLaunchData()
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
		public bool allowJumpDashing;

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

			LaunchData launchData = GetLaunchData();
			Character.StartLauncher(launchData, this, true);

			EmitSignal(nameof(Activated));
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
	}

	public struct LaunchData
	{
		public Vector3 launchDirection;
		public Vector3 startPosition;

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
		private float GRAVITY => -CharacterController.GRAVITY; //Use the same gravity as the character controller

		public Vector3 InterpolatePosition(float t)
		{
			Vector3 displacement = InitialVelocity * t + Vector3.Up * GRAVITY * t * t / 2f;
			return startPosition + displacement;
		}

		public void Calculate()
		{
			if (middleHeight < finalHeight || middleHeight < startingHeight) //Ignore middle
				middleHeight = Mathf.Max(startingHeight, finalHeight);

			FirstHalfTime = Mathf.Sqrt((-2 * middleHeight) / GRAVITY);
			SecondHalfTime = Mathf.Sqrt((-2 * (middleHeight - finalHeight)) / GRAVITY);
			TotalTravelTime = FirstHalfTime + SecondHalfTime;

			InitialHorizontalVelocity = distance / TotalTravelTime;
			InitialVerticalVelocity = Mathf.Sqrt(-2 * GRAVITY * (middleHeight - startingHeight));
			FinalVerticalVelocity = GRAVITY * SecondHalfTime;

			InitialVelocity = launchDirection.Flatten().Normalized() * InitialHorizontalVelocity + Vector3.Up * InitialVerticalVelocity;
		}

		public static LaunchData Create(Vector3 s, Vector3 e, float h, bool relativeToEnd = false)
		{
			Vector3 delta = e - s;
			LaunchData data = new LaunchData()
			{
				startPosition = s,
				launchDirection = delta.Normalized(),

				distance = delta.RemoveVertical().Length(),
				startingHeight = 0f,
				middleHeight = h,
				finalHeight = delta.y,
			};

			if (relativeToEnd)
				data.middleHeight += delta.y;

			data.Calculate();
			return data;
		}

	}
}
