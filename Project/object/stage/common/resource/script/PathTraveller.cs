using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary>
/// Controls objects that carry the player along a path. These include:
/// The logs in Dinosaur Jungle,
/// the cannon in Pirate Storm,
/// and the carpet in Night Palace.
/// </summary>
public partial class PathTraveller : Node3D
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void DeactivatedEventHandler();
	[Signal] public delegate void StaggeredEventHandler();
	[Signal] public delegate void DamagedEventHandler();

	/// <summary> How fast to move. </summary>
	[ExportGroup("Settings")]
	[Export] private float maxSpeed;
	/// <summary> How fast to turn. </summary>
	[Export] private float turnSpeed;

	/// <summary> Allow object to move vertically? </summary>
	[Export] private bool isVerticalMovementDisabled;
	public bool IsVerticalMovementDisabled => isVerticalMovementDisabled;

	[Export] private float rotationAmount = 45;
	[Export] private float tiltRatio = 1.0f;

	/// <summary> Maximum distance from the path allowed. </summary>
	[Export] private Vector2 bounds;
	public Vector2 Bounds => bounds;
	public void SetHorizontalBounds(float newBound) => bounds.X = Mathf.Abs(newBound);
	[Export] private bool autosetBounds;
	public bool AutosetBounds => autosetBounds;

	/// <summary> Reference to the travel path. </summary>
	[ExportGroup("Components")]
	[Export] private Path3D path;
	/// <summary> Reference to the pathfollower. </summary>
	[Export] private PathFollow3D pathFollower;
	public PathFollow3D PathFollower => pathFollower;
	/// <summary> Reference to the root. </summary>
	[Export] private Node3D root;
	/// <summary> Optional node if excluding tilt is needed (boat ripples). </summary>
	[Export] private Node3D localRoot;
	/// <summary> Reference to the player's position. </summary>
	[Export] private Node3D playerPosition;
	public Node3D PlayerStandin => playerPosition;
	/// <summary> Reference to the animator. </summary>
	[Export] private AnimationPlayer animator;

	/// <summary> How fast is the object currently moving? </summary>
	public float CurrentSpeed { get; protected set; }
	/// <summary> How much is the object currently turning? </summary>
	private Vector2 currentTurnAmount;
	// Values for smooth damp
	private float speedVelocity;
	private Vector2 turnVelocity;

	private float startingProgress;
	private Vector3 startingOffset;
	private SpawnData spawnData;
	private float HorizontalTurnSmoothing => Bounds.X - CollisionSmoothingDistance;
	private float VerticalTurnSmoothing => Bounds.Y - CollisionSmoothingDistance;
	/// <summary> At what distance should inputs start being smoothed? </summary>
	private readonly float CollisionSmoothingDistance = 1f;
	private readonly float SpeedSmoothing = .5f;
	private readonly float TurnSmoothing = .25f;

	private PlayerController Player => StageSettings.Player;

	public override void _Ready()
	{
		if (pathFollower == null) // Create a pathfollower if needed
		{
			pathFollower = new()
			{
				UseModelFront = true,
				Loop = false,
				CubicInterp = false,
			};

			path.AddChild(pathFollower);
		}

		pathFollower.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition);
		startingProgress = pathFollower.Progress;
		startingOffset = pathFollower.GlobalBasis.Inverse() * (GlobalPosition - pathFollower.GlobalPosition);
		spawnData = new(GetParent(), Transform); // Create spawn data

		StageSettings.Instance.Respawned += Respawn;
		Respawn();
	}

	private void Respawn()
	{
		Deactivate();

		if (animator.HasAnimation("respawn"))
			animator.Play("respawn");

		spawnData.Respawn(this);
		pathFollower.Progress = startingProgress;
		pathFollower.HOffset = startingOffset.X;
		pathFollower.VOffset = startingOffset.Y;

		if (root != null) // Reset root basis
			root.Basis = Basis.Identity;

		if (localRoot != null)
			localRoot.Basis = Basis.Identity;

		if (animator != null) // Reset speed scale
			animator.SpeedScale = 1.0f;
	}

	public void Despawn()
	{
		if (animator.HasAnimation("despawn"))
			animator.Play("despawn");
	}


	public void ProcessPathTraveller()
	{
		CalculateMovement();
		if (AutosetBounds)
		{
			// Reset travel bounds on both sides, then update each side individually
			SetHorizontalBounds(Mathf.Inf);
			UpdateCollisions(1);
			UpdateCollisions(-1);
		}

		ApplyMovement();
		UpdateAnimation();
	}

	/// <summary> Check for walls. </summary>
	private void UpdateCollisions(int direction)
	{
		float pathTravellerCollisionSize = Player.CollisionSize.X;
		float castDistance = pathTravellerCollisionSize + CollisionSmoothingDistance;
		if (Mathf.Sign(currentTurnAmount.X) == direction)
			castDistance += Mathf.Abs(currentTurnAmount.X * PhysicsManager.physicsDelta);

		Vector3 castVector = Player.Left() * direction * castDistance;
		RaycastHit wallCast = Player.CastRay(Player.GlobalPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.GlobalPosition, castVector, wallCast ? Colors.Green : Colors.White);
		if (wallCast)
			SetHorizontalBounds(Mathf.Abs(PathFollower.HOffset) + (wallCast.distance - pathTravellerCollisionSize));
	}

	/// <summary> Handles player input. </summary>
	private void CalculateMovement()
	{
		Vector2 inputVector = Player.Controller.InputAxis * turnSpeed;
		if (IsVerticalMovementDisabled) // Ignore vertical input
			inputVector.Y = 0;

		// Smooth out edges
		bool isSmoothingHorizontal = Mathf.Abs(PathFollower.HOffset) > HorizontalTurnSmoothing &&
			Mathf.Sign(inputVector.X) != Mathf.Sign(PathFollower.HOffset);
		bool isSmoothingVertical = Mathf.Abs(PathFollower.VOffset) > VerticalTurnSmoothing &&
			Mathf.Sign(inputVector.Y) != Mathf.Sign(PathFollower.VOffset);

		if (isSmoothingHorizontal)
			inputVector.X *= 1.0f - ((Mathf.Abs(PathFollower.HOffset) - HorizontalTurnSmoothing) / (Bounds.X - HorizontalTurnSmoothing));

		if (isSmoothingVertical)
			inputVector.Y *= 1.0f - ((Mathf.Abs(PathFollower.VOffset) - VerticalTurnSmoothing) / (Bounds.Y - VerticalTurnSmoothing));

		currentTurnAmount = currentTurnAmount.SmoothDamp(inputVector, ref turnVelocity, TurnSmoothing);
		CurrentSpeed = ExtensionMethods.SmoothDamp(CurrentSpeed, maxSpeed, ref speedVelocity, SpeedSmoothing);
	}

	/// <summary> Override this if you want specific control over the PathFollower's speed. </summary>
	protected virtual float CalculateMaxSpeed() => maxSpeed;

	private void UpdateAnimation()
	{
		// Update animations
		if (root != null) // Update visual rotations
		{
			float turnAmount = (currentTurnAmount.X / turnSpeed) - (Player.PathFollower.DeltaAngle * 5.0f);
			float tiltAmount = Mathf.DegToRad(rotationAmount) * tiltRatio * turnAmount;
			root.Rotation = Vector3.Zero;

			root.RotateX(Mathf.DegToRad(rotationAmount) * (currentTurnAmount.Y / turnSpeed));
			if (localRoot != null)
				localRoot.Rotation = new(0, 0, tiltAmount);
			else
				root.RotateZ(tiltAmount);
			root.RotateY(-Mathf.DegToRad(rotationAmount) * turnAmount);
		}

		if (animator != null) // Update animation speeds
			animator.SpeedScale = 1.0f + (CurrentSpeed / maxSpeed * 1.5f);
	}

	private void ApplyMovement()
	{
		// Update path follower
		PathFollower.Progress += CurrentSpeed * PhysicsManager.physicsDelta;
		// Add offsets
		PathFollower.HOffset -= currentTurnAmount.X * PhysicsManager.physicsDelta;
		PathFollower.VOffset -= currentTurnAmount.Y * PhysicsManager.physicsDelta;
		// Clamp offsets
		PathFollower.HOffset = Mathf.Clamp(PathFollower.HOffset, -Bounds.X, Bounds.X);
		PathFollower.VOffset = Mathf.Clamp(PathFollower.VOffset, -Bounds.Y, Bounds.Y);

		// Sync transforms
		GlobalTransform = PathFollower.GlobalTransform;
	}

	/// <summary> Kills all speed and turning. </summary>
	public void StopMovement()
	{
		CurrentSpeed = speedVelocity = 0;
		currentTurnAmount = turnVelocity = Vector2.Zero;
	}

	/// <summary> Call this from a trigger. </summary>
	public void Activate()
	{
		if (animator.HasAnimation("activate"))
		{
			animator.Play("activate");
			animator.Advance(0.0);
		}

		Player.StartPathTraveller(this);
		EmitSignal(SignalName.Activated);
	}

	public void Deactivate()
	{
		EmitSignal(SignalName.Deactivated);

		if (animator.HasAnimation("deactivate"))
		{
			animator.Play("deactivate");
			animator.Advance(0.0);
		}
	}

	public void OnBodyEntered(PhysicsBody3D b)
	{
		if (b.IsInGroup("stagger"))
		{
			EmitSignal(SignalName.Staggered);
			return;
		}

		EmitSignal(SignalName.Damaged);
	}
}
