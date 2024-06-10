using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
	public partial class FlowerMajin : Enemy
	{
		[ExportGroup("Enemy Settings")]
		[Export]
		/// <summary> Skip passive phase when activated? </summary>
		private bool attackInstantly;
		[Export]
		/// <summary> How long to remain passive. </summary>
		private float passiveLength;
		[Export]
		/// <summary> How long to wait before firing seeds after turning aggressive. </summary>
		private float preAttackLength;
		[Export]
		/// <summary> How long to wait after firing seeds before turning passive. </summary>
		private float postAttackLength;
		/// <summary> How much extra time to wait for when staggered. </summary>
		private const float STAGGER_LENGTH = .5f;

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
		private const int MAX_SEED_COUNT = 3;
		/// <summary> Only three seeds are ever spawned at a time. </summary>
		private readonly Seed[] seedPool = new Seed[MAX_SEED_COUNT];

		/// <summary> Flower is only considered be damaged while not in a passive state. </summary>
		private bool IsOpen => currentState != State.Passive;
		/// <summary> Returns true if the flower's stagger animation is active. </summary>
		private bool IsStaggered => (bool)animationPlayer.Get(STAGGER_ACTIVE);

		private readonly StringName STATE_TRANSITION = "parameters/state_transition/transition_request";
		private readonly StringName SHOW_STATE = "show";
		private readonly StringName PASSIVE_STATE = "passive";
		private readonly StringName HIDE_STATE = "hide";

		private readonly StringName ATTACK_TRIGGER = "parameters/attack_trigger/request";
		private readonly StringName STAGGER_TRIGGER = "parameters/stagger_trigger/request";
		private readonly StringName STAGGER_ACTIVE = "parameters/stagger_trigger/active";

		/// <summary> Reference to AnimationTree's defeat_transition node. Required to change transition fade. </summary>
		private AnimationNodeTransition defeatTransition;
		private readonly StringName DEFEAT_TRANSITION = "parameters/defeat_transition/transition_request";
		private readonly StringName ENABLED_CONSTANT = "enabled";
		private readonly StringName DISABLED_CONSTANT = "disabled";


		protected override void SetUp()
		{
			animationTree.Active = true;
			defeatTransition = (animationTree.TreeRoot as AnimationNodeBlendTree).GetNode("defeat_transition") as AnimationNodeTransition;

			for (int i = 0; i < MAX_SEED_COUNT; i++) // Initialize seeds
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
			if (attackInstantly && !IsOpen) // Skip passive phase
				StartAggressiveState();

			base.Spawn();
		}


		public override void Respawn()
		{
			base.Respawn();

			// Snap out of the defeat animation
			defeatTransition.XfadeTime = 0;
			animationTree.Set(DEFEAT_TRANSITION, DISABLED_CONSTANT);

			// Reset to passive state
			currentState = State.Passive;
			animationTree.Set(STATE_TRANSITION, PASSIVE_STATE);

			// Reset variables
			stateTimer = 0;
			rotationVelocity = 0;

			// Remove seeds
			seedIndex = 0;
			for (int i = 0; i < MAX_SEED_COUNT; i++)
			{
				if (seedPool[i].IsInsideTree())
					seedPool[i].GetParent().CallDeferred(MethodName.RemoveChild, seedPool[i]);
			}
		}


		protected override void UpdateInteraction()
		{
			if (!IsOpen && Character.ActionState == CharacterController.ActionStates.JumpDash)
			{
				Character.Lockon.StartBounce();
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
			animationTree.Set(STATE_TRANSITION, HIDE_STATE);
		}


		private void StartAggressiveState()
		{
			stateTimer = 0;
			currentState = State.PreAttack;
			animationTree.Set(STATE_TRANSITION, SHOW_STATE);
		}


		private void StartAttackState()
		{
			stateTimer = 0;
			currentState = State.Attack;
			animationTree.Set(ATTACK_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}


		private void IncrementAttack()
		{
			if (seedIndex < MAX_SEED_COUNT) // Fire another seed
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
			animationTree.Set(STAGGER_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			if (currentState == State.Attack)
				StopAttackState();
		}


		private void StartDefeatState() => animationTree.Set(DEFEAT_TRANSITION, ENABLED_CONSTANT);
	}
}
