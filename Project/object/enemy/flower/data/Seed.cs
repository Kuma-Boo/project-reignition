using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary> Spawned from a FlowerDjinn. Explodes on impact. </summary>
	public partial class Seed : Area3D
	{
		[Export]
		private float moveSpeed;
		[Export]
		private AnimationPlayer animator;

		private bool isMoving;

		public override void _PhysicsProcess(double _)
		{
			if (!isMoving) return;

			GlobalPosition += this.Forward() * moveSpeed * PhysicsManager.physicsDelta;
		}

		public void Spawn()
		{
			animator.Play("RESET");
			animator.Seek(0, true);

			animator.Play("move");
			isMoving = true;
		}

		private void Explode()
		{
			isMoving = false;
			animator.Play("explode");
		}

		public void OnEntered(Area3D a)
		{
			if (CharacterController.instance.IsAttacking)
			{
				if (CharacterController.instance.Lockon.IsHomingAttacking)
					CharacterController.instance.Lockon.StartBounce();
			}
			else
				CharacterController.instance.TakeDamage(this);

			Explode();
		}

		public void OnBodyEntered(PhysicsBody3D b) => Explode();
	}
}
