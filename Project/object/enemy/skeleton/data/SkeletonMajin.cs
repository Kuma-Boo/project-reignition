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
		private const float ImmortalRespawnTime = 1.5f;

		private AnimationNodeStateMachinePlayback AnimationState => AnimationTree.Get(AnimationPlayback).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName AnimationPlayback = "parameters/playback";
		private readonly StringName SpawnAnimation = "spawn";
		private readonly StringName AttackAnimation = "attack";
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

			if (SpawnMode != SpawnModes.Always)
			{
				Spawn();
			}
			else
			{
				AnimationState.Start(ShatterResetAnimation);
				AnimationPlayer.Play(ResetAnimation);
			}
		}

		protected override void EnterRange()
		{
			if (wasSpawned || SpawnMode == SpawnModes.Signal) return;
			Spawn();
		}

		protected override void Spawn()
		{
			if (IsActive) return; //Already spawned

			IsActive = true;
			wasSpawned = true;

			currentHealth = maxHealth; //Reset health

			AnimationPlayer.Play(SpawnAnimation);
			AnimationState.Travel(SpawnAnimation);

			stateTimer = attackDelayCurve.Sample(Runtime.randomNumberGenerator.Randf()); //Queue next attack
			base.Spawn();
		}

		protected override void UpdateEnemy()
		{
			if (IsActive)
			{
				UpdateRotation(Player.GlobalPosition);

				if (IsHitboxEnabled && !isAttacking) //Update attack
				{
					stateTimer = Mathf.MoveToward(stateTimer, 0, PhysicsManager.physicsDelta);
					if (Mathf.IsZeroApprox(stateTimer))
					{
						AnimationState.Travel(AttackAnimation);
						AnimationPlayer.Play(AttackAnimation);
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
			AnimationState.Travel(isImmortal ? DamageAnimation : DefeatAnimation);
			AnimationPlayer.Play(isImmortal ? DamageAnimation : DefeatAnimation);

			Player.MovementAngle = Player.PathFollower.ForwardAngle; //More consistent direction

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
}