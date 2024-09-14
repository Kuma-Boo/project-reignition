using Godot;

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
	public float MaxSpeed => maxSpeed;
	/// <summary> How fast to turn. </summary>
	[Export]
	private float turnSpeed;
	public float TurnSpeed => turnSpeed;

	/// <summary> Allow object to move vertically? </summary>
	[Export]
	private bool isVerticalMovementDisabled;
	public bool IsVerticalMovementDisabled => isVerticalMovementDisabled;

	[Export]
	private float rotationAmount = 45;
	[Export]
	private float tiltRatio = 1.0f;

	/// <summary> Maximum distance from the path allowed. </summary>
	[Export]
	private Vector2 bounds;
	public Vector2 Bounds => bounds;
	public void SetHorizontalBounds(float newBound) => bounds.X = Mathf.Abs(newBound);
	[Export]
	private bool autosetBounds;
	public bool AutosetBounds => autosetBounds;

	/// <summary> Reference to the travel path. </summary>
	[ExportGroup("Components")]
	[Export]
	private Path3D path;
	/// <summary> Reference to the pathfollower. </summary>
	[Export]
	private PathFollow3D pathFollower;
	public PathFollow3D PathFollower => pathFollower;
	/// <summary> Reference to the root. </summary>
	[Export]
	private Node3D root;
	/// <summary> Optional node if excluding tilt is needed (boat ripples). </summary>
	[Export]
	private Node3D localRoot;
	/// <summary> Reference to the player's position. </summary>
	[Export]
	private Node3D playerPosition;
	public Node3D PlayerStandin => playerPosition;
	/// <summary> Reference to the animator. </summary>
	[Export]
	private AnimationPlayer animator;

	private float startingProgress;
	private Vector3 startingOffset;
	private SpawnData spawnData;

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
		spawnData = new SpawnData(GetParent(), Transform); // Create spawn data

		StageSettings.Instance.ConnectRespawnSignal(this);
		Respawn();
	}

	public void UpdateAnimation(Vector2 turnVector, float currentSpeed)
	{
		// Update animations
		if (root != null) // Update visual rotations
		{
			float turnAmount = (turnVector.X / turnSpeed) - (Player.PathFollower.DeltaAngle * 5.0f);
			float tiltAmount = Mathf.DegToRad(rotationAmount) * tiltRatio * turnAmount;
			root.Rotation = Vector3.Zero;

			root.RotateX(Mathf.DegToRad(rotationAmount) * (turnVector.Y / turnSpeed));
			if (localRoot != null)
				localRoot.Rotation = new(0, 0, tiltAmount);
			else
				root.RotateZ(tiltAmount);
			root.RotateY(-Mathf.DegToRad(rotationAmount) * turnAmount);
		}

		if (animator != null) // Update animation speeds
			animator.SpeedScale = 1.0f + (currentSpeed / maxSpeed * 1.5f);
		Player.Animator.UpdateBalancing(Player.Controller.InputAxis.X - (Player.PathFollower.DeltaAngle * 20.0f));
		Player.Animator.UpdateBalanceSpeed(1f + Player.Stats.GroundSettings.GetSpeedRatio(currentSpeed), 0f);
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
