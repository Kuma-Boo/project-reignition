using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay;

public partial class Enemy : Node3D
{
	private static readonly Dictionary<int, CylinderShape3D> CollisionShapeList = [];

	[Signal]
	public delegate void RespawnedEventHandler();
	[Signal]
	public delegate void SpawnedEventHandler();
	[Signal]
	public delegate void DespawnedEventHandler();
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

	[Export(PropertyHint.Range, "0, 100")]
	public int rangeOverride = 50;

	[Export]
	/// <summary> Number of pearls to spawn when the enemy is defeated. </summary>
	protected int pearlAmount;
	[Export]
	protected int maxHealth = 1;
	protected int currentHealth;
	/// <summary> Does this enemy damage the player when it is touched? </summary>
	[Export]
	protected bool damagePlayer;

	[ExportGroup("Components")]
	[Export(PropertyHint.NodeType, "Node3D")]
	private NodePath root;
	protected Node3D Root { get; private set; }
	[Export(PropertyHint.NodeType, "Area3D")]
	private NodePath hurtbox;
	/// <summary> Lockon/Hitbox collider. Disabled when defeated (For death animations, etc). </summary>
	protected Area3D Hurtbox { get; private set; }
	[Export(PropertyHint.NodeType, "CollisionShape3D")]
	private NodePath collider;
	/// <summary> Environmental collider. Disabled when defeated (For death animations, etc). </summary>
	protected CollisionShape3D Collider { get; private set; }
	[Export(PropertyHint.NodeType, "CollisionShape3D")]
	private NodePath rangeCollider;
	/// <summary> Reference to the enemy's range collider. </summary>
	protected CollisionShape3D RangeCollider { get; private set; }
	[Export(PropertyHint.NodeType, "AnimationTree")]
	private NodePath animationTree;
	/// <summary> Animation tree for enemy character. </summary>
	protected AnimationTree AnimationTree { get; private set; }
	[Export(PropertyHint.NodeType, "AnimationPlayer")]
	private NodePath animationPlayer;
	/// <summary> Animator for event animations. </summary>
	protected AnimationPlayer AnimationPlayer { get; private set; }

	protected SpawnData SpawnData { get; private set; }
	protected CharacterController Character => CharacterController.instance;

	protected bool IsDefeated => currentHealth <= 0;

	public override void _Ready() => SetUp();
	protected virtual void SetUp()
	{
		// Get components
		Root = GetNodeOrNull<Node3D>(root);
		Hurtbox = GetNodeOrNull<Area3D>(hurtbox);
		Collider = GetNodeOrNull<CollisionShape3D>(collider);
		RangeCollider = GetNodeOrNull<CollisionShape3D>(rangeCollider);
		AnimationTree = GetNodeOrNull<AnimationTree>(animationTree);
		AnimationPlayer = GetNodeOrNull<AnimationPlayer>(animationPlayer);

		SpawnData = new(GetParent(), Transform);
		StageSettings.instance.ConnectRespawnSignal(this);
		StageSettings.instance.ConnectUnloadSignal(this);
		Respawn();

		InitializeRangeCollider();
	}

	private void InitializeRangeCollider()
	{
		if (RangeCollider == null)
			return;

		if (rangeOverride == 0) // Disable range collider
		{
			RangeCollider.Disabled = true;
			return;
		}

		// Resize range trigger
		if (CollisionShapeList.TryGetValue(rangeOverride, out CylinderShape3D shape))
		{
			RangeCollider.Shape = shape;
			return;
		}

		// Cache a new collision shape
		CylinderShape3D cylinderShape = new()
		{
			Radius = rangeOverride,
			Height = 30
		};
		RangeCollider.Shape = cylinderShape;
		CollisionShapeList.Add(rangeOverride, cylinderShape);
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
		ResetInteractionProcessed();

		if (spawnMode == SpawnModes.Always ||
			(spawnMode == SpawnModes.Range && IsInRange)) // No activation trigger. Activate immediately.
		{
			EnterRange();
		}

		EmitSignal(SignalName.Respawned);
	}

	/// <summary> Overload function to allow using Godot's built-in Area3D.OnEntered(Area3D area) signal. </summary>
	protected void Spawn(Area3D _) => Spawn();
	protected virtual void Spawn() => EmitSignal(SignalName.Spawned);

	public virtual void Despawn()
	{
		if (!IsInsideTree()) return;
		GetParent().CallDeferred("remove_child", this);
		EmitSignal(SignalName.Despawned);
	}

	/// <summary> Override this from an inherited class. </summary>
	protected virtual void UpdateEnemy() { }

