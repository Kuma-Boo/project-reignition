using Godot;

namespace Project.Gameplay
{
	public partial class Enemy : Node3D
	{
		[Signal]
		public delegate void DefeatedEventHandler();

		[Export]
		protected CollisionShape3D collider; //Environmental collider. Disabled when defeated (For death animations, etc)
		[Export]
		protected Area3D hurtbox; //Lockon/Hitbox collider. Disabled when defeated (For death animations, etc)
		[Export(PropertyHint.Range, "1, 10")]
		public int maxHealth = 1;
		protected int currentHealth;
		[Export]
		public bool damagePlayer; //Does this enemy hurt the player on touch?

		protected SpawnData SpawnData { get; private set; }
		protected CharacterController Character => CharacterController.instance;

		protected bool IsDefeated => currentHealth <= 0;

		public override void _Ready() => SetUp();
		protected virtual void SetUp()
		{
			SpawnData = new SpawnData(GetParent(), Transform);
			LevelSettings.instance.ConnectRespawnSignal(this);
			LevelSettings.instance.ConnectUnloadSignal(this);
			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (!IsInsideTree() || !Visible) return;

			UpdateEnemy();

			if (isInteracting)
				UpdateInteraction();
		}

		public virtual void Unload() => QueueFree();
		public virtual void Respawn()
		{
			SpawnData.Respawn(this);
			currentHealth = maxHealth;

			PhysicsMonitoring = true;
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;
			GetParent().CallDeferred("remove_child", this);
		}

		public virtual void TakeDamage()
		{
			if (Character.Lockon.IsPerfectHomingAttack)
				currentHealth -= 2; //float damage
			else
				currentHealth--; //TODO increase player attack based on skills?

			if (IsDefeated)
				Defeat();
			else
				Character.Camera.LockonTarget = this;
		}

		protected virtual void UpdateEnemy() { }

		//Called when the enemy is defeated
		protected virtual void Defeat()
		{
			PhysicsMonitoring = false;
			OnExited(null);
			EmitSignal(SignalName.Defeated);
		}

		protected bool PhysicsMonitoring
		{
			set
			{
				//Update environment collider
				if (collider != null)
					collider.SetDeferred("disabled", !value);

				//Update hurtbox
				if (hurtbox != null)
				{
					hurtbox.SetDeferred("monitorable", value);
					hurtbox.SetDeferred("monitoring", value);
				}
			}
		}

		/// <summary> Is the enemy being processed? Can be set from a trigger or a sub-class. </summary>
		protected bool IsActivated { get; set; }
		protected virtual void Activate() { }
		protected virtual void Deactivate() { }

		protected bool isInteracting; //True when colliding with an object
		protected bool isInteractingWithPlayer; //True when colliding with the player specifically
		protected virtual void UpdateInteraction()
		{
			if (isInteractingWithPlayer)
			{
				if (Character.Lockon.IsBouncing) return;

				if (Character.Skills.IsSpeedBreakActive) //For now, speed break kills enemies instantly
					Defeat();
				else if (Character.MovementState == CharacterController.MovementStates.Launcher) //Launcher kills enemies instantly
					Defeat();
				else if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				{
					Character.Lockon.StartBounce(); //Important! Bounce must occur first
					TakeDamage();
				}
				else if (damagePlayer)
					Character.StartKnockback();
			}
		}

		public void OnEntered(Area3D a)
		{
			isInteracting = true;
			isInteractingWithPlayer = a.IsInGroup("player");
		}

		public void OnExited(Area3D _)
		{
			isInteracting = false;

			if (isInteractingWithPlayer)
				isInteractingWithPlayer = false;
		}

		public void OnRangeEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			IsActivated = true;
			Activate();
		}

		public void OnRangeExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			IsActivated = false;
			Deactivate();
		}
	}
}
