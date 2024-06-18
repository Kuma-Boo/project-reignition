using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
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

	[Export]
	private PackedScene seed;
	private int seedIndex;
	private const int MaxSeedCount = 3;
	/// <summary> Only three seeds are ever spawned at a time. </summary>
	private readonly Seed[] seedPool = new Seed[MaxSeedCount];

	/// <summary> Flower is only considered be damaged while not in a passive state. </summary>
	private bool IsOpen => currentState != State.Passive;
	/// <summary> Returns true if the flower's stagger animation is active. </summary>
	private bool IsStaggered => (bool)animationTree.Get(StaggerActive);

	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName ShowState = "show";
	private readonly StringName PassiveState = "passive";
	private readonly StringName HideState = "hide";

	private readonly StringName AttackTrigger = "parameters/attack_trigger/request";
	private readonly StringName StaggerTrigger = "parameters/stagger_trigger/request";
	private readonly StringName StaggerActive = "parameters/stagger_trigger/active";

	/// <summary> Reference to AnimationTree's defeat_transition node. Required to change transition fade. </summary>
	private AnimationNodeTransition defeatTransition;
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";


	protected override void SetUp()
	{
		animationTree.Active = true;
		defeatTransition = (animationTree.TreeRoot as AnimationNodeBlendTree).GetNode("defeat_transition") as AnimationNodeTransition;

		for (int i = 0; i < MaxSeedCount; i++) // Initialize seeds
			seedPool[i] = seed.Instantiate<Seed>();

		base.SetUp();
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
		defeatTransition.XfadeTime = 0;
		animationTree.Set(DefeatTransition, DisabledConstant);
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(StaggerTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		// Reset to passive state
		currentState = State.Passive;
		animationTree.Set(StateTransition, PassiveState);

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
		if (!Character.Skills.IsAttacking && !Character.Lockon.IsHomingAttacking) // Skill damage
		{
			if (IsOpen)
				StartStaggerState();

			return;
		}


		if (!IsOpen)
		{
			if (Character.Lockon.IsHomingAttacking)
				Character.Lockon.StartBounce(false);

			return;
		}

		base.UpdateInteraction();
	}


	public override void TakePlayerDamage()
	{
		base.TakePlayerDamage();

		if (IsDefeated)
		{
			StartDefeatState();
			return;
		}

		StartStaggerState();
	}


	protected override void UpdateEnemy()
	{
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
		root.Rotation = new Vector3(root.Rotation.X, currentRotation, root.Rotation.Z); // Apply rotation
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
				if (stateTimer >= passiveLength)
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
		animationTree.Set(StateTransition, HideState);
		EmitSignal(SignalName.Passive);
	}


	private void StartAggressiveState()
	{
		stateTimer = 0;
		currentState = State.PreAttack;
		animationTree.Set(StateTransition, ShowState);
		EmitSignal(SignalName.Aggressive);
	}


	private void StartAttackState()
	{
		stateTimer = 0;
		currentState = State.Attack;
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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

		Vector3 targetOffset = hurtbox.GlobalPosition - Character.CenterPosition;
		targetOffset -= Vector3.Up * .4f; // Aim slightly higher so seeds avoid hitting the ground
		seedPool[seedIndex].LookAtFromPosition(hurtbox.GlobalPosition, hurtbox.GlobalPosition + targetOffset, Vector3.Up);
		seedPool[seedIndex].Spawn();

		seedIndex++; // Increment counter
	}


	private void StopAttackState()
	{
		seedIndex = 0;
		stateTimer = 0;
		currentState = State.PostAttack;
	}


	private void StartStaggerState()
	{
		animationTree.Set(StaggerTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		if (currentState != State.Attack)
		{
			EmitSignal(SignalName.Stagger);
			return;
		}

		StopAttackState();
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}


	private void StartDefeatState() => animationTree.Set(DefeatTransition, EnabledConstant);
}
