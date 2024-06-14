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


	[ExportGroup("Settings")]
	[Export]
	/// <summary> How fast to move. </summary>
	private float maxSpeed;
	[Export]
	/// <summary> How fast to turn. </summary>
	private float turnSpeed;

	[Export]
	/// <summary> Allow object to move vertically? </summary>
	private bool isVerticalMovementDisabled;

	[Export]
	private float tiltAmount = 45;

	[Export]
	/// <summary> Maximum distance from the path allowed. </summary>
	private Vector2 bounds;
	private float HorizontalTurnSmoothing => bounds.X * TURN_SMOOTHING_RATIO;
	private float VerticalTurnSmoothing => bounds.Y * TURN_SMOOTHING_RATIO;
	/// <summary> At what ratio should inputs start being smoothed? </summary>
	private readonly float TURN_SMOOTHING_RATIO = .8f;


	/// <summary> How fast is the object currently moving? </summary>
	private float speedDelta;
	private Vector2 turnDelta;
	// Values for smooth damp
	private float speedVelocity;
	private Vector2 turnVelocity;
	private readonly float SPEED_SMOOTHING = .5f;
	private readonly float TURN_SMOOTHING = .25f;


	[ExportGroup("Components")]
	[Export]
	/// <summary> Reference to the travel path. </summary>
	private Path3D path;
	[Export]
	/// <summary> Reference to the pathfollower. </summary>
	private PathFollow3D pathFollower;
	[Export]
	/// <summary> Reference to the root. </summary>
	private Node3D root;
	[Export]
	/// <summary> Reference to the player's position. </summary>
	private Node3D playerPosition;
	[Export]
	/// <summary> Reference to the animator. </summary>
	private AnimationPlayer animator;


	/// <summary> Is the carpet currently active? </summary>
	private bool isActive;
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
		if (!isActive) return;

		ProcessMovement();
	}

	/// <summary> Handles object's movement. </summary>
	private void ProcessMovement()
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

		speedDelta = ExtensionMethods.SmoothDamp(speedDelta, maxSpeed, ref speedVelocity, SPEED_SMOOTHING);
		turnDelta = turnDelta.SmoothDamp(inputVector, ref turnVelocity, TURN_SMOOTHING);

		// Update path follower
		pathFollower.Progress += speedDelta * PhysicsManager.physicsDelta;

		// Add offsets
		pathFollower.HOffset -= turnDelta.X * PhysicsManager.physicsDelta;
		pathFollower.VOffset -= turnDelta.Y * PhysicsManager.physicsDelta;
		// Clamp offsets
		pathFollower.HOffset = Mathf.Clamp(pathFollower.HOffset, -bounds.X, bounds.X);
		pathFollower.VOffset = Mathf.Clamp(pathFollower.VOffset, -bounds.Y, bounds.Y);

		// Update animations
		if (root != null) // Update visual rotations
		{
			root.Rotation = Vector3.Zero;
			root.RotateX(Mathf.DegToRad(tiltAmount) * (turnDelta.Y / turnSpeed));
			root.RotateZ(Mathf.DegToRad(tiltAmount) * (turnDelta.X / turnSpeed));
		}

		if (animator != null) // Update animation speeds
			animator.SpeedScale = 1.0f + (speedDelta / maxSpeed * 1.5f);

		Character.Animator.UpdateBalancing(inputVector.X / turnSpeed);


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
		speedDelta = speedVelocity = 0;
		turnDelta = turnVelocity = Vector2.Zero;

		if (Character.ExternalParent == this)
			Character.StopExternal();
		Character.Animator.ResetState();
	}
}
