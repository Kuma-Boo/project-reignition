using Godot;

namespace Project.Gameplay
{
	public partial class Enemy : RespawnableObject
	{
		[Signal]
		public delegate void DefeatedEventHandler();

		[Export]
		public NodePath collider; //Environmental collider. Disabled when defeated (For death animations, etc)
		protected CollisionShape3D _collider;
		[Export]
		public NodePath hitbox; //Lockon/Hitbox collider. Disabled when defeated (For death animations, etc)
		protected Area3D _hitbox;
		[Export]
		public int maxHealth;
		protected int currentHealth;
		[Export]
		public bool damagePlayer;

		protected override bool IsRespawnable() => true; //Enemies are always respawnable

		protected override void SetUp()
		{
			if (collider != null) _collider = GetNode<CollisionShape3D>(collider);
			if (hitbox != null) _hitbox = GetNode<Node>(hitbox) as Area3D;

			base.SetUp();
			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (!IsInsideTree() || !Visible) return;

			ProcessEnemy();

			if (isInteracting)
				ProcessInteraction();
		}

		public override void Respawn()
		{
			base.Respawn();

			currentHealth = maxHealth;

			if (_collider != null) _collider.Disabled = false;
			if (_hitbox != null) _hitbox.Monitorable = _hitbox.Monitoring = true;
		}

		public virtual void TakeDamage()
		{
			if (Character.Lockon.IsPerfectHomingAttack)
				currentHealth -= 2; //float damage
			else
				currentHealth--; //TODO increase player attack based on skills?

			if (currentHealth <= 0)
				Defeat();
		}

		protected virtual void ProcessEnemy() { }

		//Called when the enemy is defeated
		protected virtual void Defeat()
		{
			//Stop colliding/monitoring
			if (_collider != null) _collider.Disabled = true;
			if (_hitbox != null) _hitbox.Monitorable = _hitbox.Monitoring = false;

			OnExited(null);
			EmitSignal(SignalName.Defeated);
		}

		protected bool isInteracting; //True when colliding with an object
		protected bool isInteractingWithPlayer; //True when colliding with the player specifically
		protected virtual void ProcessInteraction()
		{
			if (isInteractingWithPlayer)
			{
				if (Character.Lockon.IsBouncing) return;

				if (Character.Skills.IsSpeedBreakActive) //For now, speed break kills enemies instantly
					Defeat();
				else if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				{
					TakeDamage();
					Character.Lockon.StartBounce();
				}
				else if (damagePlayer)
					Character.TakeDamage(this);
			}
		}

		public void OnEntered(Area3D area)
		{
			isInteracting = true;
			isInteractingWithPlayer = area.IsInGroup("player");
		}

		public void OnExited(Area3D _)
		{
			isInteracting = false;

			if (isInteractingWithPlayer)
				isInteractingWithPlayer = false;
		}
	}
}
