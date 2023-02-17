using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Launches the player. Use <see cref="CreateLaunchData(Vector3, Vector3, float, bool)"/> to bypass needing a Launcher node.
	/// </summary>
	[Tool]
	public partial class Launcher : Area3D //Similar to Character.JumpTo(), but jumps between static points w/ custom sfx support
	{
		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler(); //Called after character finishes processing this launcher.

		[Export]
		private float startingHeight; //Height at the beginning of the arc
		[Export]
		private float middleHeight; //Height at the highest point of the arc
		[Export]
		private float finalHeight; //Height at the end of the arc
		[Export]
		private float distance; //How far to travel

		private Vector3 travelDirection; //Direction the player should face when being launched

		public LaunchData GetLaunchData()
		{
			LaunchData data = new LaunchData
			{
				launchDirection = GetLaunchDirection(),

				startPosition = GlobalPosition + Vector3.Up * startingHeight,
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
			if (launchDirection == LaunchDirection.Forward)
				return this.Forward();

			return this.Up();
		}

		public Vector3 StartingPoint => GlobalPosition + Vector3.Up * startingHeight;

		public virtual void Activate(Area3D a)
		{
			if (sfxPlayer != null)
				sfxPlayer.Play();

			IsCharacterCentered = recenterSpeed == 0;
			LaunchData launchData = GetLaunchData();
			Character.StartLauncher(launchData, this, true);

			if (launchData.InitialVelocity.AngleTo(Vector3.Up) < Mathf.Pi * .1f)
				Character.Animator.Jump();
			else
				Character.Animator.LaunchAnimation();

			EmitSignal(SignalName.Activated);
		}

		[Export]
		private int recenterSpeed; //How fast to recenter the character

		public bool IsCharacterCentered { get; private set; }
		private CharacterController Character => CharacterController.instance;

		public Vector3 RecenterCharacter()
		{
			Vector3 pos = Character.GlobalPosition.MoveToward(StartingPoint, recenterSpeed * PhysicsManager.physicsDelta);
			IsCharacterCentered = pos.IsEqualApprox(StartingPoint);
			return pos;
		}

		public void Deactivate() => EmitSignal(SignalName.Deactivated);

		[Export]
		private AudioStreamPlayer3D sfxPlayer; //Optional SFX field
	}

	public struct LaunchData
	{
		//Physics data
		public Vector3 launchDirection;
		public Vector3 endPosition;
		public Vector3 startPosition;

		public float distance;
		public float startingHeight;
		public float middleHeight;
		public float finalHeight;

		public Vector3 InitialVelocity { get; private set; }
		public float HorizontalVelocity { get; private set; } //Horizontal velocity remains constant throughout the entire launch
		public float InitialVerticalVelocity { get; private set; }
		public float FinalVerticalVelocity { get; private set; }

		public float FirstHalfTime { get; private set; }
		public float SecondHalfTime { get; private set; }
		public float TotalTravelTime { get; private set; }

		public bool IsLauncherFinished(float t) => t + PhysicsManager.physicsDelta >= TotalTravelTime;
		private float GRAVITY => -Runtime.GRAVITY; //Use the same gravity as the character controller

		/// <summary>
		/// Get the current position, using t -> [0 <-> 1]. Lerps when launch data is invalid.
		/// </summary>
		public Vector3 InterpolatePositionRatio(float t)
		{
			if (Mathf.IsZeroApprox(TotalTravelTime) && !Mathf.IsZeroApprox(distance)) //Invalid launch data, use a lerp
				return startPosition.Lerp(endPosition, t);

			return InterpolatePositionTime(t * TotalTravelTime);
		}
		/// <summary>
		/// Get the current position, using t -> current time, in seconds. Relatively unsafe due to errors during invalid launch paths.
		/// </summary>
		public Vector3 InterpolatePositionTime(float t)
		{
			Vector3 displacement = InitialVelocity * t + Vector3.Up * GRAVITY * t * t / 2f;
			return startPosition + displacement;
		}

		public void Calculate()
		{
			if (middleHeight <= finalHeight || middleHeight < startingHeight) //Ignore middle
				middleHeight = Mathf.Max(startingHeight, finalHeight);

			FirstHalfTime = Mathf.Sqrt((-2 * middleHeight) / GRAVITY);
			SecondHalfTime = Mathf.Sqrt((-2 * (middleHeight - finalHeight)) / GRAVITY);
			TotalTravelTime = FirstHalfTime + SecondHalfTime;

			HorizontalVelocity = distance / TotalTravelTime;
			InitialVerticalVelocity = Mathf.Sqrt(-2 * GRAVITY * (middleHeight - startingHeight));
			FinalVerticalVelocity = GRAVITY * SecondHalfTime;

			InitialVelocity = launchDirection.RemoveVertical().Normalized() * HorizontalVelocity + Vector3.Up * InitialVerticalVelocity;
		}

		//Control Data
		/// <summary> Allow the player to jumpdash after launch is completed? </summary>
		public bool canJumpDash;

		/// <summary>
		/// Creates new launch data.
		/// s -> starting position, e -> ending position, h -> height, relativeToEnd -> Is the height relative to the end, or start?
		/// </summary>
		public static LaunchData Create(Vector3 s, Vector3 e, float h, bool relativeToEnd = false)
		{
			Vector3 delta = e - s;
			LaunchData data = new LaunchData()
			{
				startPosition = s,
				endPosition = e,
				launchDirection = delta.Normalized(),

				distance = delta.Flatten().Length(),
				startingHeight = 0f,
				middleHeight = h,
				finalHeight = delta.Y,
			};

			if (relativeToEnd)
				data.middleHeight += delta.Y;

			data.Calculate();
			return data;
		}
	}
}
