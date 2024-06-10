using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
public partial class FlowerMajin : Enemy
{
	[Signal]
	public delegate void OnPassiveEventHandler();
	[Signal]
	public delegate void OnAggressiveEventHandler();


	/// <summary> Skip passive phase when activated? </summary>
	[ExportGroup("Enemy Settings")]
	[Export]
	private bool attackInstantly;
	/// <summary> How long to remain passive. </summary>
	[Export]
	private float passiveLength;
	/// <summary> How long to wait before firing seeds after turning aggressive. </summary>
	[Export]
	private float preAttackLength;
	/// <summary> How long to wait after firing seeds before turning passive. </summary>
	[Export]
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
	private PackedScene seedScene;
	private int seedIndex;
	private const int MaxSeedCount = 3;
	/// <summary> Only three seeds are ever spawned at a time. </summary>
	private readonly Seed[] seedPool = new Seed[MaxSeedCount];

	/// <summary> Flower is only considered be damaged while not in a passive state. </summary>
	private bool IsOpen => currentState != State.Passive;
	/// <summary> Returns true if the flower's stagger animation is active. </summary>
	private bool IsStaggered => (bool)animationTree.Get(staggerActive);

	private readonly StringName stateTransition = "parameters/state_transition/transition_request";
	private readonly StringName showState = "show";
	private readonly StringName passiveState = "passive";
	private readonly StringName hideState = "hide";

	private readonly StringName attackTrigger = "parameters/attack_trigger/request";
	private readonly StringName staggerTrigger = "parameters/stagger_trigger/request";
	private readonly StringName staggerActive = "parameters/stagger_trigger/active";

	/// <summary> Reference to AnimationTree's defeat_transition node. Required to change transition fade. </summary>
	private AnimationNodeTransition defeatTransitionNode;
	private readonly StringName defeatTransition = "parameters/defeat_transition/transition_request";
	private readonly StringName enabledConstant = "enabled";
	private readonly StringName disabledConstant = "disabled";


	protected override void SetUp()
	{
		animationTree.Active = true;
		defeatTransitionNode = ((AnimationNodeBlendTree)animationTree.TreeRoot).GetNode("defeat_transition") as AnimationNodeTransition;

		for (var i = 0; i < MaxSeedCount; i++) // Initialize seeds
			seedPool[i] = seedScene.Instantiate<Seed>();

		base.SetUp();
	}


	public override void Unload()
	{
		foreach (var seed in seedPool)
			seed.QueueFree();

		base.Unload();
	}


	protected override void Spawn()
	{
		if (attackInstantly && !IsOpen) // Skip passive phase
			StartAggressiveState();

		base.Spawn();
	}


	public override void Respawn()
	{
		base.Respawn();

		// Reset animations
		defeatTransitionNode.XfadeTime = 0;
		animationTree.Set(defeatTransition, disabledConstant);
		animationTree.Set(attackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(staggerTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		// Reset to passive state
		currentState = State.Passive;
		animationTree.Set(stateTransition, passiveState);

		// Reset variables
		stateTimer = 0;
		rotationVelocity = 0;

		// Remove seeds
		seedIndex = 0;
		for (var i = 0; i < MaxSeedCount; i++)
		{
			if (seedPool[i].IsInsideTree())
				seedPool[i].GetParent().CallDeferred(MethodName.RemoveChild, seedPool[i]);
		}
	}


	protected override void UpdateInteraction()
	{
		if (!Character.Skills.IsAttacking && !Character.Lockon.IsHomingAttacking)
		{
			if (IsOpen)
				StartStaggerState();

			Character.Lockon.StartBounce(false);
			return;
		}

		if (!IsOpen)
			return;

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
					if (IsInRange)
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
		animationTree.Set(stateTransition, hideState);
	}


	private void StartAggressiveState()
	{
		stateTimer = 0;
		currentState = State.PreAttack;
		animationTree.Set(stateTransition, showState);
	}


	private void StartAttackState()
	{
		stateTimer = 0;
		currentState = State.Attack;
		animationTree.Set(attackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
		animationTree.Set(staggerTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		if (currentState != State.Attack)
			return;

		StopAttackState();
		animationTree.Set(attackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}


	private void StartDefeatState() => animationTree.Set(defeatTransition, enabledConstant);
}