	public virtual void UpdateLockon()
	{
		if (Character.Lockon.IsHomingAttacking)
			Character.Lockon.CallDeferred(CharacterLockon.MethodName.StopHomingAttack);

		if (!IsDefeated)
			Character.Camera.SetDeferred("LockonTarget", Hurtbox);
	}

	public virtual void TakeDamage(int amount = -1)
	{
		if (amount == -1)
			currentHealth = 0;
		else
			currentHealth -= amount;

		if (IsDefeated)
			Defeat();
	}

	/// <summary>
	/// Called when the enemy is defeated.
	/// </summary>
	protected virtual void Defeat()
	{
		Character.Camera.LockonTarget = null;
		Character.Lockon.CallDeferred(CharacterLockon.MethodName.ResetLockonTarget);
		BonusManager.instance.AddEnemyChain();
		StageSettings.instance.UpdateScore(50 * maxHealth, StageSettings.MathModeEnum.Add); // Add points based on max health

		// Automatically increment objective count
		if (StageSettings.instance.Data.MissionType == LevelDataResource.MissionTypes.Enemy)
			StageSettings.instance.IncrementObjective();

		EmitSignal(SignalName.Defeated);
	}

	/// <summary>
	/// Spawns pearls. Call this somewhere in Defeat(), or from an AnimationPlayer.
	/// </summary>
	protected virtual void SpawnPearls() => Runtime.Instance.SpawnPearls(pearlAmount, GlobalPosition, new Vector2(2, 1.5f), 1.5f);

	protected bool IsHitboxEnabled { get; private set; }
	protected void SetHitboxStatus(bool isEnabled, bool hurtboxOnly = false)
	{
		IsHitboxEnabled = isEnabled;

		// Update environment collider
		if (Collider != null && !hurtboxOnly)
			Collider.Disabled = !IsHitboxEnabled;

		// Update hurtbox
		if (Hurtbox != null)
			Hurtbox.Monitorable = Hurtbox.Monitoring = IsHitboxEnabled;
	}

	/// <summary> Is the enemy currently active? </summary>
	protected bool IsActive { get; set; }
	/// <summary> Is the player within the enemies range trigger? </summary>
	protected bool IsInRange { get; set; }
	protected virtual void EnterRange()
	{
		if (spawnMode == SpawnModes.Signal) return;
		Spawn();
	}
	protected virtual void ExitRange() { }

	/// <summary> True when colliding with the player. </summary>
	protected bool IsInteracting { get; set; }
	/// <summary> True when a particular interaction has already been processed. </summary>
	protected bool IsInteractionProcessed { get; private set; }
	protected virtual void UpdateInteraction()
	{
		if (IsInteractionProcessed)
			return;

		if ((Character.Lockon.IsBouncingLockoutActive &&
			Character.ActionState == CharacterController.ActionStates.Normal) ||
			!IsHitboxEnabled)
		{
			return;
		}

		if (Character.ActionState == CharacterController.ActionStates.JumpDash)
		{
			UpdateLockon();
			Character.Lockon.StartBounce(IsDefeated);
		}
		else if (damagePlayer && Character.AttackState == CharacterController.AttackStates.None)
		{
			Character.StartKnockback();
		}
		switch (Character.AttackState)
		{
			case CharacterController.AttackStates.OneShot:
				Defeat();
				break;
			case CharacterController.AttackStates.Weak:
				TakeDamage(1);
				break;
			case CharacterController.AttackStates.Strong:
				TakeDamage(2);
				break;
		}

		SetInteractionProcessed();
	}

	protected void SetInteractionProcessed()
	{
		IsInteractionProcessed = true;
		// Connect a signal
		Character.Connect(CharacterController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed), (uint)ConnectFlags.OneShot);
	}
	protected void ResetInteractionProcessed() => IsInteractionProcessed = false;

	/// <summary> Current local rotation of the enemy. </summary>
	protected float currentRotation;
	protected float rotationVelocity;
	protected const float TrackingSmoothing = .2f;
	/// <summary>
	/// Updates current rotation to track the player.
	/// </summary>
	protected void TrackPlayer()
	{
		float targetRotation = ExtensionMethods.Flatten(GlobalPosition - Character.GlobalPosition).AngleTo(Vector2.Up);
		targetRotation -= GlobalRotation.Y; // Rotation is in local space
		currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, TrackingSmoothing);
	}

	protected virtual void StartUhuBounce() { }

	public void OnEntered(Area3D a)
	{
		if (a.IsInGroup("uhu"))
		{
			StartUhuBounce();
			return;
		}

		if (!a.IsInGroup("player")) return;
		IsInteracting = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		IsInteracting = false;
	}

	public void OnRangeEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		EnterRange();
		IsInRange = true;
	}

	public void OnRangeExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		ExitRange();
		IsInRange = false;
	}
}
