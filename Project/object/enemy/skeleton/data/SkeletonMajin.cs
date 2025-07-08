using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary>
/// Special enemy found in the Skeleton Dome.
/// </summary>
[Tool]
public partial class SkeletonMajin : Enemy
{
	/// <summary> Put the majin into a skeleton pile and respawn instead of being defeated. </summary>
	[Export] private bool isImmortal;
	/// <summary> Should this skeleton move towards the player when attacking? </summary>
	[Export] private bool isMovementEnabled;
	private bool isHurtboxInteraction;

	/// <summary> Timer to keep track of state. </summary>
	private float stateTimer;

	/// <summary> How long should the skeleton stay in an attacking state? </summary>
	[Export] private float attackLength = 1f;
	[Export] private Curve attackDelayCurve;
	/// <summary> Is the enemy currently attacking? </summary>
	private bool isAttacking;
	public void SetAttackStatus(bool value) => isAttacking = value;

	private float movementSpeed;
	private Vector3 movementDirection;
	private readonly float MaxMovementSpeed = 5f;
	private readonly float MovementTrackingSmoothing = 20f;
	private readonly float MovementTraction = 40f;
	private readonly float MovementFriction = 120f;

	/// <summary> Keeps track of whether the skeleton's range was already triggered. </summary>
	private bool wasSpawned;
	/// <summary> How long to stay shattered when isImmortal is true. </summary>
	private readonly float ImmortalRespawnTime = 1.5f;

	private AnimationNodeStateMachinePlayback AnimationState => AnimationTree.Get(AnimationPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName AnimationPlayback = "parameters/playback";
	private readonly StringName SpawnAnimation = "spawn";
	private readonly StringName AttackStartAnimation = "attack-start";
	private readonly StringName AttackStopAnimation = "attack-stop";
	private readonly StringName ShatterResetAnimation = "shatter-reset";
	private readonly StringName ResetAnimation = "RESET";
	private readonly StringName DefeatAnimation = "defeat";
	private readonly StringName DamageAnimation = "damage";

	protected override void SetUp()
	{
		base.SetUp();
		AnimationTree.Active = true;
	}

	protected override void UpdateInteraction()
	{
		if (Player.IsBouncing || !IsHitboxEnabled) return;

		if (isAttacking && !isHurtboxInteraction && !Player.Skills.IsSpeedBreakActive)
			Player.StartKnockback();
		else
			base.UpdateInteraction();
	}

	public override void Respawn()
	{
		base.Respawn();

		wasSpawned = false;
		SetAttackStatus(false);
		SetHitboxStatus(false);

		if (SpawnMode != SpawnModes.Always)
		{
			Spawn();
			return;
		}

		AnimationState.Start(ShatterResetAnimation);
	}

	protected override void EnterRange()
	{
		if (wasSpawned || SpawnMode == SpawnModes.Signal) return;

		Spawn();
	}

	protected override void Spawn()
	{
		if (IsActive) return; // Already spawned

		IsActive = true;
		wasSpawned = true;
		movementSpeed = 0;

		currentHealth = maxHealth; // Reset health
		stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); // Queue next attack

		AnimationState.Travel(SpawnAnimation);
		base.Spawn();
	}

	protected override void UpdateEnemy()
	{
		if (Engine.IsEditorHint())
			return;

		if (IsActive)
		{
			if (isMovementEnabled)
				ProcessMovement();

			if (isAttacking)
			{
				if (IsStateFinished())
					FinishAttack();

				return;
			}

			ProcessRotation(Player.GlobalPosition);
			if (IsHitboxEnabled && IsStateFinished()) // Check whether we can start attacking
				StartAttack();

			return;
		}

		if (wasSpawned && isImmortal && IsStateFinished()) // Check for revival
			Spawn();
	}

	private bool IsStateFinished()
	{
		stateTimer = Mathf.MoveToward(stateTimer, 0, PhysicsManager.physicsDelta);
		return Mathf.IsZeroApprox(stateTimer);
	}

	private void StartAttack()
	{
		AnimationState.Travel(AttackStartAnimation);
		stateTimer = attackLength;
	}

	private void FinishAttack()
	{
		AnimationState.Travel(AttackStopAnimation);
		stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); // Queue next attack
	}

	private void ProcessMovement()
	{
		if (!isAttacking)
		{
			if (!Mathf.IsZeroApprox(movementSpeed))
				movementSpeed = Mathf.MoveToward(movementSpeed, 0f, MovementFriction * PhysicsManager.physicsDelta);

			return;
		}

		// Move towards player
		movementSpeed = Mathf.MoveToward(movementSpeed, MaxMovementSpeed, MovementTraction * PhysicsManager.physicsDelta);
		ProcessRotation(Player.GlobalPosition, MovementTrackingSmoothing);

		GlobalTranslate(Root.Forward() * movementSpeed * PhysicsManager.physicsDelta);
		RaycastHit groundHit = this.CastRay(GlobalPosition + Vector3.Up, Vector3.Down * 2f, Runtime.Instance.environmentMask);
		GlobalPosition = groundHit.point;
	}

	protected override void Defeat()
	{
		base.Defeat();

		IsActive = false;
		AnimationState.Start(isImmortal ? DamageAnimation : DefeatAnimation);
		SetHitboxStatus(false);

		Player.MovementAngle = Player.PathFollower.ForwardAngle; // More consistent direction

		if (isImmortal)
			stateTimer = ImmortalRespawnTime;
	}

	public void OnHurtboxEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;

		IsInteracting = true;
		isHurtboxInteraction = true;
	}

	public void OnHurtboxExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;

		IsInteracting = false;
		isHurtboxInteraction = false;
	}
}