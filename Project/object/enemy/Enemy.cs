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
	public SpawnModes SpawnMode { get; protected set; }
	public enum SpawnModes
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
	[Export]
	protected float bounceScale = 1f;

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "Node3D")]
	private NodePath root;
	protected Node3D Root { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "Area3D")]
	private NodePath hurtbox;
	/// <summary> Lockon/Hitbox collider. Disabled when defeated (For death animations, etc). </summary>
	protected Area3D Hurtbox { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "CollisionShape3D")]
	private NodePath collider;
	/// <summary> Environmental collider. Disabled when defeated (For death animations, etc). </summary>
	protected CollisionShape3D Collider { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "CollisionShape3D")]
	private NodePath rangeCollider;
	/// <summary> Reference to the enemy's range collider. </summary>
	protected CollisionShape3D RangeCollider { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "AnimationTree")]
	private NodePath animationTree;
	/// <summary> Animation tree for enemy Player. </summary>
	protected AnimationTree AnimationTree { get; private set; }
	[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
	private NodePath animationPlayer;
	/// <summary> Animator for event animations. </summary>
	protected AnimationPlayer AnimationPlayer { get; private set; }

	protected SpawnData SpawnData { get; private set; }
	protected PlayerController Player => StageSettings.Player;

	public bool IsDefeated => currentHealth <= 0;
	protected bool IsSpeedbreakDefeat { get; private set; }
	protected bool IsLightSpeedAttackValid { get; private set; }

	private readonly int EnemyAchievementRequirement = 1000;
	private readonly StringName EnemyAchievementName = "genie buster";

	public override void _Ready() => SetUp();
	protected virtual void SetUp()
	{
		GetComponents();

		SpawnData = new(GetParent(), Transform);
		StageSettings.Instance.Respawned += Respawn;
		StageSettings.Instance.Unloaded += Unload;
		StageSettings.Instance.RespawnedEnemies += RespawnRange;

		InitializeRangeCollider();

		CallDeferred(MethodName.Respawn);
		CallDeferred(MethodName.RespawnRange);
	}

	protected void GetComponents()
	{
		Root = GetNodeOrNull<Node3D>(root);
		Hurtbox = GetNodeOrNull<Area3D>(hurtbox);
		Collider = GetNodeOrNull<CollisionShape3D>(collider);
		RangeCollider = GetNodeOrNull<CollisionShape3D>(rangeCollider);
		AnimationTree = GetNodeOrNull<AnimationTree>(animationTree);
		AnimationPlayer = GetNodeOrNull<AnimationPlayer>(animationPlayer);
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

		if (IsDefeated)
			return;

		if (IsInteracting)
			UpdateInteraction();
		else if (IsInteractionProcessed && Player.AttackState == PlayerController.AttackStates.None)
			ResetInteractionProcessed();
	}

	public virtual void Unload() => QueueFree();
	public virtual void Respawn()
	{
		IsActive = false; // Start disabled
		IsInteracting = false; // Disable interactions
		IsLightSpeedAttackValid = false;

		SpawnData.Respawn(this);
		currentHealth = maxHealth;

		IsSpeedbreakDefeat = false;

		SetHitboxStatus(true);
		ResetInteractionProcessed();

		EmitSignal(SignalName.Respawned);
	}

	private void RespawnRange()
	{
		if (SpawnMode == SpawnModes.Always ||
			(SpawnMode == SpawnModes.Range && IsInRange))
		{
			EnterRange();
		}
	}

	/// <summary> Overload function to allow using Godot's built-in Area3D.OnEntered(Area3D area) signal. </summary>
	protected void Spawn(Area3D _) => Spawn();
	protected virtual void Spawn() => EmitSignal(SignalName.Spawned);

	public virtual void Despawn()
	{
		if (!IsInsideTree()) return;
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
		EmitSignal(SignalName.Despawned);
	}

	/// <summary> Override this from an inherited class. </summary>
	protected virtual void UpdateEnemy() { }

	public virtual void UpdateLockon()
	{
		if (!IsDefeated)
			Player.Camera.SetLockonTarget(Hurtbox);
	}

	public virtual void TakeDamage(int amount = -1)
	{
		if (amount == -1)
			currentHealth = 0;
		else
			currentHealth -= amount;

		if (IsDefeated)
			CallDeferred(MethodName.Defeat);
	}

	/// <summary>
	/// Called when the enemy is defeated.
	/// </summary>
	protected virtual void Defeat()
	{
		currentHealth = 0;
		Player.Camera.SetLockonTarget(null);
		Player.Lockon.ResetLockonTarget();

		CheckLightSpeedAttack();

		SaveManager.SharedData.EnemyCount = (int)Mathf.MoveToward(SaveManager.SharedData.EnemyCount, int.MaxValue, 1);
		if (SaveManager.SharedData.EnemyCount >= EnemyAchievementRequirement)
			AchievementManager.Instance.UnlockAchievement(EnemyAchievementName);

		BonusManager.instance.AddEnemyChain();
		StageSettings.Instance.UpdateScore(50 * maxHealth, StageSettings.MathModeEnum.Add); // Add points based on max health

		// Automatically increment objective count
		if (StageSettings.Instance.Data.MissionType == LevelDataResource.MissionTypes.Enemy)
			StageSettings.Instance.CallDeferred(StageSettings.MethodName.IncrementObjective);

		EmitSignal(SignalName.Defeated);
	}

	/// <summary>
	/// Spawns pearls. Call this somewhere in Defeat(), or from an AnimationPlayer.
	/// </summary>
	protected virtual void SpawnPearls() => Runtime.Instance.SpawnPearls(pearlAmount, Hurtbox != null ? Hurtbox.GlobalPosition : GlobalPosition, new Vector2(2, 1.5f), 1.5f);

	protected bool IsHitboxEnabled { get; private set; }
	protected void SetHitboxStatus(bool isEnabled, bool hurtboxOnly)
	{
		IsHitboxEnabled = isEnabled;

		// Update environment collider
		if (Collider != null && !hurtboxOnly)
			Collider.Disabled = !IsHitboxEnabled;

		// Update hurtbox
		if (Hurtbox != null)
			Hurtbox.Monitorable = Hurtbox.Monitoring = IsHitboxEnabled;
	}

	protected void SetHitboxStatus(bool isEnabled) => SetHitboxStatus(isEnabled, false);

	/// <summary> Is the enemy currently active? </summary>
	protected bool IsActive { get; set; }
	/// <summary> Is the player within the enemies range trigger? </summary>
	protected bool IsInRange { get; set; }
	protected virtual void EnterRange()
	{
		if (SpawnMode == SpawnModes.Signal) return;
		Spawn();
	}
	protected virtual void ExitRange() { }

	/// <summary> True when colliding with the player. </summary>
	protected bool IsInteracting { get; set; }
	/// <summary> True when a particular interaction has already been processed. </summary>
	protected bool IsInteractionProcessed { get; private set; }
	/// <summary> How long it's been since the enemy last interacted with the player. </summary>
	private float timeSinceLastInteraction;
	/// <summary> How long an interaction can last before being "reset". </summary>
	private readonly float MaxInteractionLength = .2f;
	protected virtual void UpdateInteraction()
	{
		if (IsInteractionProcessed)
		{
			timeSinceLastInteraction += PhysicsManager.physicsDelta;
			if (timeSinceLastInteraction < MaxInteractionLength)
				return;

			ResetInteractionProcessed();
		}

		if (!IsHitboxEnabled)
			return;

		switch (Player.AttackState)
		{
			case PlayerController.AttackStates.OneShot:
				IsSpeedbreakDefeat = Player.Skills.IsSpeedBreakActive;
				if (IsSpeedbreakDefeat) // Shake the camera
					Player.Camera.StartMediumCameraShake();

				Defeat();
				break;
			case PlayerController.AttackStates.Weak:
				TakeDamage(1);
				break;
			case PlayerController.AttackStates.Strong:
				TakeDamage(2);
				break;
		}

		if (Player.IsSpinJump)
		{
			Player.StartSpinJumpBounce();
		}
		else if (Player.IsJumpDashOrHomingAttack)
		{
			UpdateLockon();
			IsLightSpeedAttackValid = IsDefeated && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.LightSpeedAttack) &&
				(Input.IsActionPressed("button_attack") || (Input.IsActionPressed("button_jump") && !SaveManager.Config.useStompJumpButtonMode));

			// If the player is trying to perform a light speed attack, only do the bounce AFTER checking the next target
			if (!IsLightSpeedAttackValid)
				Player.StartBounce(IsDefeated, bounceScale, Hurtbox);
		}
		else if ((Player.IsBouncing && !Player.IsBounceInteruptable) ||
			(Player.IsJumping && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.ArmorJump)))
		{
			// Bouncing off an enemy
			UpdateLockon();
			Player.StartBounce(IsDefeated, bounceScale, Hurtbox);
		}
		else if (damagePlayer && Player.AttackState == PlayerController.AttackStates.None &&
			(!Player.IsBouncing || Player.IsBounceInteruptable))
		{
			Player.StartKnockback();
		}

		SetInteractionProcessed();
	}

	protected void SetInteractionProcessed()
	{
		IsInteractionProcessed = true;
		timeSinceLastInteraction = 0;
		Player.AttackStateChanged += ResetInteractionProcessed;
	}

	protected void ResetInteractionProcessed()
	{
		IsInteractionProcessed = false;
		IsLightSpeedAttackValid = false;
		timeSinceLastInteraction = 0;
		Player.AttackStateChanged -= ResetInteractionProcessed;
	}

	protected void CheckLightSpeedAttack()
	{
		if (!IsLightSpeedAttackValid)
			return;

		if (Player.AttemptLightSpeedAttack()) // Transition to Light Speed Attack
			return;

		// Otherwise, do the delayed bounce
		Player.StartBounce(IsDefeated, bounceScale, Hurtbox);
	}

	/// <summary> Current local rotation of the enemy. </summary>
	protected float currentRotation;
	protected float rotationVelocity;
	protected const float TrackingSmoothing = 10f;
	/// <summary>
	/// Updates current rotation to track the player.
	/// </summary>
	protected void ProcessRotation(Vector3 targetPosition, float smoothing = TrackingSmoothing)
	{
		float targetRotation = ExtensionMethods.Flatten(GlobalPosition - targetPosition).AngleTo(Vector2.Up);
		targetRotation -= GlobalRotation.Y; // Rotation is in local space
		ProcessRotation(targetRotation, smoothing);
	}

	protected void ProcessRotation(float targetRotation, float smoothing = TrackingSmoothing)
	{
		currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, smoothing * PhysicsManager.physicsDelta);
		ApplyRotation();
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

		if (Player.IsSpinJump && Player.VerticalSpeed <= 0)
			ResetInteractionProcessed();
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		IsInteracting = false;
	}

	public void OnRangeEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		IsInRange = true;
		EnterRange();
	}

	protected virtual void ApplyRotation() => Root.Rotation = Vector3.Up * currentRotation;

	public void OnRangeExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		IsInRange = false;
		ExitRange();
	}
}