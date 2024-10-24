using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
[Tool]
public partial class FlowerMajin : Enemy
{
	[Signal]
	public delegate void PassiveEventHandler();
	[Signal]
	public delegate void AggressiveEventHandler();
	[Signal]
	public delegate void StaggerEventHandler();
	[Signal]
	public delegate void AttackEventHandler();
	[Signal]
	public delegate void DeflectEventHandler();

	/// <summary> Skip passive phase when activated? </summary>
	[ExportGroup("Enemy Settings")]
	[Export]
	private bool skipPassive;
	/// <summary> Don't attack. NOTE: This will lead the flower majin to never move onto the PostAttack state. </summary>
	[Export]
	private bool disableAttacking;
	/// <summary> How long to remain passive. </summary>
	[Export(PropertyHint.Range, "0, 5, .1")]
	private float passiveLength;
	/// <summary> How long to wait before firing seeds after turning aggressive. </summary>
	[Export(PropertyHint.Range, "0, 5, .1")]
	private float preAttackLength;
	/// <summary> How long to wait after firing seeds before turning passive. </summary>
	[Export(PropertyHint.Range, "0, 5, .1")]
	private float postAttackLength;

	/// <summary> Tracks how long FlowerMajin has been in the current state. </summary>
	private float stateTimer;
	private State currentState;
	private enum State
	{
		Passive,
		PreAttack,
		Attack,
		PostAttack
	}

	/// <summary> Allow the player to force the flower majin out of it's passive state? </summary>
	[Export]
	private bool weakDefense;
	[Export]
	private PackedScene seed;
	private int seedIndex;
	private const int MaxSeedCount = 3;
	/// <summary> Only three seeds are ever spawned at a time. </summary>
	private readonly Seed[] seedPool = new Seed[MaxSeedCount];

	/// <summary> Flower is only considered be damaged while not in a passive state. </summary>
	private bool IsOpen => currentState != State.Passive;
	/// <summary> Returns true if the flower's stagger animation is active. </summary>
	private bool IsStaggered => (bool)AnimationTree.Get(HitActive);

	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName ShowState = "show";
	private readonly StringName AggressiveState = "aggressive";
	private readonly StringName PassiveState = "passive";
	private readonly StringName HideState = "hide";

	private readonly StringName AttackTrigger = "parameters/attack_trigger/request";
	private readonly StringName HitTrigger = "parameters/hit_trigger/request";
	private readonly StringName HitActive = "parameters/hit_trigger/active";
	private readonly StringName HitTransition = "parameters/hit_transition/transition_request";
	private readonly StringName StaggerState = "stagger";
	private readonly StringName BoopState = "boop";

	/// <summary> Reference to AnimationTree's defeat_transition node. Required to change transition fade. </summary>
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";

	protected override void SetUp()
	{
		if (Engine.IsEditorHint()) return; // In Editor

		for (int i = 0; i < MaxSeedCount; i++) // Initialize seeds
			seedPool[i] = seed.Instantiate<Seed>();

		base.SetUp();
		AnimationTree.Active = true;
	}

	public override void Unload()
	{
		for (int i = 0; i < seedPool.Length; i++) // Clear memory
			seedPool[i].QueueFree();

		base.Unload();
	}

	protected override void Spawn()
	{
		if (skipPassive && !IsOpen) // Skip passive phase
			StartAggressiveState();

		base.Spawn();
	}

