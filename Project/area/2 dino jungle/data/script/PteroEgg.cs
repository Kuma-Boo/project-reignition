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

	/// <summary> How close is this egg to the player? </summary>
	public int EggIndex { get; set; }
	/// <summary> Egg is sleeping, and can no longer be interacted with. </summary>
	private bool isSleeping;
	/// <summary> Has the egg been returned to the nest successfully? </summary>
	private bool isReturnedToNest;

	private float returnTravelRatio;
	private SpawnData spawnData;
	private LaunchSettings returnArc; // The path to follow when returning to the nest
	private CharacterController Character => CharacterController.instance;
	private readonly float FollowDistance = 1f;

	public override void _Ready()
	{
		spawnData = new SpawnData(GetParent(), Transform);
		StageSettings.instance.ConnectRespawnSignal(this);
	}

	public override void _PhysicsProcess(double _)
	{
		if (isSleeping) return;

		if (EggIndex != 0)
		{
			// Update position to trail player
			GlobalPosition = Character.GlobalPosition + (Character.PathFollower.Back() * FollowDistance);
		}
		else if (isReturnedToNest)
		{
			if (Mathf.IsZeroApprox(returnTravelRatio))
				Animator.Play("returned", .2f);

			returnTravelRatio = Mathf.MoveToward(returnTravelRatio, 1f, PhysicsManager.physicsDelta);
			GlobalPosition = returnArc.InterpolatePositionRatio(returnTravelRatio);

			if (Mathf.IsEqualApprox(returnTravelRatio, 1))
			{
				isSleeping = true;
				EmitSignal(SignalName.Returned);
			}
		}
	}

	public void Frighten() // Called when the player takes damage, dies, or a third egg is picked up.
	{
		EggIndex = 0;
		Animator.Play("frighten");
	}

	private void Respawn()
	{
		if (isReturnedToNest) return; // Don't respawn if we're already at the nest. Don't force the player to redo stuff they already did.

		if (EggIndex != 0)
			PteroEggManager.DropEgg(this);

		spawnData.Respawn(this);
		Animator.Play("idle");
	}

	public void SetType(Node3D model) // Adds the egg model as a child
	{
		Root.CallDeferred("add_child", model);
		model.SetDeferred("global_transform", GlobalTransform);
		model.SetDeferred("global_transform", GlobalTransform);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.Frighten), (uint)ConnectFlags.OneShot);
		PteroEggManager.PickUpEgg(this);
	}

	public void ReturnToNest(PteroNest nest)
	{
		PteroEggManager.DropEgg(this);
		isReturnedToNest = true;

		Vector3 delta = GlobalPosition - nest.GlobalPosition;

		GetParent().CallDeferred("remove_child", this);
		nest.CallDeferred("add_child", this);
		SetDeferred("global_position", nest.GlobalPosition + delta);

		returnTravelRatio = 0f;
		returnArc = LaunchSettings.Create(GlobalPosition, nest.GlobalPosition + (Vector3.Up * 0.6f), 4f, true);
	}
}