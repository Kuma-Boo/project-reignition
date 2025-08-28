using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary> Special enemy found in the Skeleton Dome. </summary>
[Tool]
public partial class SkeletonMajin : Enemy
{
	/// <summary> Put the majin into a skeleton pile and respawn instead of being defeated. </summary>
	[Export] private bool isImmortal;
	/// <summary> Should this skeleton move towards the player when attacking? </summary>
	[Export] private bool isMovementEnabled;
	[Export] private bool onlyAttackInRange;
	[Export] private bool despawnOnRangeExit;
	/// <summary> How should this skeleton attack? </summary>
	[Export] private AttackType attackType;
	private enum AttackType
	{
		Spin,
		Overhead,
		Disabled,
	}

	private bool isHurtboxInteraction;

	/// <summary> Timer to keep track of state. </summary>
	private float stateTimer;

	/// <summary> How long should the skeleton stay in an attacking state? </summary>
	[Export] private float attackLength = 1f;
	[Export] private Curve attackDelayCurve;
	/// <summary> Is the enemy currently attacking? </summary>
	private bool isAttacking;
	public void SetAttackStatus(bool value) => isAttacking = value;

	private bool isImpededByWall;
	private float wallCastTimer;
	private float movementSpeed;
	private Vector3 movementDirection;
	private Vector3 homePosition;
	private readonly float WalkSpeed = 3f;
	private readonly float MaxMovementSpeed = 5f;
	private readonly float MovementTrackingSmoothing = 20f;
	private readonly float MovementTraction = 40f;
	private readonly float MovementFriction = 120f;
	private readonly float StrikeRangeSquared = 9f;
	private readonly float WallCastInterval = .2f;

	/// <summary> Keeps track of whether the skeleton has been spawned before. </summary>
	private bool wasSpawned;
	/// <summary> How long to stay shattered when isImmortal is true. </summary>
	private readonly float ImmortalRespawnTime = 1.5f;

	private AnimationNodeStateMachinePlayback AnimationState => AnimationTree.Get(AnimationPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly string AnimationPlayback = "parameters/playback";
	private readonly string SpawnAnimation = "spawn";
	private readonly string OverheadAnimation = "overhead";
	private readonly string IdleAnimation = "idle";
	private readonly string WalkAnimation = "walk";
	private readonly string AttackStartAnimation = "attack-start";
	private readonly string AttackStopAnimation = "attack-stop";
	private readonly string ShatterResetAnimation = "shatter-reset";
	private readonly string ResetAnimation = "RESET";
	private readonly string DefeatAnimation = "defeat";
	private readonly string DamageAnimation = "damage";

	protected override void SetUp()
	{
		if (Engine.IsEditorHint())
			return;

		base.SetUp();
		AnimationTree.Active = true;
		homePosition = GlobalPosition;
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

		if (SpawnMode == SpawnModes.Always)
			return;

		AnimationState.Start(ShatterResetAnimation);
	}

	protected override void EnterRange()
	{
		if (wasSpawned && !IsActive)
		{
			Spawn();
			return;
		}

		base.EnterRange();
	}

	protected override void ExitRange()
	{
		if (!IsActive)
			return;

		if (!despawnOnRangeExit)
			return;

		CallDeferred(MethodName.Deactivate, false);
	}

	protected override void Spawn()
	{
		wasSpawned = true;

		if (IsActive)
			return; // Already spawned

		IsActive = true;
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
			ProcessMovement();

			if (isAttacking)
			{
				if (attackType == AttackType.Spin && IsStateFinished())
					FinishAttack();

				return;
			}

			if (!isMovementEnabled)
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
		if (attackType == AttackType.Disabled)
			return;

		if (onlyAttackInRange && Player.GlobalPosition.DistanceSquaredTo(GlobalPosition) > StrikeRangeSquared)
			return;

		AnimationState.Travel(attackType == AttackType.Overhead ? OverheadAnimation : AttackStartAnimation);
		stateTimer = attackLength;
	}

	private void FinishAttack()
	{
		AnimationState.Travel(AttackStopAnimation);
		stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); // Queue next attack
	}

	private void ProcessMovement()
	{
		if (!isMovementEnabled)
			return;

		wallCastTimer = Mathf.MoveToward(wallCastTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(wallCastTimer) && (!Mathf.IsZeroApprox(movementSpeed) || isImpededByWall))
		{
			wallCastTimer = WallCastInterval;
			RaycastHit wallHit = this.CastRay(GlobalPosition + Vector3.Up * 0.2f, Player.GlobalPosition - GlobalPosition, Runtime.Instance.environmentMask);
			isImpededByWall = wallHit && wallHit.collidedObject.IsInGroup("wall");
		}

		float homeDistanceSquared = rangeOverride * rangeOverride;
		bool leftHome = GlobalPosition.DistanceSquaredTo(homePosition) > homeDistanceSquared;
		if (leftHome) // Allow skeleton to walk back towards player when player is close to home
			leftHome = Player.GlobalPosition.DistanceSquaredTo(homePosition) > homeDistanceSquared;

		if (attackType == AttackType.Spin)
		{
			// Only move when spinning
			if (isAttacking && !leftHome && !isImpededByWall)
				movementSpeed = Mathf.MoveToward(movementSpeed, MaxMovementSpeed, MovementTraction * PhysicsManager.physicsDelta);
			else
				movementSpeed = Mathf.MoveToward(movementSpeed, 0f, MovementFriction * PhysicsManager.physicsDelta);
		}
		else
		{
			bool isInStrikeRange = GlobalPosition.RemoveVertical().DistanceSquaredTo(Player.GlobalPosition.RemoveVertical()) <= StrikeRangeSquared;
			if (isAttacking || isInStrikeRange || leftHome || isImpededByWall)
			{
				movementSpeed = Mathf.MoveToward(movementSpeed, 0f, MovementFriction * PhysicsManager.physicsDelta);

				if (!isAttacking && AnimationState.GetCurrentNode() == WalkAnimation)
					AnimationState.Travel(IdleAnimation);
			}
			else
			{
				movementSpeed = Mathf.MoveToward(movementSpeed, WalkSpeed, MovementTraction * PhysicsManager.physicsDelta);

				if (AnimationState.GetCurrentNode() == IdleAnimation)
					AnimationState.Travel(WalkAnimation);
			}
		}

		ApplyMovement();
	}

	private void ApplyMovement()
	{
		if (!isAttacking)
			ProcessRotation(Player.GlobalPosition, MovementTrackingSmoothing);

		if (Mathf.IsZeroApprox(movementSpeed))
			return;

		// Move towards player
		GlobalTranslate(Root.Forward() * movementSpeed * PhysicsManager.physicsDelta);

		RaycastHit groundHit = this.CastRay(GlobalPosition + Vector3.Up, Vector3.Down * 2f, Runtime.Instance.environmentMask);
		GlobalPosition = groundHit.point;

		// TODO Wall Checks?
	}

	protected override void Defeat()
	{
		Deactivate(true);

		if (isImmortal)
		{
			currentHealth = 0;
			Player.Camera.SetLockonTarget(null);
			Player.Lockon.ResetLockonTarget();

			CheckLightSpeedAttack();
			EmitSignal(SignalName.Defeated);
		}
		else
		{
			base.Defeat();
		}

		if (isImmortal)
			stateTimer = ImmortalRespawnTime;
	}

	private void Deactivate(bool isDefeated)
	{
		IsActive = false;
		AnimationState.Start((!isDefeated || isImmortal) ? DamageAnimation : DefeatAnimation);
		SetHitboxStatus(false);
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