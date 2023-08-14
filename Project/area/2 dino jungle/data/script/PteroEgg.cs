using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// For that one act in Dinosaur Jungle. Follows the player until they take damage.
	/// </summary>
	public partial class PteroEgg : Area3D
	{
		[Signal]
		public delegate void ReturnedEventHandler(); //Emitted when the egg is back in the nest

		[Export]
		private Node3D root;
		[Export]
		private AnimationPlayer animator;

		public int EggIndex { get; set; } //Is this the first (closest) egg, or the second (farther) egg? or, is this egg not being carried? (index 0)
		private bool isSleeping; //Egg is sleeping, and can no longer be interacted with
		private bool isReturnedToNest; //Has the egg been returned to the nest?

		private float returnTravelRatio;
		private SpawnData spawnData;
		private LaunchSettings returnArc; //The path to follow when returning to the nest
		private CharacterController Character => CharacterController.instance;
		private readonly float FOLLOW_DISTANCE = 1f;

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
				//Update position to trail player
				GlobalPosition = Character.GlobalPosition + Character.PathFollower.Back() * FOLLOW_DISTANCE;
			}
			else if (isReturnedToNest)
			{
				if (Mathf.IsZeroApprox(returnTravelRatio))
					animator.Play("returned", .2f);

				returnTravelRatio = Mathf.MoveToward(returnTravelRatio, 1f, PhysicsManager.physicsDelta);
				GlobalPosition = returnArc.InterpolatePositionRatio(returnTravelRatio);

				if (Mathf.IsEqualApprox(returnTravelRatio, 1))
				{
					isSleeping = true;
					EmitSignal(SignalName.Returned);
				}
			}
		}

		public void Frighten() //Called when the player takes damage, dies, or a third egg is picked up.
		{
			EggIndex = 0;
			animator.Play("frighten");
		}

		private void Respawn()
		{
			if (isReturnedToNest) return; //Don't respawn if we're already at the nest. Don't force the player to redo stuff they already did.

			if (EggIndex != 0)
			{
				PteroEggManager.DropEgg(this);
			}

			spawnData.Respawn(this);
			animator.Play("idle");
		}

		public void SetType(Node3D model) //Adds the egg model as a child
		{
			root.CallDeferred("add_child", model);
			model.SetDeferred("global_transform", GlobalTransform);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

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
			returnArc = LaunchSettings.Create(GlobalPosition, nest.GlobalPosition + Vector3.Up * 0.6f, 4f, true);
		}
	}
}
