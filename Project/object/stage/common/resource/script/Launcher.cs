using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Launches the player. Use <see cref="CreateLaunchSettings(Vector3, Vector3, float, bool)"/> to bypass needing a Launcher node.
	/// </summary>
	[Tool]
	public partial class Launcher : Area3D //Jumps between static points w/ custom sfx support
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

		[Export]
		public bool allowJumpDashing;

		[Export]
		public LaunchDirection launchDirection;
		public enum LaunchDirection
		{
			Forward,
			Up,
		}
		public virtual Vector3 GetLaunchDirection()
		{
			if (launchDirection == LaunchDirection.Forward)
				return this.Forward();

			return this.Up();
		}

		public Vector3 StartingPoint => GlobalPosition + Vector3.Up * startingHeight;

		public LaunchSettings GetLaunchSettings()
		{
			Vector3 startPosition = GlobalPosition + Vector3.Up * startingHeight;
			Vector3 endPosition = startPosition + GetLaunchDirection() * distance + Vector3.Up * finalHeight;

			LaunchSettings settings = LaunchSettings.Create(startPosition, endPosition, middleHeight);
			settings.UseAutoAlign = true;

			return settings;
		}

		public virtual void Activate(Area3D a)
		{
			if (sfxPlayer != null)
				sfxPlayer.Play();

			IsCharacterCentered = recenterSpeed == 0;
			LaunchSettings LaunchSettings = GetLaunchSettings();
			Character.StartLauncher(LaunchSettings, this);

			if (LaunchSettings.InitialVelocity.AngleTo(Vector3.Up) < Mathf.Pi * .1f)
				Character.Animator.Jump();
			else
				Character.Animator.LaunchAnimation();

			EmitSignal(SignalName.Activated);
		}

		[Export]
		private int recenterSpeed; //How fast to recenter the character

		public bool IsCharacterCentered { get; private set; }
		protected CharacterController Character => CharacterController.instance;

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
}

namespace Project.Gameplay
{
	public struct LaunchSettings
	{
		//Character settings
		/// <summary> Play jump FX? </summary>
		public bool IsJump { get; set; }
		/// <summary> Automatically align player's orientation? </summary>
		public bool UseAutoAlign { get; set; }
		/// <summary> Allow the player to jumpdash after launch is completed? </summary>
		public bool AllowJumpDash { get; set; }

		//Physics settings
		public Vector3 launchDirection;
		public Vector3 endPosition;
		public Vector3 startPosition;

		public float distance;
		public float middleHeight;
		public float finalHeight;

		public Vector3 InitialVelocity { get; private set; }
		public float HorizontalVelocity { get; private set; } //Horizontal velocity remains constant throughout the entire launch
		public float InitialVerticalVelocity { get; private set; }
		public float FinalVerticalVelocity { get; private set; }

		public float FirstHalfTime { get; private set; }
		public float SecondHalfTime { get; private set; }
		public float TotalTravelTime { get; private set; }

		/// <summary> Was this launch settings initialized? </summary>
		public bool IsInitialized { get; private set; }
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

		public void Initialize()
		{
			if (middleHeight <= finalHeight) //Ignore middle
				middleHeight = finalHeight;

			FirstHalfTime = Mathf.Sqrt((-2 * middleHeight) / GRAVITY);
			SecondHalfTime = Mathf.Sqrt((-2 * (middleHeight - finalHeight)) / GRAVITY);
			TotalTravelTime = FirstHalfTime + SecondHalfTime;

			HorizontalVelocity = distance / TotalTravelTime;
			InitialVerticalVelocity = Mathf.Sqrt(-2 * GRAVITY * middleHeight);
			FinalVerticalVelocity = GRAVITY * SecondHalfTime;

			InitialVelocity = launchDirection.RemoveVertical().Normalized() * HorizontalVelocity + Vector3.Up * InitialVerticalVelocity;
			IsInitialized = true;
		}

		/// <summary>
		/// Creates new launch data and calculates it. Modify the return value for extra control.
		/// s -> starting position, e -> ending position, h -> height, relativeToEnd -> Is the height relative to the end, or start?
		/// </summary>
		public static LaunchSettings Create(Vector3 s, Vector3 e, float h, bool relativeToEnd = false)
		{
			Vector3 delta = e - s;
			LaunchSettings data = new LaunchSettings()
			{
				startPosition = s,
				endPosition = e,
				launchDirection = delta.Normalized(),

				distance = delta.Flatten().Length(),
				middleHeight = h,
				finalHeight = delta.Y,
			};

			if (relativeToEnd)
				data.middleHeight += delta.Y;

			data.Initialize();
			return data;
		}
	}
}