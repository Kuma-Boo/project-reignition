using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay
{
	public partial class DinoTrio : PathFollow3D
	{
		private static List<DinoTrio> registeredDinoTrio = new List<DinoTrio>();
		private static float playerHitTimer; // Dinos will wait a bit after hitting the player
		private static float attackTimer; // Timer to determine when to attack
		private static float playerProgress; // Player's offset on the curve

		private bool isMainDino; // Is this the first registered dino?

		[Export]
		private Path3D path;

		[ExportGroup("Movement")]
		[Export]
		private float traction;
		[Export]
		private float friction;
		[Export]
		private float preferredOffset; // Target offset from player
		[Export]
		private float attackOffset;
		[Export(PropertyHint.Range, "0, 1")]
		private float rubberbandingStrength;

		private float moveSpeed; // Base movespeed (without rubberbanding)
		private float rubberbandingSpeed; // Rubberbanding speed

		// Total movespeed (currentMoveSpeed + rubberbanding)
		private float TotalMoveSpeed
		{
			get
			{
				float spd = (moveSpeed + rubberbandingSpeed);
				if (isAttacking)
					spd *= speedMultiplier;
				if (spd < 2f)
					spd = 0;
				return spd;
			}
		}

		// Used during attacks
		[ExportGroup("Animation")]
		[Export]
		private bool isAttacking;
		[Export]
		private bool isAttackActive;
		[Export(PropertyHint.Range, "0, 5")]
		private float speedMultiplier;

		private CharacterController Character => CharacterController.instance;

		private const float SPEED_DIFFERENCE = 2f; // How much faster the player should be
		private const float PLAYER_HIT_WAIT_TIME = 3f; // How long to wait after hitting the player
		private const float ATTACK_INTERVAL = 3f; // How long to wait between attacks


		public override void _Ready()
		{
			animationTree.Active = true;
			LevelSettings.instance.ConnectRespawnSignal(this);
			Respawn();
		}

		private void Respawn()
		{
			if (isMainDino)
			{
				playerHitTimer = 0;
				attackTimer = ATTACK_INTERVAL;
			}

			Progress = 0;
			moveSpeed = rubberbandingSpeed = 0;
			CancelAttack();
		}

		public override void _EnterTree()
		{
			isMainDino = registeredDinoTrio.Count == 0;

			if (!registeredDinoTrio.Contains(this))
				registeredDinoTrio.Add(this);
		}


		public override void _ExitTree()
		{
			if (registeredDinoTrio.Contains(this))
				registeredDinoTrio.Remove(this);
		}


		public override void _PhysicsProcess(double _)
		{
			if (isMainDino) // Main dino processes extra things
			{
				ProcessPositions();
				ProcessAttacks();
			}

			CalculateMovespeed();
			UpdateAnimations();

			Progress += TotalMoveSpeed * PhysicsManager.physicsDelta;
		}


		private void ProcessPositions()
		{
			Vector3 localPosition = path.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - path.GlobalPosition);
			playerProgress = path.Curve.GetClosestOffset(localPosition);
		}


		private void ProcessAttacks()
		{
			if (!isMainDino) return; // Only the main dino can process attack timer
			if (Character.Camera.IsBehindCamera(GlobalPosition)) return; // Don't attack when off-camera for fairness

			for (int i = 0; i < registeredDinoTrio.Count; i++)
			{
				if (registeredDinoTrio[i].isAttacking) // Don't update timer when a dino is already attacking
					return;
			}

			attackTimer = Mathf.MoveToward(attackTimer, 0, PhysicsManager.physicsDelta);
			if (!Mathf.IsZeroApprox(attackTimer)) return;

			// Calculate which dino attacks
			Vector3 closestPosition = path.GlobalPosition + path.Curve.SampleBaked(playerProgress);
			float targetDeltaPosition = (path.GlobalTransform.Basis.Inverse() * (Character.GlobalPosition - closestPosition)).X;

			float closestDeltaPosition = Mathf.Inf;
			int closestDinoIndex = 0;
			for (int i = 0; i < registeredDinoTrio.Count; i++)
			{
				float deltaPosition = Mathf.Abs(targetDeltaPosition - registeredDinoTrio[i].HOffset);
				if (deltaPosition < closestDeltaPosition)
				{
					closestDinoIndex = i;
					closestDeltaPosition = deltaPosition;
				}
			}

			if (Mathf.Abs(registeredDinoTrio[closestDinoIndex].Progress - playerProgress) > attackOffset) // Too far away to attack
				return;

			registeredDinoTrio[closestDinoIndex].StartAttack();
			attackTimer = ATTACK_INTERVAL; // Reset timer
		}


		private void CalculateMovespeed()
		{
			float deltaProgress = playerProgress - Progress;

			if (!Mathf.IsZeroApprox(playerHitTimer))
			{
				if (isMainDino) // Update timer
					playerHitTimer = Mathf.MoveToward(playerHitTimer, 0, PhysicsManager.physicsDelta);

				moveSpeed = Mathf.MoveToward(moveSpeed, 0, friction * PhysicsManager.physicsDelta);
				rubberbandingSpeed = 0;

				if (Mathf.Abs(deltaProgress) > attackOffset)
					playerHitTimer = 0; // Start chase again
				return;
			}


			// Accelerate
			if (isAttackActive)
				moveSpeed = Mathf.Lerp(moveSpeed, Character.MoveSpeed + Mathf.Abs(deltaProgress), .25f);
			else
			{
				float targetSpeed = Mathf.Clamp(Character.MoveSpeed - SPEED_DIFFERENCE, 0, Character.Skills.GroundSettings.speed);
				if (Mathf.Abs(deltaProgress) > attackOffset)
					targetSpeed = Character.Skills.GroundSettings.speed - SPEED_DIFFERENCE;

				moveSpeed = Mathf.MoveToward(moveSpeed, targetSpeed, traction * PhysicsManager.physicsDelta);


				// Rubberbanding
				rubberbandingSpeed = (deltaProgress - preferredOffset) * rubberbandingStrength;
			}
		}


		[Export]
		private AnimationTree animationTree;
		private AnimationNodeStateMachinePlayback IdleState => animationTree.Get(IDLE_STATE_PLAYBACK).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName ENABLED_CONSTANT = "enabled";
		private readonly StringName DISABLED_CONSTANT = "disabled";

		private readonly StringName IDLE_STATE_PLAYBACK = "parameters/idle_state/playback";
		private readonly StringName IDLE_STATE_PARAMETER = "trio-idle";
		private readonly StringName PAW_STATE_PARAMETER = "trio-fidget-paw";
		private readonly StringName SHAKE_STATE_PARAMETER = "trio-fidget-shake";

		private readonly StringName MOVING_TRANSITION = "parameters/moving_transition/current_state";
		private readonly StringName MOVING_TRANSITION_REQUEST = "parameters/moving_transition/transition_request";

		private readonly StringName MOVEMENT_BLEND_PARAMETER = "parameters/movement_blend/blend_position";
		private readonly StringName MOVEMENT_SPEED_PARAMETER = "parameters/movement_speed/scale";

		private readonly StringName ATTACK_TRIGGER = "parameters/attack_trigger/request";

		private void UpdateAnimations()
		{
			if (Mathf.IsZeroApprox(TotalMoveSpeed))
			{
				if ((string)animationTree.Get(MOVING_TRANSITION) == ENABLED_CONSTANT)
				{
					animationTree.Set(MOVING_TRANSITION_REQUEST, DISABLED_CONSTANT);
					SelectIdleFidget();
				}
			}
			else
			{
				animationTree.Set(MOVING_TRANSITION_REQUEST, ENABLED_CONSTANT);
				animationTree.Set(MOVEMENT_BLEND_PARAMETER, Character.Skills.GroundSettings.GetSpeedRatioClamped(TotalMoveSpeed));
				animationTree.Set(MOVEMENT_SPEED_PARAMETER, .6f + Character.Skills.GroundSettings.GetSpeedRatio(TotalMoveSpeed) * 1.4f);
			}
		}


		public void SelectIdleFidget() => IdleState.Travel(Runtime.randomNumberGenerator.Randf() > .5f ? PAW_STATE_PARAMETER : SHAKE_STATE_PARAMETER);


		public void StartAttack() => animationTree.Set(ATTACK_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		public void CancelAttack() => animationTree.Set(ATTACK_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Abort);


		public void OnEntered(Area3D area)
		{
			if (!area.IsInGroup("player")) return;

			CharacterController.instance.StartKnockback(new CharacterController.KnockbackSettings()
			{
				knockForward = true, // Always knock forward
				ignoreInvincibility = true, // Always knockback the player
				disableDamage = CharacterController.instance.IsInvincible, // Don't hurt player during invincibility
				overrideKnockbackSpeed = true,
				knockbackSpeed = Mathf.Max(TotalMoveSpeed * .5f, 10f),
				overrideKnockbackHeight = true,
				knockbackHeight = 2
			});

			playerHitTimer = PLAYER_HIT_WAIT_TIME;
			attackTimer = ATTACK_INTERVAL; // Reset attack timer
		}
	}
}
