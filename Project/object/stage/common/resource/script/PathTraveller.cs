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
	[Signal] public delegate void AdvancedEventHandler();
	[Signal] public delegate void StaggeredEventHandler();
	[Signal] public delegate void DamagedEventHandler();

	/// <summary> How fast to move. </summary>
	[ExportGroup("Settings")]
	[Export] protected float MaxSpeed { get; private set; }
	/// <summary> How fast to turn. </summary>
	[Export] protected float TurnSpeed { get; private set; }
	[Export] private float turnSmoothing = 20f;

	/// <summary> Allow object to move vertically? </summary>
	[Export] private bool isVerticalMovementDisabled;
	public bool IsVerticalMovementDisabled => isVerticalMovementDisabled;

	/// <summary> Should this PathTraveller automatically respawn the player after taking damage? </summary>
	[Export] public bool AutoDefeat { get; private set; }
	[Export] private float rotationAmount = 45;
	[Export] private float tiltRatio = 1.0f;
	[Export] private bool disableStepButtons;

	/// <summary> Maximum distance from the path allowed. </summary>
	[Export] private Vector2 bounds;
	public Vector2 Bounds => bounds;
	public void SetHorizontalBounds(float newBound) => bounds.X = Mathf.Abs(newBound);
	[Export] private bool autosetBounds;
	public bool AutosetBounds => autosetBounds;

	/// <summary> Reference to the travel path. </summary>
	[ExportGroup("Components")]
	[Export] protected Path3D path;
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
	[Export] protected AnimationPlayer animator;

	/// <summary> Should the player play the crouching animation? </summary>
	public bool IsCrouching { get; protected set; }

	/// <summary> How fast is the object currently moving? </summary>
	public float CurrentSpeed { get; protected set; }
	/// <summary> How much is the object currently turning? </summary>
	public Vector2 CurrentTurnAmount { get; protected set; }
	// Values for smooth damp
	protected float speedVelocity;
	protected Vector2 turnVelocity;

	private float startingProgress;
	private Vector3 startingOffset;
	private SpawnData spawnData;
	private float HorizontalTurnSmoothing => Bounds.X - CollisionSmoothingDistance;
	private float VerticalTurnSmoothing => Bounds.Y - CollisionSmoothingDistance;
	/// <summary> At what distance should inputs start being smoothed? </summary>
	private readonly float CollisionSmoothingDistance = 1f;
	private readonly float SpeedSmoothing = 25f;

	protected PlayerController Player => StageSettings.Player;

	public override void _Ready() => SetUp();

	protected virtual void SetUp()
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

	protected virtual void Respawn()
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

	/// <summary>
	/// Advances the pathfollower by a certain amount.
	/// Used in Pirate Storm Act 1 to make the cannonball ending faster.
	/// </summary>
	public void Advance(float amount)
	{
		pathFollower.Progress += amount;
		GlobalPosition = PathFollower.GlobalPosition;
		GlobalRotation = PathFollower.GlobalRotation;
		EmitSignal(SignalName.Advanced);
	}

	public virtual void ProcessPathTraveller()
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
		if (Mathf.Sign(CurrentTurnAmount.X) == direction)
			castDistance += Mathf.Abs(CurrentTurnAmount.X * PhysicsManager.physicsDelta);

		Vector3 castVector = this.Left() * direction * castDistance;
		RaycastHit wallCast = Player.CastRay(Player.GlobalPosition, castVector, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(Player.GlobalPosition, castVector, wallCast ? Colors.Green : Colors.White);
		if (wallCast)
			SetHorizontalBounds(Mathf.Abs(PathFollower.HOffset) + (wallCast.distance - pathTravellerCollisionSize));
	}

	/// <summary> Handles player input. </summary>
	private void CalculateMovement()
	{
		Vector2 inputVector = Player.Controller.InputAxis;
		if (IsVerticalMovementDisabled) // Ignore vertical input
			inputVector.Y = 0;

		if (Player.Skills.IsSpeedBreakActive) // Reduce turning strength during speedbreak
			inputVector *= 0.2f;

		if (!disableStepButtons)
		{
			// Add step input influence
			float turnInfluence = Player.Controller.StepAxis;
			inputVector.X -= turnInfluence * 0.5f;
		}

		inputVector *= GetCurrentTurnSpeed;

		// Smooth out edges
		bool isSmoothingHorizontal = Mathf.Abs(PathFollower.HOffset) > HorizontalTurnSmoothing &&
			Mathf.Sign(inputVector.X) != Mathf.Sign(PathFollower.HOffset);
		bool isSmoothingVertical = Mathf.Abs(PathFollower.VOffset) > VerticalTurnSmoothing &&
			Mathf.Sign(inputVector.Y) != Mathf.Sign(PathFollower.VOffset);

		if (isSmoothingHorizontal)
			inputVector.X *= 1.0f - ((Mathf.Abs(PathFollower.HOffset) - HorizontalTurnSmoothing) / (Bounds.X - HorizontalTurnSmoothing));

		if (isSmoothingVertical)
			inputVector.Y *= 1.0f - ((Mathf.Abs(PathFollower.VOffset) - VerticalTurnSmoothing) / (Bounds.Y - VerticalTurnSmoothing));

		CurrentTurnAmount = CurrentTurnAmount.SmoothDamp(inputVector, ref turnVelocity, turnSmoothing * PhysicsManager.physicsDelta);
		Accelerate();
	}

	protected virtual void Accelerate()
	{
		CurrentSpeed = ExtensionMethods.SmoothDamp(CurrentSpeed, GetCurrentMaxSpeed(), ref speedVelocity, SpeedSmoothing * PhysicsManager.physicsDelta);
	}

	/// <summary> Override this if you want specific control over the PathFollower's speed. </summary>
	protected virtual float GetCurrentMaxSpeed()
	{
		if (Player.Skills.IsSpeedBreakActive)
			return Player.MoveSpeed;

		return MaxSpeed;
	}
	/// <summary> Override this if you want specific control over the PathFollower's turning. </summary>
	protected virtual float GetCurrentTurnSpeed => TurnSpeed;

	private void UpdateAnimation()
	{
		// Update animations
		if (root != null) // Update visual rotations
		{
			float turnAmount = (CurrentTurnAmount.X / GetCurrentTurnSpeed) - (Player.PathFollower.DeltaAngle * 5.0f);
			float tiltAmount = Mathf.DegToRad(rotationAmount) * tiltRatio * turnAmount;
			root.Rotation = Vector3.Zero;

			root.RotateX(Mathf.DegToRad(rotationAmount) * (CurrentTurnAmount.Y / GetCurrentTurnSpeed));
			if (localRoot != null)
				localRoot.Rotation = new(0, 0, tiltAmount);
			else
				root.RotateZ(tiltAmount);
			root.RotateY(-Mathf.DegToRad(rotationAmount) * turnAmount);
		}

		if (animator != null) // Update animation speeds
			animator.SpeedScale = 1.0f + (CurrentSpeed * 1.5f / MaxSpeed);
	}

	protected virtual void ApplyMovement()
	{
		// Update path follower
		PathFollower.Progress += CurrentSpeed * PhysicsManager.physicsDelta;
		// Add offsets
		PathFollower.HOffset -= CurrentTurnAmount.X * PhysicsManager.physicsDelta;
		PathFollower.VOffset -= CurrentTurnAmount.Y * PhysicsManager.physicsDelta;
		// Clamp offsets
		PathFollower.HOffset = Mathf.Clamp(PathFollower.HOffset, -Bounds.X, Bounds.X);
		PathFollower.VOffset = Mathf.Clamp(PathFollower.VOffset, -Bounds.Y, Bounds.Y);

		// Sync transforms
		GlobalPosition = PathFollower.GlobalPosition;
		GlobalRotation = PathFollower.GlobalRotation;
	}

	/// <summary> Kills all speed and turning. </summary>
	public void StopMovement()
	{
		CurrentSpeed = speedVelocity = 0;
		CurrentTurnAmount = turnVelocity = Vector2.Zero;
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
			Stagger();
			return;
		}

		EmitSignal(SignalName.Damaged);
	}

	/// <summary> Called when the PathTraveller hits a non-lethal hazard. </summary>
	protected virtual void Stagger()
	{
		StopMovement();
		EmitSignal(SignalName.Staggered);
	}

	/// <summary> Gets the end position for the player's damage launch. </summary>
	public virtual Vector3 GetDamageEndPosition() => Player.GlobalPosition;
}
