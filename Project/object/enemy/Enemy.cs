using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public partial class Enemy : Node3D
	{
		[Signal]
		public delegate void SpawnedEventHandler();
		[Signal]
		public delegate void DefeatedEventHandler();

		[Export]
		protected SpawnModes spawnMode;
		protected enum SpawnModes
		{
			Range, // Use Range trigger
			Signal, // External Signal
			Always, // Always spawned
		}

		[Export(PropertyHint.Range, "-1, 100")]
		protected int rangeOverride = -1;

		[Export]
		/// <summary> Number of pearls to spawn when the enemy is defeated. </summary>
		protected int pearlAmount;
		[Export]
		protected int maxHealth = 1;
		protected int currentHealth;
		[Export]
		protected bool damagePlayer; // Does this enemy hurt the player on touch?

		[ExportGroup("Components")]
		[Export]
		protected Node3D root;
		[Export]
		protected CollisionShape3D collider; // Environmental collider. Disabled when defeated (For death animations, etc)
		[Export]
		protected Area3D hurtbox; // Lockon/Hitbox collider. Disabled when defeated (For death animations, etc)
		[Export]
		protected CollisionShape3D rangeCollider; // Range trigger
		[Export]
		/// <summary> Animation tree for enemy character. </summary>
		protected AnimationTree animationTree;
		[Export]
		/// <summary> Animator for event animations. </summary>
		protected AnimationPlayer animationPlayer;

		protected SpawnData SpawnData { get; private set; }
		protected CharacterController Character => CharacterController.instance;

		protected bool IsDefeated => currentHealth <= 0;

		public override void _Ready() => SetUp();
		protected virtual void SetUp()
		{
			SpawnData = new SpawnData(GetParent(), Transform);
			StageSettings.instance.ConnectRespawnSignal(this);
			StageSettings.instance.ConnectUnloadSignal(this);
			Respawn();

			if (rangeCollider != null && rangeOverride != -1)
			{
				if (rangeOverride == 0) // Disable
					rangeCollider.Disabled = true;
				else // Resize range trigger
				{
					rangeCollider.Shape = new CylinderShape3D()
					{
						Radius = rangeOverride,
						Height = 15
					};
				}
			}
		}

		public override void _PhysicsProcess(double _)
		{
			if (!IsInsideTree() || !Visible) return;

			UpdateEnemy();

			if (!IsDefeated && IsInteracting)
				UpdateInteraction();
		}

		public virtual void Unload() => QueueFree();
		public virtual void Respawn()
		{
			IsActive = false; // Start disabled

			SpawnData.Respawn(this);
			currentHealth = maxHealth;

			SetHitboxStatus(true);
		}

		/// <summary>
		/// Overload function to allow using Godot's built-in Area3D.OnEntered(Area3D area) signal.
		/// </summary>
		protected void Spawn(Area3D _) => Spawn();
		protected virtual void Spawn() => EmitSignal(SignalName.Spawned);

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;
			GetParent().CallDeferred("remove_child", this);
		}

		public virtual void TakeDamage()
		{
			if (Character.Lockon.IsPerfectHomingAttack)
				currentHealth -= 2; // float damage
			else
				currentHealth--; // TODO increase player attack based on skills?

			if (IsDefeated)
				Defeat();
			else
				Character.Camera.LockonTarget = this;
		}

		protected virtual void UpdateEnemy() { }

		/// <summary>
		/// Called when the enemy is defeated.
		/// </summary>
		protected virtual void Defeat()
		{
			Character.Camera.LockonTarget = null;
			SetHitboxStatus(false);
			EmitSignal(SignalName.Defeated);
		}

		/// <summary>
		/// Spawns pearls. Call this somewhere in Defeat(), or from an AnimationPlayer.
		/// </summary>
		protected virtual void SpawnPearls() => Runtime.Instance.SpawnPearls(pearlAmount, GlobalPosition, new Vector2(2, 1.5f), 1.5f);

		protected bool IsHitboxEnabled { get; private set; }
		protected void SetHitboxStatus(bool isEnabled)
		{
			IsHitboxEnabled = isEnabled;

			// Update environment collider
			if (collider != null)
				collider.Disabled = !IsHitboxEnabled;

			// Update hurtbox
			if (hurtbox != null)
				hurtbox.Monitorable = hurtbox.Monitoring = IsHitboxEnabled;
		}

		/// <summary> Is the enemy currently active? </summary>
		protected bool IsActive { get; set; }
		/// <summary> Is the player within the enemies range trigger? </summary>
		protected bool IsInRange { get; set; }
		protected virtual void EnterRange() { }
		protected virtual void ExitRange() { }

		// True when colliding with the player
		protected bool IsInteracting => interactionCounter != 0;
		protected int interactionCounter;
		protected virtual void UpdateInteraction()
		{
			if (Character.Lockon.IsBouncingLockoutActive || !IsHitboxEnabled) return;

			if (Character.Skills.IsSpeedBreakActive) // For now, speed break kills enemies instantly
				Defeat();
			else if (Character.MovementState == CharacterController.MovementStates.Launcher) // Launcher kills enemies instantly
				Defeat();
			else if (Character.ActionState == CharacterController.ActionStates.JumpDash)
			{
				TakeDamage();
				Character.Lockon.StartBounce(IsDefeated);
			}
			else if (damagePlayer)
				Character.StartKnockback();
		}

		/// <summary> Current rotation of the enemy. </summary>
		protected float currentRotation;
		protected float rotationVelocity;
		protected const float TRACKING_SMOOTHING = .2f;
		/// <summary>
		/// Updates current rotation to track the player.
		/// </summary>
		protected void TrackPlayer()
		{
			float targetRotation = ExtensionMethods.Flatten(GlobalPosition - Character.GlobalPosition).AngleTo(Vector2.Up);
			targetRotation -= GlobalRotation.Y; // Rotation is in local space
			currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, TRACKING_SMOOTHING);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			interactionCounter++;
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			interactionCounter--;
		}

		public void OnRangeEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			EnterRange();
			IsInRange = true;
		}

		public void OnRangeExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			ExitRange();
			IsInRange = false;
		}
	}
}
