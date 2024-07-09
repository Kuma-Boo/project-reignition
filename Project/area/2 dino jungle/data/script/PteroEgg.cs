using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary> For that one act in Dinosaur Jungle. Follows the player until damage is taken. </summary>
public partial class PteroEgg : Area3D
{
	/// <summary> Emitted when the egg is back in the nest. </summary>
	[Signal]
	public delegate void ReturnedEventHandler();

	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath root;
	private Node3D Root { get; set; }
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animator;
	private AnimationPlayer Animator { get; set; }

	public bool IsHeld => PteroEggManager.heldEggs.Contains(this);
	/// <summary> Is this egg on its way back to its nest? </summary>
	public bool IsReturningToNest { get; private set; }
	/// <summary> Has the egg been returned to the nest successfully? </summary>
	public bool IsReturnedToNest { get; private set; }
	/// <summary> Has this egg been saved at a checkpoint? </summary>
	private bool ignoreRespawn;

	private float followDistance;
	private Vector3 followVelocity;
	private readonly float MinDistance = .5f;
	private readonly float FollowDistanceIncrement = 1.5f;
	private readonly float FollowSmoothing = 10.0f;

	private float returnTravelRatio;
	private SpawnData spawnData;
	private LaunchSettings returnArc; // The path to follow when returning to the nest
	private CharacterController Character => CharacterController.instance;

	public override void _Ready()
	{
		Root = GetNodeOrNull<Node3D>(root);
		Animator = GetNodeOrNull<AnimationPlayer>(animator);

		spawnData = new SpawnData(GetParent(), Transform);
		StageSettings.instance.ConnectRespawnSignal(this);
		StageSettings.instance.Connect(StageSettings.SignalName.TriggeredCheckpoint, new(this, MethodName.SaveNestStatus));
		Character.Connect(CharacterController.SignalName.Knockback, new(this, MethodName.Frighten));
	}

	public override void _PhysicsProcess(double _)
	{
		if (IsReturnedToNest) return;

		if (IsHeld)
		{
			float smoothing = FollowSmoothing;
			Vector3 targetPosition = Character.GlobalPosition + (Character.PathFollower.Back() * followDistance);

			int eggIndex = PteroEggManager.heldEggs.IndexOf(this);
			Vector3 referencePosition = eggIndex == 0 ? Character.GlobalPosition : PteroEggManager.heldEggs[eggIndex - 1].GlobalPosition;
			float distanceSquared = GlobalPosition.DistanceSquaredTo(referencePosition);
			if (distanceSquared < Mathf.Pow(MinDistance, 2.0f)) // Extra snappy when things are too close
			{
				smoothing = 5.0f;
				targetPosition = GlobalPosition - (GlobalPosition.DirectionTo(referencePosition) * MinDistance);
			}

			// Update position to trail player
			GlobalPosition = GlobalPosition.SmoothDamp(targetPosition, ref followVelocity, smoothing * PhysicsManager.physicsDelta);
		}
		else if (IsReturningToNest)
		{
			if (Mathf.IsZeroApprox(returnTravelRatio))
				Animator.Play("returned", .2f);

			returnTravelRatio = Mathf.MoveToward(returnTravelRatio, 1f, PhysicsManager.physicsDelta);
			GlobalPosition = returnArc.InterpolatePositionRatio(returnTravelRatio);

			if (Mathf.IsEqualApprox(returnTravelRatio, 1))
			{
				Animator.Play("sleep", .1f);
				IsReturnedToNest = true;
				IsReturningToNest = false;
				EmitSignal(SignalName.Returned);
			}
		}
	}

	private void SaveNestStatus() => ignoreRespawn = IsReturnedToNest;

	public void Frighten() // Called when the player takes damage or respawns
	{
		if (!IsHeld) return;
		if (PteroEggManager.heldEggs.IndexOf(this) == 0)
			Animator.Play("frighten");

		followDistance -= FollowDistanceIncrement;
	}

	private void Respawn()
	{
		if (ignoreRespawn) return; // Don't respawn if we're already at the nest. Don't force the player to redo stuff they already did.

		if (IsHeld)
			PteroEggManager.heldEggs.Remove(this);

		followVelocity = Vector3.Zero;
		spawnData.Respawn(this);
		Animator.Play("idle");
	}

	public void SetType(Node3D model) // Adds the egg model as a child
	{
		Root.AddChild(model);
		model.GlobalTransform = GlobalTransform;
		model.Position += Vector3.Up * .5f;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		if (IsReturningToNest || IsReturnedToNest) return;
		if (IsHeld) return;

		PteroEggManager.heldEggs.Add(this);
		followDistance = FollowDistanceIncrement * PteroEggManager.heldEggs.Count;
	}

	public void ReturnToNest(PteroNest nest)
	{
		if (IsHeld)
			PteroEggManager.heldEggs.Remove(this);

		IsReturningToNest = true;

		returnTravelRatio = 0f;
		returnArc = LaunchSettings.Create(GlobalPosition, nest.GlobalPosition, 4f, true);
	}
}