	public override void Respawn()
	{
		base.Respawn();

		// Reset animations
		AnimationTree.Set(DefeatTransition, DisabledConstant);
		AnimationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(HitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		// Reset to passive state
		currentState = State.Passive;
		AnimationTree.Set(StateTransition, PassiveState);

		// Reset variables
		stateTimer = 0;
		rotationVelocity = 0;

		// Remove seeds
		seedIndex = 0;
		for (int i = 0; i < MaxSeedCount; i++)
		{
			if (seedPool[i].IsInsideTree())
				seedPool[i].GetParent().CallDeferred(MethodName.RemoveChild, seedPool[i]);
		}
	}

	protected override void UpdateInteraction()
	{
		if (!IsOpen)
		{
			if (Player.IsHomingAttacking)
			{
				Player.StartBounce(false);
				UpdateLockon();

				if (weakDefense)
					StartStaggerState();
				else
					EmitSignal(SignalName.Deflect);
			}

			if (Player.AttackState == PlayerController.AttackStates.None ||
				(Player.AttackState == PlayerController.AttackStates.Weak && !weakDefense))
			{
				return;
			}
		}

		if (!IsStaggered && Player.AttackState == PlayerController.AttackStates.None)
		{
			StartStaggerState();
			Player.StartBounce(false);
			UpdateLockon();
			return;
		}

		base.UpdateInteraction();
	}

	public override void TakeDamage(int amount = -1)
	{
		base.TakeDamage(amount);

		if (!IsDefeated)
			StartStaggerState();
	}

	protected override void Defeat()
	{
		AnimationTree.Set(DefeatTransition, EnabledConstant);
		SetHitboxStatus(false);
		base.Defeat();
	}

	protected override void UpdateEnemy()
	{
		if (Engine.IsEditorHint()) return; // In Editor
		if (IsDefeated) return;

		if (IsInRange || currentState != State.Passive)
		{
			UpdateRotation();
			UpdateState();
		}
	}

	private void UpdateRotation()
	{
		if (!IsOpen) return;

		// TODO Update movement

		// Rotate towards the player
		TrackPlayer();
		Root.Rotation = new Vector3(Root.Rotation.X, currentRotation, Root.Rotation.Z); // Apply rotation
	}

	private void UpdateState()
	{
		if (currentState == State.Attack)
			return; // Let the attack finish

		if (IsStaggered)
			return; // Let stagger finish

		stateTimer += PhysicsManager.physicsDelta;
		switch (currentState)
		{
			case State.Passive:
				if (!Mathf.IsZeroApprox(passiveLength) && stateTimer >= passiveLength)
					StartAggressiveState();
				break;
			case State.PreAttack:
				if (stateTimer >= preAttackLength)
				{
					if (IsInRange && !disableAttacking)
						StartAttackState();
					else // Player has left range; return to passive state
						StartPassiveState();
				}
				break;
			case State.PostAttack:
				if (stateTimer >= postAttackLength)
					StartPassiveState();
				break;
		}
	}

	private void StartPassiveState()
	{
		stateTimer = 0;
		currentState = State.Passive;
		AnimationTree.Set(StateTransition, HideState);
		EmitSignal(SignalName.Passive);
	}

	private void StartAggressiveState()
	{
		stateTimer = 0;
		currentState = State.PreAttack;
		AnimationTree.Set(StateTransition, ShowState);
		EmitSignal(SignalName.Aggressive);
	}

	private void StartAttackState()
	{
		stateTimer = 0;
		currentState = State.Attack;
		AnimationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		EmitSignal(SignalName.Attack);
	}

	private void IncrementAttack()
	{
		if (seedIndex < MaxSeedCount) // Fire another seed
		{
			StartAttackState();
			return;
		}

		StopAttackState();
	}

	/// <summary> Launches a seed at the player. </summary>
	private void FireAttack()
	{
		if (!seedPool[seedIndex].IsInsideTree()) // Add seeds to the scene tree
			GetTree().Root.AddChild(seedPool[seedIndex]);

		Vector3 targetOffset = Hurtbox.GlobalPosition - Player.CenterPosition;
		targetOffset -= Vector3.Up * .4f; // Aim slightly higher so seeds avoid hitting the ground
		seedPool[seedIndex].LookAtFromPosition(Hurtbox.GlobalPosition, Hurtbox.GlobalPosition + targetOffset, Vector3.Up);
		seedPool[seedIndex].Spawn();

		seedIndex++; // Increment counter
	}

	private void StopAttackState()
	{
		if (!IsOpen)
			AnimationTree.Set(StateTransition, AggressiveState);

		seedIndex = 0;
		stateTimer = 0;
		currentState = State.PostAttack;
	}

	private void StartStaggerState()
	{
		if (Player.AttackState == PlayerController.AttackStates.None)
			AnimationTree.Set(HitTransition, BoopState);
		else
			AnimationTree.Set(HitTransition, StaggerState);
		AnimationTree.Set(HitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		AnimationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		if (currentState == State.Passive || currentState == State.Attack)
			StopAttackState();
		EmitSignal(SignalName.Stagger);
	}
}
