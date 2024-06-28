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
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();
	[Signal]
	public delegate void StaggeredEventHandler();
	[Signal]
	public delegate void DamagedEventHandler();

	/// <summary> How fast to move. </summary>
	[ExportGroup("Settings")]
	[Export]
	private float maxSpeed;
	/// <summary> How fast to turn. </summary>
	[Export]
	private float turnSpeed;

	/// <summary> Allow object to move vertically? </summary>
	[Export]
	private bool isVerticalMovementDisabled;

	[Export]
	private float tiltAmount = 45;
	[Export]
	private bool rotateY;

	/// <summary> Maximum distance from the path allowed. </summary>
	[Export]
	private Vector2 bounds;
	[Export]
	private bool autosetBounds;
	private float HorizontalTurnSmoothing => bounds.X - COLLISION_SMOOTHING_DISTANCE;
	private float VerticalTurnSmoothing => bounds.Y - COLLISION_SMOOTHING_DISTANCE;

	/// <summary> How fast is the object currently moving? </summary>
	private float currentSpeed;
	/// <summary> How much is the object currently turning? </summary>
	private Vector2 currentTurnAmount;
	// Values for smooth damp
	private float speedVelocity;
	private Vector2 turnVelocity;
	private readonly float SPEED_SMOOTHING = .5f;
	private readonly float TURN_SMOOTHING = .25f;
	/// <summary> At what distance should inputs start being smoothed? </summary>
	private readonly float COLLISION_SMOOTHING_DISTANCE = 1f;

	/// <summary> Reference to the travel path. </summary>
	[ExportGroup("Components")]
	[Export]
	private Path3D path;
	/// <summary> Reference to the pathfollower. </summary>
	[Export]
	private PathFollow3D pathFollower;
	/// <summary> Reference to the root. </summary>
	[Export]
	private Node3D root;
	/// <summary> Reference to the player's position. </summary>
	[Export]
	private Node3D playerPosition;
	/// <summary> Reference to the animator. </summary>
	[Export]
	private AnimationPlayer animator;

	/// <summary> Is the carpet currently active? </summary>
	private bool isActive;
	private bool isRespawning;
	private float startingProgress;
	private SpawnData spawnData;

	private CharacterController Character => CharacterController.instance;

	public override void _Ready()
	{
		pathFollower.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition);
		startingProgress = pathFollower.Progress;
		spawnData = new SpawnData(GetParent(), Transform); // Create spawn data

		StageSettings.instance.ConnectRespawnSignal(this);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isActive)
			return;

		if (isRespawning)
			return;

		CalculateMovement();
		if (autosetBounds)
			bounds.X = Mathf.Inf;
		UpdateCollisions(1);
		UpdateCollisions(-1);
		ApplyMovement();
	}

	/// <summary> Handles player input. </summary>
	private void CalculateMovement()
	{
		Vector2 inputVector = Character.InputVector * turnSpeed;
		if (isVerticalMovementDisabled) // Ignore vertical input
			inputVector.Y = 0;

		// Smooth out edges
		bool isSmoothingHorizontal = Mathf.Abs(pathFollower.HOffset) > HorizontalTurnSmoothing &&
			Mathf.Sign(inputVector.X) != Mathf.Sign(pathFollower.HOffset);
		bool isSmoothingVertical = Mathf.Abs(pathFollower.VOffset) > VerticalTurnSmoothing &&
			Mathf.Sign(inputVector.Y) != Mathf.Sign(pathFollower.VOffset);

		if (isSmoothingHorizontal)
			inputVector.X *= 1.0f - ((Mathf.Abs(pathFollower.HOffset) - HorizontalTurnSmoothing) / (bounds.X - HorizontalTurnSmoothing));

		if (isSmoothingVertical)
			inputVector.Y *= 1.0f - ((Mathf.Abs(pathFollower.VOffset) - VerticalTurnSmoothing) / (bounds.Y - VerticalTurnSmoothing));

		currentSpeed = ExtensionMethods.SmoothDamp(currentSpeed, maxSpeed, ref speedVelocity, SPEED_SMOOTHING);
		currentTurnAmount = currentTurnAmount.SmoothDamp(inputVector, ref turnVelocity, TURN_SMOOTHING);
		Character.Animator.UpdateBalancing(inputVector.X / turnSpeed);
	}

	/// <summary> Check for walls. </summary>
	private void UpdateCollisions(int direction)
	{
		if (!autosetBounds)
			return;

		float pathTravellerCollisionSize = Character.CollisionRadius;
		float castDistance = pathTravellerCollisionSize + COLLISION_SMOOTHING_DISTANCE;
		if (Mathf.Sign(currentTurnAmount.X) == direction)
			castDistance += Mathf.Abs(currentTurnAmount.X * PhysicsManager.physicsDelta);

		Vector3 castVector = pathFollower.Left() * direction * castDistance;
		RaycastHit wallCast = this.CastRay(GlobalPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(GlobalPosition, castVector, wallCast ? Colors.Green : Colors.White);
		if (wallCast)
			bounds.X = Mathf.Abs(pathFollower.HOffset) + (wallCast.distance - pathTravellerCollisionSize);
	}

	private void ApplyMovement()
	{
		// Update path follower
		pathFollower.Progress += currentSpeed * PhysicsManager.physicsDelta;
		// Add offsets
		pathFollower.HOffset -= currentTurnAmount.X * PhysicsManager.physicsDelta;
		pathFollower.VOffset -= currentTurnAmount.Y * PhysicsManager.physicsDelta;
		// Clamp offsets
		pathFollower.HOffset = Mathf.Clamp(pathFollower.HOffset, -bounds.X, bounds.X);
		pathFollower.VOffset = Mathf.Clamp(pathFollower.VOffset, -bounds.Y, bounds.Y);

		// Update animations
		if (root != null) // Update visual rotations
		{
			root.Rotation = Vector3.Zero;
			root.RotateX(Mathf.DegToRad(tiltAmount) * (currentTurnAmount.Y / turnSpeed));
			root.RotateZ(Mathf.DegToRad(tiltAmount) * (currentTurnAmount.X / turnSpeed));
			root.RotateY(-Mathf.DegToRad(tiltAmount) * (currentTurnAmount.X / turnSpeed));
		}

		if (animator != null) // Update animation speeds
			animator.SpeedScale = 1.0f + (currentSpeed / maxSpeed * 1.5f);

		// Sync transforms
		GlobalTransform = pathFollower.GlobalTransform;
		Character.UpdateExternalControl();
	}

	public void Respawn()
	{
		Deactivate();

		spawnData.Respawn(this);
		pathFollower.Progress = startingProgress;
		pathFollower.HOffset = pathFollower.VOffset = 0;

		if (root != null) // Reset root transform
			root.Transform = Transform3D.Identity;

		if (animator != null) // Reset speed scale
			animator.SpeedScale = 1.0f;

		isRespawning = false;
	}

	/// <summary> Call this from a trigger. </summary>
	public void Activate()
	{
		isActive = true;

		Character.StartExternal(this, playerPosition, .1f);
		Character.Animator.StartBalancing(); // Carpet uses balancing animations
		Character.Animator.UpdateBalanceSpeed(1.0f);
		Character.Animator.ExternalAngle = 0; // Rotate to follow pathfollower
		EmitSignal(SignalName.Activated);
	}

	public void Deactivate()
	{
		EmitSignal(SignalName.Deactivated);
		isActive = false;

		// Reset damping values
		currentSpeed = speedVelocity = 0;
		currentTurnAmount = turnVelocity = Vector2.Zero;

		if (Character.ExternalParent == this)
			Character.StopExternal();
		Character.Animator.ResetState();
	}

	private void TakeDamage()
	{
		Deactivate();
		isRespawning = true;

		// Bump the player off
		LaunchSettings launchSettings = LaunchSettings.Create(Character.GlobalPosition, Character.GlobalPosition, 2);
		Character.StartLauncher(launchSettings);
		Character.Effect.StartSpinFX();
		Character.Animator.StartSpin(3.0f);
		Character.Animator.ResetState(0.1f);
	}

	private void Stagger()
	{
		currentSpeed = speedVelocity = 0;
		currentTurnAmount = turnVelocity = Vector2.Zero;

		// TODO Play stagger animation
		EmitSignal(SignalName.Staggered);
	}

	public void OnBodyEntered(PhysicsBody3D b)
	{
		if (b.IsInGroup("stagger"))
		{
			Stagger();
			return;
		}

		TakeDamage();
		EmitSignal(SignalName.Damaged);
	}
}
