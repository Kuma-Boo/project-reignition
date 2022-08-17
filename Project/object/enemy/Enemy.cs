using Godot;

namespace Project.Gameplay
{
	public class Enemy : RespawnableObject
	{
		[Signal]
		public delegate void Defeated();

		[Export]
		public NodePath collider; //Environmental collider. Disabled when defeated
		protected CollisionShape _collider;
		[Export]
		public NodePath hitbox; //Lockon/Hitbox collider. Disabled when defeated
		protected Area _hitbox;

		[Export]
		public int maxHealth;
		private int currentHealth;
		[Export]
		public bool damagePlayer;

		protected override bool IsRespawnable() => true; //Enemies are always respawnable

		protected override void SetUp()
		{
			if (collider != null) _collider = GetNode<CollisionShape>(collider);
			if (hitbox != null) _hitbox = GetNode<Node>(hitbox) as Area;

			base.SetUp();
			Respawn();
		}

		public override void _PhysicsProcess(float delta)
		{
			if (!IsInsideTree() || !Visible) return;

			ProcessEnemy();

			if(isInteracting)
				ProcessInteraction();
		}

		public override void Respawn()
		{
			base.Respawn();

			currentHealth = maxHealth;

			if(_collider != null) _collider.Disabled = false;
			if(_hitbox != null) _hitbox.Monitorable = _hitbox.Monitoring = true;
		}

		public virtual void TakeDamage()
		{
			currentHealth--; //TODO increase player attack based on skills?

			if (currentHealth <= 0)
				Defeat();
		}

		protected virtual void ProcessEnemy() { }

		//Called when the enemy is defeated
		protected virtual void Defeat()
		{
			if (_collider != null) _collider.Disabled = true;
			if (_hitbox != null) _hitbox.Monitorable = _hitbox.Monitoring = false;
			
			OnExited(null);
			EmitSignal(nameof(Defeated));
		}

		protected bool isInteracting; //True when colliding with an object
		protected bool isInteractingWithPlayer; //True when colliding with the player specifically
		protected virtual void ProcessInteraction()
		{
			if(isInteractingWithPlayer)
			{
				if (Character.Lockon.IsBouncing) return;

				if(Character.Soul.IsSpeedBreakActive) //For now, speed break kills enemies instantly
					Defeat();
				else if(Character.ActionState == CharacterController.ActionStates.JumpDash)
				{
					TakeDamage();
					Character.Lockon.StartBounce();
				}
				else if(damagePlayer)
					Character.TakeDamage(this);
			}
		}

		public void OnEntered(Area area)
		{
			isInteracting = true;
			isInteractingWithPlayer = area.IsInGroup("player");
		}

		public void OnExited(Area _)
		{
			isInteracting = false;

			if(isInteractingWithPlayer)
				isInteractingWithPlayer = false;
		}
	}
}
