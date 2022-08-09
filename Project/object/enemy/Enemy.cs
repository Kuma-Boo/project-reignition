using Godot;

namespace Project.Gameplay
{
	public class Enemy : RespawnableObject
	{
		[Signal]
		public delegate void Defeated();
		
		protected int currentHealth; //Override this in inherited class, otherwise all enemies will be defeated in a single hit
		protected override bool IsRespawnable() => true; //Enemies are always respawnable

		public override void _PhysicsProcess(float delta)
		{
			if (!IsInsideTree() || !Visible) return;

			ProcessEnemy();
		}

		protected virtual void ProcessEnemy() { }

		public virtual void TakeDamage()
		{
			currentHealth--; //TODO increase player attack based on skills?
			if (currentHealth <= 0)
				Defeat();
		}

		//Called when the enemy is defeated
		protected virtual void Defeat() => EmitSignal(nameof(Defeated));

		//Called when the player interacts with this enemy
		protected bool isInteractingWithPlayer;
		protected virtual void Interact() { }

		public void OnEntered(Area _)
		{
			isInteractingWithPlayer = true;
			Interact();
		}

		public void OnExited(Area _)
		{
			isInteractingWithPlayer = false;
			Character.DequeueHitbox(this);
		}
	}
}
