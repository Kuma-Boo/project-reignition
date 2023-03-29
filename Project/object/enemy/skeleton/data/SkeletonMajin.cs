using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Special enemy found in the Skeleton Dome.
	/// </summary>
	public partial class SkeletonMajin : Enemy
	{
		[Export]
		/// <summary> Put the majin into a skeleton pile and respawn instead of being defeated. </summary>
		private bool isImmortal;
		private bool isHurtboxInteraction;

		/// <summary> Timer to keep track of state. </summary>
		private float stateTimer;

		[Export]
		private Curve attackDelayCurve;
		/// <summary> Is the enemy currently attacking? </summary>
		private bool isAttacking;
		public void SetAttackStatus(bool value) => isAttacking = value;

		/// <summary> Keeps track of whether the skeleton's range was already triggered. </summary>
		private bool wasSpawned;
		/// <summary> How long to stay shattered when isImmortal is true. </summary>
		private const float IMMORTAL_RESPAWN_TIME = 1.5f;

		private AnimationNodeStateMachinePlayback AnimationState => animationTree.Get(ANIMATION_STATE_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName ANIMATION_STATE_PLAYBACK = "parameters/playback";
		private readonly StringName SPAWN_ANIMATION = "spawn";
		private readonly StringName ATTACK_ANIMATION = "attack";
		private readonly StringName SHATTER_RESET_ANIMATION = "shatter-reset";
		private readonly StringName RESET_ANIMATION = "RESET";
		private readonly StringName DEFEAT_ANIMATION = "defeat";
		private readonly StringName DAMAGE_ANIMATION = "damage";

		protected override void SetUp()
		{
			base.SetUp();
			animationTree.Active = true;
		}


		protected override void UpdateInteraction()
		{
			if (Character.Lockon.IsBouncingLockoutActive || !IsHitboxEnabled) return;

			if (isAttacking && !isHurtboxInteraction)
				Character.StartKnockback();
			else
				base.UpdateInteraction();
		}

		public override void Respawn()
		{
			base.Respawn();

			wasSpawned = false;

			if (spawnMode != SpawnModes.Always)
				Spawn();
			else
			{
				AnimationState.Start(SHATTER_RESET_ANIMATION);
				animationPlayer.Play(RESET_ANIMATION);
			}
		}

		protected override void EnterRange()
		{
			if (wasSpawned || spawnMode == SpawnModes.Signal) return;
			Spawn();
		}

		protected override void Spawn()
		{
			if (IsActive) return; //Already spawned

			IsActive = true;
			wasSpawned = true;

			currentHealth = maxHealth; //Reset health

			animationPlayer.Play(SPAWN_ANIMATION);
			AnimationState.Travel(SPAWN_ANIMATION);

			stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); //Queue next attack
			base.Spawn();
		}

		protected override void UpdateEnemy()
		{
			if (IsActive)
			{
				TrackPlayer();
				root.Rotation = new Vector3(root.Rotation.X, currentRotation, root.Rotation.Z);

				if (IsHitboxEnabled && !isAttacking) //Update attack
				{
					stateTimer = Mathf.MoveToward(stateTimer, 0, PhysicsManager.physicsDelta);
					if (Mathf.IsZeroApprox(stateTimer))
					{
						AnimationState.Travel(ATTACK_ANIMATION);
						animationPlayer.Play(ATTACK_ANIMATION);
						stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); //Queue next attack
					}
				}
			}
			else if (wasSpawned && isImmortal) //Revive
			{
				stateTimer = Mathf.MoveToward(stateTimer, 0, PhysicsManager.physicsDelta);
				if (Mathf.IsZeroApprox(stateTimer))
					Spawn();
			}
		}

		protected override void Defeat()
		{
			base.Defeat();

			IsActive = false;
			AnimationState.Travel(isImmortal ? DAMAGE_ANIMATION : DEFEAT_ANIMATION);
			animationPlayer.Play(isImmortal ? DAMAGE_ANIMATION : DEFEAT_ANIMATION);

			Character.MovementAngle = Character.PathFollower.ForwardAngle; //More consistent direction

			if (isImmortal)
				stateTimer = IMMORTAL_RESPAWN_TIME;
		}

		public void OnHurtboxEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			interactionCounter++;
			isHurtboxInteraction = true;
		}

		public void OnHurtboxExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			interactionCounter--;
			isHurtboxInteraction = false;
		}
	}
}