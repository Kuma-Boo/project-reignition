using Godot;
using Godot.Collections;
using Project.Core;
using Project.CustomNodes;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Bosses
{
	/// <summary> Controls the first boss of the game, the Sand Scorpion. </summary>
	/*
	Behaviour:
	Runs away from the player, unless the player is far away and backstepping, in which case Sand Scorpion will walk towards the player to maintain distance.
	-Remains idle until the player moves-
	When far away, shoot missiles. When really far, one of these missiles locks onto the player's current position, to force the player to keep moving.
	When close, attack depending on which side the player is on. Attack Pattern: Two hit light attacks, heavy attack.
	The player can skip the second tail attack by exiting close range then re-entering

	ITEMS:
	After hitting the trigger point (Excluding the first time), change the active itemset to the next lap. Lap 4 doesn't have any items.
	*/
	public partial class SandScorpion : Node3D
	{
		[Export]
		/// <summary> Boss's path follower. </summary>
		private PathFollow3D bossPathFollower;
		[Export]
		private Camera3D cutsceneCamera;

		private enum AttackState
		{
			INACTIVE,
			WINDUP,
			STRIKE,
			RECOVERY,
		}
		[Export]
		/// <summary> Phase of the boss's current attack. </summary>
		private AttackState attackState;

		private enum FightState
		{
			Introduction,
			Waiting,
			Active,
			Defeated
		}
		/// <summary> Is the boss being processed? </summary>
		private FightState fightState;
		private readonly float STARTING_POSITION = 60;

		private CharacterController Character => CharacterController.instance;
		private CharacterPathFollower PathFollower => Character.PathFollower;


		[ExportGroup("Dialogs")]
		[Export]
		private Array<DialogTrigger> hitDialogs = new();
		[Export]
		private Array<DialogTrigger> hintDialogs = new();
		private int[] dialogFlags = {
			0, // Light attack hint flag
			0, // Heavy attack hint flags
			0 // Flying Eye flag
		};

		private bool PlayHint(int index)
		{
			if (!hintDialogs[index].Visible) return false; // Already played that hint

			hintDialogs[index].Activate();
			hintDialogs[index].Visible = false; // Use visibility as a flag to keep track of played hints
			return true;
		}

		[ExportGroup("Animation")]
		[Export]
		private AnimationTree rootAnimationTree;
		[Export]
		private AnimationTree lTailAnimationTree;
		[Export]
		private AnimationTree rTailAnimationTree;
		[Export]
		private AnimationTree flyingEyeAnimationTree;
		[Export]
		private AnimationPlayer eventAnimator; // Extra animator that manages stuff like damage flashing, hitboxes, etc
		private readonly StringName DISABLED_STATE = "disabled";
		private readonly StringName ENABLED_STATE = "enabled";
		private readonly StringName INTRO_PARAMETER = "parameters/intro_trigger/request";
		private readonly StringName DAMAGE_PARAMETER = "parameters/damage_trigger/request";
		private readonly StringName DEFEAT_PARAMETER = "parameters/defeat_trigger/request";
		private readonly StringName DEFEAT_SEEK_PARAMETER = "parameters/defeat_seek/seek_request";

		public override void _Ready()
		{
			rootAnimationTree.Active = lTailAnimationTree.Active = rTailAnimationTree.Active = flyingEyeAnimationTree.Active = true; // Activate animation trees

			SetUpMissiles();

			StageSettings.instance.ConnectUnloadSignal(this);
			StageSettings.instance.ConnectRespawnSignal(this);

			StartIntroduction();
		}


		public void Respawn()
		{
			fightState = FightState.Waiting;
			currentHealth = MAX_HEALTH;

			bossPathFollower.Progress = STARTING_POSITION;
			GlobalPosition = Vector3.Forward * STARTING_POSITION;
			GlobalRotation = Vector3.Up * Mathf.Pi;

			// Reset animations
			isPhaseTwoActive = false;
			lTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, DISABLED_STATE);
			rTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, DISABLED_STATE);
			rootAnimationTree.Set(EYE_PARAMETER, DISABLED_STATE);

			rootAnimationTree.Set(DAMAGE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			lTailAnimationTree.Set(LIGHT_ATTACK_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			rTailAnimationTree.Set(LIGHT_ATTACK_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);

			flyingEyeAnimationTree.Set(EYE_PARAMETER, DISABLED_STATE);
			flyingEyeAnimationTree.Set(DAMAGE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);

			// Reset movement
			MoveSpeed = 0;
			moveSpeedVelocity = 0;
			movementBlend = 0;
			rootAnimationTree.Set(MOVEMENT_BLEND_PARAMETER, movementBlend);

			// Reset phase
			phaseTwoBlend = 0;
			phaseTwoBlendVelocity = 0;
			rootAnimationTree.Set(PHASE_TWO_PARAMETER, phaseTwoBlend);
			rootAnimationTree.Set(HEAVY_ATTACK_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			impactEffect.Visible = false;
			flyingEyeHitbox.Monitorable = false;
			flyingEyeVFX.Emitting = false;

			eventAnimator.Play("RESET");

			// Reset attacks
			flyingEyeBlend = 0;
			attackTimer = 0;
			attackCounter = 0;
			attackState = AttackState.INACTIVE;

			// Reset flying eye
			flyingEyeRoot.Position = Vector3.Zero;
			flyingEyeRoot.Rotation = Vector3.Zero;

			RespawnMissiles();
		}


		public void Unload()
		{
			// Cleanup orphan nodes
			for (int i = 0; i < missilePool.Count; i++)
				missilePool[i].QueueFree();
		}


		private void StartIntroduction()
		{
			rootAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			lTailAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			rTailAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			GlobalTransform = Transform3D.Identity;

			cutsceneCamera.Current = true;

			// Disable the player for the intro animation
			Character.ProcessMode = ProcessModeEnum.Disabled;
			Interface.PauseMenu.AllowPausing = false;
			HeadsUpDisplay.instance.Visible = false;
		}


		private void FinishIntroduction()
		{
			if (TransitionManager.IsTransitionActive) return; // Player must have skipped the introduction animation

			TransitionManager.StartTransition(new()
			{
				inSpeed = 0f,
				outSpeed = .5f,
				color = Colors.Black
			});
			TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.StartBattle), (uint)ConnectFlags.OneShot);
		}


		private void StartBattle()
		{
			rootAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			lTailAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);
			rTailAnimationTree.Set(INTRO_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Abort);

			Respawn();
			eventAnimator.Play("finish-intro");
			TransitionManager.FinishTransition();
			Character.ProcessMode = ProcessModeEnum.Inherit;
			Interface.PauseMenu.AllowPausing = true;
			HeadsUpDisplay.instance.Visible = true;
			Character.Camera.Camera.Current = true;
		}


		private void StartFinalBlow()
		{
			TransitionManager.StartTransition(new()
			{
				inSpeed = 0f,
				outSpeed = .5f,
				color = Colors.Black
			});
			TransitionManager.FinishTransition();

			eventAnimator.Play("defeat");
			eventAnimator.Advance(0.0);

			Character.Visible = false;
			Character.AddLockoutData(StageSettings.instance.Data.CompletionLockout);
			Interface.PauseMenu.AllowPausing = false;
			HeadsUpDisplay.instance.Visible = false;
		}


		private void StartDefeat()
		{
			rootAnimationTree.Set(DEFEAT_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			lTailAnimationTree.Set(DEFEAT_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			rTailAnimationTree.Set(DEFEAT_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			flyingEyeAnimationTree.Set(DEFEAT_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);

			// TODO Award 1000 points for defeating the boss

			fightState = FightState.Defeated;
			Character.ProcessMode = ProcessModeEnum.Disabled;
		}


		private void StartResults()
		{
			rootAnimationTree.Active = rTailAnimationTree.Active = lTailAnimationTree.Active = flyingEyeAnimationTree.Active = false;
			eventAnimator.Play("finish-defeat");

			Character.Visible = true;
			Character.ProcessMode = ProcessModeEnum.Inherit;
			Character.Camera.Camera.Current = true;
			StageSettings.instance.FinishLevel(true);
		}


		public override void _PhysicsProcess(double _)
		{
			switch (fightState)
			{
				case FightState.Introduction:
					if (Input.IsActionJustPressed("button_pause"))
						FinishIntroduction();
					break;
				case FightState.Waiting:
					UpdateEyes();
					// Wait for the player to do something
					if (!Mathf.IsZeroApprox(Character.MoveSpeed))
						fightState = FightState.Active;
					break;
				case FightState.Active:
					// Update Boss
					UpdateEyes();
					UpdatePhase();
					UpdatePosition();
					UpdateMissiles();
					UpdateAttacks();
					UpdateHitboxes();
					break;
				case FightState.Defeated:
					if (Input.IsActionJustPressed("button_pause"))
					{
						eventAnimator.Play("finish-defeat");
						rootAnimationTree.Set(DEFEAT_SEEK_PARAMETER, 10);
						rTailAnimationTree.Set(DEFEAT_SEEK_PARAMETER, 10);
						lTailAnimationTree.Set(DEFEAT_SEEK_PARAMETER, 10);
					}
					break;
			}
		}


		/// <summary> Fastest possible movement speed, based on the player's top speed. </summary>
		private float CharacterTopSpeed => Character.Skills.GroundSettings.speed;

		private float MoveSpeed { get; set; }
		private float moveSpeedVelocity;

		private float movementBlend;
		private readonly StringName MOVESPEED_PARAMETER = "parameters/movespeed/scale";
		private readonly StringName MOVEMENT_BLEND_PARAMETER = "parameters/movement_blend/blend_amount";

		/// <summary> Current distance to the player. </summary>
		private float currentDistance;
		/// <summary> Ideal distance when attacking. </summary>
		private const float STRIKE_DISTANCE = 8.0f;
		/// <summary> Always try to keep at least this much distance between the player. (Unless boss is attacking) </summary>
		private const float CHASE_DISTANCE = 25.0f;
		/// <summary> Distance to start using close range attacks. </summary>
		private const float ATTACK_DISTANCE = 30.0f;
		/// <summary> Distance to start running away from the player. </summary>
		private const float RETREAT_DISTANCE = 55.0f;
		/// <summary> Distance to start advancing towards the player. </summary>
		private const float ADVANCE_DISTANCE = 65.0f;
		/// <summary> Distance to teleport. </summary>
		private const float TELEPORT_DISTANCE = 300.0f;

		/// <summary> Traction amount to use when attacking. </summary>
		private const float STRIKE_TRACTION = 20.0f;
		private const float TRACTION = .2f;
		private const float FRICTION = .8f;
		private const float HITSTUN_FRICTION = .4f;

		private float CalculateDistance() // Calculate the distance between the player and the boss based on their respective pathfollowers.
		{
			float bossProgress = bossPathFollower.Progress + MoveSpeed * PhysicsManager.physicsDelta;
			float playerProgress = PathFollower.Progress + Character.MoveSpeed * PhysicsManager.physicsDelta;
			if (bossProgress < playerProgress)
				bossProgress += PathFollower.ActivePath.Curve.GetBakedLength();

			return bossProgress - playerProgress;
		}

		private void UpdatePosition()
		{
			if (currentHealth == 0)
				MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref moveSpeedVelocity, HITSTUN_FRICTION);
			else if (damageState != DamageState.None && !isPhaseTwoActive) // Knockback/hitstun
			{
				MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref moveSpeedVelocity, HITSTUN_FRICTION); // Slow down

				if (damageState == DamageState.Knockback)
				{
					if (MoveSpeed < 5.0f) // Because transitioning from speed 0 feels laggy
					{
						if (currentHealth <= 3) // Check for second phase
						{
							isPhaseTwoActive = true;
							hitDialogs[1].Activate();
						}

						damageState = DamageState.None;
					}
				}
				else if (Character.IsOnGround) // Player canceled their assault, resume movement
				{
					FinishHeavyAttack(true);
					damageState = DamageState.None;
				}
			}
			else
			{
				currentDistance = CalculateDistance();

				if (currentDistance >= RETREAT_DISTANCE && currentDistance <= ADVANCE_DISTANCE) // Waiting for the player
					MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref moveSpeedVelocity, FRICTION);
				else if (attackState == AttackState.STRIKE && currentDistance < ATTACK_DISTANCE && !isPhaseTwoActive) // Attempt to match distance for more consistant attacks
				{
					float delta = currentDistance - STRIKE_DISTANCE;
					MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, Mathf.Clamp(MoveSpeed - delta, 0f, CharacterTopSpeed), ref moveSpeedVelocity, STRIKE_TRACTION * PhysicsManager.physicsDelta);
				}
				else
				{
					float speedFactor;
					if (currentDistance < RETREAT_DISTANCE)
						speedFactor = 1f - (currentDistance - CHASE_DISTANCE) / (RETREAT_DISTANCE - CHASE_DISTANCE);
					else // Move towards player
					{
						speedFactor = -Mathf.Clamp((currentDistance - ADVANCE_DISTANCE) * .1f, 0f, 1f);

						if (currentDistance > TELEPORT_DISTANCE
							&& PathFollower.ActivePath.Curve.GetBakedLength() - currentDistance > 10) // Teleport when really far away
							bossPathFollower.Progress -= (currentDistance - TELEPORT_DISTANCE) * PhysicsManager.physicsDelta;
					}

					MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, CharacterTopSpeed * speedFactor, ref moveSpeedVelocity, TRACTION);
				}
			}

			bossPathFollower.Progress += MoveSpeed * PhysicsManager.physicsDelta;


			float speedRatio = .5f + Mathf.Abs(MoveSpeed / CharacterTopSpeed) * 1.2f;
			if (damageState == DamageState.Knockback)
				speedRatio = 0f;

			rootAnimationTree.Set(MOVESPEED_PARAMETER, speedRatio);

			float direction = Mathf.Sign(MoveSpeed);
			if (Mathf.Abs(MoveSpeed) <= 2f)
				direction = 0;
			else if (isPhaseTwoActive)
				direction *= -1;

			movementBlend = Mathf.MoveToward(movementBlend, direction, 4f * PhysicsManager.physicsDelta);
			rootAnimationTree.Set(MOVEMENT_BLEND_PARAMETER, movementBlend);

			GlobalPosition = bossPathFollower.GlobalPosition;
		}

		/// <summary> Math.PI during the first phase, 0 during the phase two. </summary>
		private float phaseRotation;
		/// <summary> Used to smoothdamp phaseRotation. </summary>
		private float phaseRotationVelocity;
		/// <summary> How much smoothing to apply when the phase changes. </summary>
		private readonly float PHASE_ROTATION_SMOOTHING = 30.0f;

		/// <summary> Has phase two started? </summary>
		private bool isPhaseTwoActive;
		private float phaseTwoBlend;
		private float phaseTwoBlendVelocity;
		private readonly StringName PHASE_TWO_PARAMETER = "parameters/phase_two_add/add_amount";
		private readonly float PHASE_TWO_BLEND_SMOOTHING = 30.0f;

		private void UpdatePhase()
		{
			if (damageState == DamageState.None)
			{
				if (isPhaseTwoActive)
					phaseRotation = ExtensionMethods.SmoothDampAngle(phaseRotation, 0, ref phaseRotationVelocity, PHASE_ROTATION_SMOOTHING * PhysicsManager.physicsDelta);
				else
					phaseRotation = Mathf.Pi;
			}

			float facingAngle = bossPathFollower.Back().Flatten().AngleTo(Vector2.Down) - phaseRotation;
			GlobalRotation = Vector3.Up * facingAngle;

			if (isPhaseTwoActive)
			{
				phaseTwoBlend = ExtensionMethods.SmoothDamp(phaseTwoBlend, 1.0f, ref phaseTwoBlendVelocity, PHASE_TWO_BLEND_SMOOTHING * PhysicsManager.physicsDelta);
				rootAnimationTree.Set(PHASE_TWO_PARAMETER, phaseTwoBlend);
			}
		}

		#region Attacks
		[ExportGroup("Missiles")]
		[Export]
		private Array<NodePath> missilePositionPaths;
		private Node3D[] missilePositions; // Where to fire missiles from
		[Export]
		private PackedScene missileScene; // Missile packed scene
		private readonly Array<Missile> missilePool = new(); // Pool of missiles
		private readonly int MAX_MISSILE_COUNT = 5; // Same as the original game, only 5 missiles can be fired at a time

		private void SetUpMissiles()
		{
			missilePositions = new Node3D[missilePositionPaths.Count];
			for (int i = 0; i < missilePositionPaths.Count; i++)
				missilePositions[i] = GetNode<Node3D>(missilePositionPaths[i]);

			for (int i = 0; i < MAX_MISSILE_COUNT; i++) // Pool missiles
				missilePool.Add(missileScene.Instantiate<Missile>());
		}

		private void RespawnMissiles()
		{
			for (int i = 0; i < MAX_MISSILE_COUNT; i++)
			{
				if (missilePool[missileIndex].IsInsideTree()) // Remove all missiles from the scene tree
					missilePool[missileIndex].GetParent().RemoveChild(missilePool[missileIndex]);
			}

			missileTimer = MISSILE_DELAY;
		}

		private bool missileGroupReset = true;
		private int missileIndex;
		private float missileTimer;
		/// <summary> How long between each individual missile shots. </summary>
		private const float MISSILE_INTERVAL = .1f;
		/// <summary> Interval length between missile groups. </summary>
		private const float MISSILE_GROUP_INTERVAL = 2.5f;
		/// <summary> How much horizontal spread to allow. </summary>
		private const float MISSILE_SPREAD = 1.5f;
		/// <summary> Starting delay so missiles don't fire immediately. </summary>
		private const float MISSILE_DELAY = 1.5f;

		private void UpdateMissiles()
		{
			if (missileGroupReset && currentDistance < ATTACK_DISTANCE) // Too close for missiles
				return;

			if (currentDistance > TELEPORT_DISTANCE) // Too far for missiles
				return;

			missileTimer = Mathf.MoveToward(missileTimer, 0, PhysicsManager.physicsDelta);

			// Spawn a Missile
			if (missileTimer <= 0)
			{
				SpawnMissile();

				// Wait for the next missile group?
				missileGroupReset = missileIndex >= MAX_MISSILE_COUNT;
				missileTimer = missileGroupReset ? MISSILE_GROUP_INTERVAL : MISSILE_INTERVAL;
				if (missileGroupReset) // Loop missile index
					missileIndex = 0;
			}
		}

		/// <summary>
		/// Spawns a missle.
		/// </summary>
		private void SpawnMissile()
		{
			if (!missilePool[missileIndex].IsInsideTree()) // Add missile to the tree if it isn't already added
				GetTree().Root.AddChild(missilePool[missileIndex]);

			int spawnFrom = Runtime.randomNumberGenerator.RandiRange(0, 2); // Figure out which position to spawn from
			Vector3 spawnPosition = missilePositions[spawnFrom].GlobalPosition; // Move missile to the spawn position
			missilePool[missileIndex].Launch(LaunchSettings.Create(spawnPosition, GetMissileTargetPosition(missileIndex), 5)); // Recalculate trajectory

			missileIndex++;
		}

		/// <summary>
		/// Gets the position where the missile will target based on how fast the player is moving.
		/// </summary>
		private Vector3 GetMissileTargetPosition(int i)
		{
			float progress = bossPathFollower.Progress; // Cache current progress

			// Try to predict where the player will be when the missile lands
			float dot = Character.GetMovementDirection().Dot(PathFollower.Back());
			float offsetPrediction = Character.MoveSpeed * Runtime.randomNumberGenerator.RandfRange(1f, 2f) * dot;
			bossPathFollower.Progress = PathFollower.Progress + offsetPrediction;
			bossPathFollower.HOffset = -PathFollower.LocalPlayerPositionDelta.X; // Works since the path is flat
			if (i != 0 && i < MAX_MISSILE_COUNT - 1) // Slightly randomize the middle missile's spread
				bossPathFollower.HOffset += Runtime.randomNumberGenerator.RandfRange(-MISSILE_SPREAD, MISSILE_SPREAD);

			Vector3 targetPosition = bossPathFollower.GlobalPosition;
			bossPathFollower.Progress = progress; // Reset progress
			bossPathFollower.HOffset = 0; // Reset HOffset
			targetPosition.Y = 0; // Make sure missiles end up on the floor

			return targetPosition;
		}

		/// <summary> Which side is the boss attacking? -1 for left, 1 for right. </summary>
		private int attackSide;
		/// <summary> How many attacks has the boss done since giving an opening? </summary>
		private int attackCounter;
		/// <summary> Timer for attacks. </summary>
		private float attackTimer;
		/// <summary> How long to wait between phase one attacks. </summary>
		private readonly float PHASE_ONE_ATTACK_INTERVAL = .8f;
		/// <summary> How long to wait between phase two attacks </summary>
		private readonly float PHASE_TWO_ATTACK_INTERVAL = 1.4f;

		/// <summary> Target position of the flying eye when sent out. </summary>
		private Vector3 flyingEyeTarget;
		/// <summary> 0 - 1 value blend value. </summary>
		private float flyingEyeBlend;
		private readonly float FLYING_EYE_NORMAL_SPEED = 0.6f; // How fast does the eye typically move?
		private readonly float FLYING_EYE_LOCKON_SPEED = 0.3f; // How fast does the eye move when targeted?
		private readonly float FLYING_EYE_KNOCKBACK = 1.2f; // How quickly to knock the eye back on the final hit

		private void UpdateAttacks()
		{
			if (damageState != DamageState.None) return; // Boss is too busy getting owned

			if (attackState != AttackState.INACTIVE) // Process the current attack
			{
				if (isPhaseTwoActive) // Eye attack
					UpdateEyeAttack();
				else if (!IsHeavyAttackActive) // Light Attack
					UpdateLightAttack();

				return;
			}

			if (currentDistance > ATTACK_DISTANCE || missileIndex != 0) return; // Out of range, or shooting missiles

			attackTimer -= PhysicsManager.physicsDelta;
			if (attackTimer < 0)
			{
				attackTimer = PHASE_ONE_ATTACK_INTERVAL;
				if (isPhaseTwoActive) // Send eye out
				{
					attackTimer = PHASE_TWO_ATTACK_INTERVAL;
					StartEyeAttack();
				}
				else if (attackCounter <= 1)
					StartLightAttack();
				else
					StartHeavyAttack();
			}
		}

		private float lightAttackTrackingVelocity;
		private readonly StringName LIGHT_ATTACK_PARAMETER = "parameters/light_attack_trigger/request";
		private readonly StringName LIGHT_ATTACK_POSITION_PARAMETER = "parameters/light_attack_blend/blend_amount";
		private void StartLightAttack()
		{
			// Play hint
			if (dialogFlags[0] == 0)
				dialogFlags[0]++;
			else if (dialogFlags[0] == 1)
			{
				PlayHint(0);
				dialogFlags[0] = 1;
			}

			lightAttackTrackingVelocity = 0;
			attackCounter++;
			if (PathFollower.LocalPlayerPositionDelta.X < 0) // Left Attack
			{
				attackSide = -1;
				eventAnimator.Play("l-light-attack");
				lTailAnimationTree.Set(LIGHT_ATTACK_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
			else
			{
				attackSide = 1;
				eventAnimator.Play("r-light-attack");
				rTailAnimationTree.Set(LIGHT_ATTACK_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}


		private void UpdateLightAttack()
		{
			if (attackState != AttackState.STRIKE || attackSide == 0) return;

			// Track the player's position
			float current = (float)lTailAnimationTree.Get(LIGHT_ATTACK_POSITION_PARAMETER);
			float pos = PathFollower.LocalPlayerPositionDelta.X;
			if ((attackSide == -1 && pos > 0) || (attackSide == 1 && pos < 0))
				pos = 0;

			pos = 2 * -Mathf.Abs(pos / 4) + 1;
			current = ExtensionMethods.SmoothDamp(current, pos, ref lightAttackTrackingVelocity, .2f);

			lTailAnimationTree.Set(LIGHT_ATTACK_POSITION_PARAMETER, current);
			rTailAnimationTree.Set(LIGHT_ATTACK_POSITION_PARAMETER, current);
		}

		private bool IsHeavyAttackActive => attackState != AttackState.INACTIVE && attackCounter == 0;

		private readonly StringName HEAVY_STRIKE_STATE = "strike";
		private readonly StringName HEAVY_RECOVERY_STATE = "recovery";
		private readonly StringName HEAVY_ATTACK_PARAMETER = "parameters/heavy_attack_transition/transition_request";
		private readonly StringName HEAVY_ATTACK_TRIGGER_PARAMETER = "parameters/heavy_attack_trigger/request";

		private void StartHeavyAttack()
		{
			attackCounter = 0;
			if (PathFollower.LocalPlayerPositionDelta.X < 0) // Left Attack
			{
				attackSide = -1;
				eventAnimator.Play("l-heavy-attack");
				lTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, HEAVY_STRIKE_STATE);
			}
			else
			{
				attackSide = 1;
				eventAnimator.Play("r-heavy-attack");
				rTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, HEAVY_STRIKE_STATE);
			}

			rootAnimationTree.Set(HEAVY_ATTACK_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		public void FinishHeavyAttack(bool forced = default)
		{
			if (!forced && (damageState == DamageState.Hitstun || Character.Lockon.IsHomingAttacking)) return;

			// Player missed their chance, update hints.
			if (dialogFlags[1] < 2)
			{
				if (dialogFlags[1] == 0)
					PlayHint(1);
				else if (dialogFlags[1] == 1)
					PlayHint(2);

				dialogFlags[1]++;
			}

			attackState = AttackState.INACTIVE;

			// Disables all hurtboxes
			eventAnimator.Play("disable-hurtbox-03");

			if (attackSide == 1)
				rTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, HEAVY_RECOVERY_STATE);
			else if (attackSide == -1)
				lTailAnimationTree.Set(HEAVY_ATTACK_PARAMETER, HEAVY_RECOVERY_STATE);
		}

		[ExportGroup("Effects")]
		[Export]
		private GroupGpuParticles3D impactEffect;
		private void StartImpactFX(NodePath n)
		{
			impactEffect.Visible = true;
			Vector3 p = GetNode<Node3D>(n).GlobalPosition;
			p.Y = 0; // Snap to the floor
			impactEffect.GlobalPosition = p;
			impactEffect.RestartGroup();
		}


		[Export]
		private GroupGpuParticles3D hitEffect;
		private void StartHitFX()
		{
			hitEffect.Visible = true;
			hitEffect.GlobalPosition = Character.CenterPosition;
			hitEffect.RestartGroup();
		}

		[Export]
		private GpuParticles3D flyingEyeVFX;
		#endregion

		#region Eyes
		[ExportGroup("Eyes")]
		[Export]
		private Array<Node3D> mainEyes;
		[Export]
		private Array<Node3D> tailEyes;
		/// <summary> Eyes that track the player. </summary>
		private float eyeTrackingFactor;
		private float eyeTrackingVelocity;
		private const float EYE_TRACKING_SMOOTHING = 30.0f;

		/// <summary>
		/// Updates the eyes to look at the player's position.
		/// </summary>
		private void UpdateEyes()
		{
			// Main eyes always look at the player
			for (int i = 0; i < mainEyes.Count; i++)
			{
				if (currentHealth == 0)
				{
					mainEyes[i].Rotation = Vector3.Zero;
					continue;
				}

				if ((mainEyes[i].GlobalPosition - Character.GlobalPosition).LengthSquared() < 1f) // Failsafe
					continue;

				mainEyes[i].LookAt(Character.GlobalPosition, Vector3.Up);
			}

			float targetTracking;
			if (isPhaseTwoActive)
				targetTracking = phaseTwoBlend;
			else
				targetTracking = attackState == AttackState.INACTIVE ? 1f : 0f;
			eyeTrackingFactor = ExtensionMethods.SmoothDamp(eyeTrackingFactor, targetTracking, ref eyeTrackingVelocity, EYE_TRACKING_SMOOTHING * PhysicsManager.physicsDelta);

			// Update tail eyes
			for (int i = 0; i < tailEyes.Count; i++)
			{
				if (currentHealth == 0)
				{
					tailEyes[i].Rotation = Vector3.Zero;
					continue;
				}

				if ((tailEyes[i].GlobalPosition - Character.GlobalPosition).LengthSquared() < 1f) // Failsafe
					continue;

				tailEyes[i].LookAt(Character.GlobalPosition, Vector3.Up);
				tailEyes[i].Basis = tailEyes[i].Basis.Slerp(Basis.Identity, 1f - eyeTrackingFactor).Orthonormalized();
			}
		}

		[Export]
		/// <summary> Hurtbox of the flying eye. </summary>
		private Area3D flyingEyeHitbox;
		[Export]
		/// <summary> Flying eye, only accessible during phase two. </summary>
		private Node3D flyingEyeRoot;
		[Export]
		/// <summary> Position in the body. </summary>
		private Node3D flyingEyeBone;
		/// <summary> Target position of the flying eye attack, based on the pathfollower. </summary>
		private Vector2 flyingEyeAttackPosition;
		/// <summary> Maximum amount the flying eye can track the player. Lower values make the attack easier to avoid. </summary>
		private const float FLYING_EYE_MAX_TRACKING = 2.5f;
		/// <summary> Used to prevent the flying eye clipping into the ground. </summary>
		private const float FLYING_EYE_RADIUS = 2f;
		private void UpdateFlyingEyeTarget()
		{
			// Calculate
			float horizontalTracking = flyingEyeAttackPosition.X - PathFollower.LocalPlayerPositionDelta.X;
			horizontalTracking = Mathf.Clamp(horizontalTracking, -FLYING_EYE_MAX_TRACKING, FLYING_EYE_MAX_TRACKING);
			flyingEyeTarget = Character.PathFollower.GlobalPosition + Vector3.Up * flyingEyeAttackPosition.Y;
			flyingEyeTarget += Character.PathFollower.Right() * (flyingEyeAttackPosition.X - horizontalTracking);
			flyingEyeTarget += Character.PathFollower.Back() * FLYING_EYE_RADIUS;
		}


		private readonly StringName EYE_BITE_STATE = "bite";
		private readonly StringName EYE_RETREAT_STATE = "retreat";
		private readonly StringName EYE_PARAMETER = "parameters/eye_transition/transition_request";
		private void StartEyeAttack()
		{
			attackState = AttackState.STRIKE;

			// Cache current player position delta
			flyingEyeAttackPosition = new Vector2(PathFollower.LocalPlayerPositionDelta.X, FLYING_EYE_RADIUS);
			eventAnimator.Play("cage");
			flyingEyeVFX.Emitting = true;
			rootAnimationTree.Set(EYE_PARAMETER, ENABLED_STATE); // Open eye cage
			flyingEyeAnimationTree.Set(EYE_PARAMETER, EYE_BITE_STATE); // Start biting
		}


		private void UpdateEyeAttack()
		{
			if (attackState == AttackState.STRIKE)
			{
				UpdateFlyingEyeTarget();

				flyingEyeBlend = Mathf.MoveToward(flyingEyeBlend, 1f, FLYING_EYE_NORMAL_SPEED * PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(flyingEyeBlend, 1f))
					RetreatEyeAttack();
			}
			else
			{
				if (currentHealth == 0)
					flyingEyeBlend = Mathf.MoveToward(flyingEyeBlend, 0f, FLYING_EYE_KNOCKBACK * PhysicsManager.physicsDelta);
				else if (Character.Lockon.IsHomingAttacking)
					flyingEyeBlend = Mathf.MoveToward(flyingEyeBlend, 0f, FLYING_EYE_LOCKON_SPEED * PhysicsManager.physicsDelta);
				else
					flyingEyeBlend = Mathf.MoveToward(flyingEyeBlend, 0f, FLYING_EYE_NORMAL_SPEED * PhysicsManager.physicsDelta);

				if (Mathf.IsZeroApprox(flyingEyeBlend))
					FinishEyeAttack();
			}

			float t = Mathf.SmoothStep(0, 1, flyingEyeBlend);
			flyingEyeRoot.GlobalPosition = flyingEyeBone.GlobalPosition.Lerp(flyingEyeTarget, t);

			// Update rotation
			Vector2 delta = (flyingEyeTarget - flyingEyeBone.GlobalPosition).Flatten();
			flyingEyeRoot.GlobalRotation = Vector3.Up * Mathf.LerpAngle(flyingEyeBone.GlobalRotation.Y, delta.AngleTo(Vector2.Down), t);
		}


		private void RetreatEyeAttack()
		{
			attackState = AttackState.RECOVERY;
			flyingEyeAnimationTree.Set(EYE_PARAMETER, EYE_RETREAT_STATE);
			flyingEyeHitbox.Monitorable = true;
		}


		private void FinishEyeAttack()
		{
			if (currentHealth == 0)
				StartDefeat();
			else if (dialogFlags[2] == 0 && currentHealth == 1)
			{
				hitDialogs[3].Activate();
				dialogFlags[2] = 1;
			}

			eventAnimator.Play("cage");
			flyingEyeVFX.Emitting = false;
			flyingEyeAnimationTree.Set(EYE_PARAMETER, DISABLED_STATE); // Reset
			rootAnimationTree.Set(EYE_PARAMETER, DISABLED_STATE); // Close eye cage

			flyingEyeHitbox.Monitorable = false;
			attackState = AttackState.INACTIVE;
		}
		#endregion

		#region Hitboxes
		private int currentHealth;
		private readonly int MAX_HEALTH = 5;

		private enum DamageState
		{
			None, // Not taking any damage
			Hitstun, // Player is bouncing on the tail
			Knockback // Sliding backwards
		}
		private DamageState damageState;
		/// <summary> How much force to knockback the boss back with when taking damage. </summary>
		private readonly float KNOCKBACK = 80.0f;

		/// <summary>
		/// Deals damage to the boss. Returns True if the boss is defeated.
		/// </summary>
		private void TakeDamage()
		{
			currentHealth = (int)Mathf.MoveToward(currentHealth, 0, 1);
			eventAnimator.Play("damage");
			eventAnimator.Advance(0.0);

			if (currentHealth == 4)
				hitDialogs[0].Activate();
			else if (isPhaseTwoActive && currentHealth == 2)
				hitDialogs[2].Activate();
			else if (currentHealth == 0)
				StartFinalBlow();
		}

		private void UpdateHitboxes()
		{
			if (isCollidingWithBackEye)
			{
				ProcessBackEyeCollision();
				return;
			}

			if (isCollidingWithFlyingEye)
			{
				ProcessFlyingEyeCollision();
				return;
			}

			if (IsCollidingWithBoss)
				ProcessHitboxCollision();
		}

		/// <summary> Keeps track of how many of the boss's hitboxes the player is colliding with. </summary>
		private int bossHitboxCounter;
		private bool IsCollidingWithBoss => bossHitboxCounter != 0;
		/// <summary>
		/// Called when the player enters one of the boss's hitboxes.
		/// </summary>
		public void OnHitboxEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			bossHitboxCounter++;
		}

		public void OnHitboxExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			bossHitboxCounter--;
		}

		public void ProcessHitboxCollision()
		{
			if (Character.Lockon.IsHomingAttacking || Character.Lockon.IsBouncingLockoutActive) return; // Player's homing attack always takes priority.
			if (damageState == DamageState.Knockback) return; // Boss is in knockback and can't damage the player.

			if (Character.Skills.IsSpeedBreakActive)
			{
				Character.Skills.ToggleSpeedBreak();
				Character.StartKnockback(new CharacterController.KnockbackSettings()
				{
					disableDamage = true
				});
			}
			else
				Character.StartKnockback();
		}

		/// <summary> Is the player currently colliding with the flying eye? </summary>
		private bool isCollidingWithFlyingEye;
		/// <summary>
		/// Called when the player enters the flying eye.
		/// </summary>
		public void OnFlyingEyeEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isCollidingWithFlyingEye = true;
		}

		/// <summary>
		/// Called when the player exits the flying eye.
		/// </summary>
		public void OnFlyingEyeExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isCollidingWithFlyingEye = false;
		}

		private void ProcessFlyingEyeCollision()
		{
			if (Character.Lockon.IsBouncingLockoutActive) return; // Player just finished a homing attack

			if (Character.Skills.IsSpeedBreakActive) // Special attack
				return;

			// Player countered the attack
			if (attackState == AttackState.STRIKE && Character.ActionState == CharacterController.ActionStates.JumpDash)
			{
				flyingEyeAnimationTree.Set(DAMAGE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				RetreatEyeAttack();
				Character.Lockon.StartBounce(false);
				return;
			}

			if (!Character.Lockon.IsHomingAttacking) // Player isn't attacking
			{
				Character.StartKnockback();
				return;
			}

			flyingEyeAnimationTree.Set(DAMAGE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			StartHitFX();
			TakeDamage();
			Character.Lockon.StartBounce(false);
		}

		/// <summary> Is the player currently colliding with the eye on the boss's back? </summary>
		private bool isCollidingWithBackEye;
		/// <summary>
		/// Called when the player enters with the eye on the boss's back.
		/// </summary>
		public void OnBackEyeEntered(Area3D area)
		{
			if (!area.IsInGroup("player")) return;
			isCollidingWithBackEye = true;
		}

		/// <summary>
		/// Called when the player leavesthe eye on the boss's back.
		/// </summary>
		public void OnBackEyeExited(Area3D area)
		{
			if (!area.IsInGroup("player")) return;
			isCollidingWithBackEye = false;
		}

		public void ProcessBackEyeCollision()
		{
			if (!Character.Lockon.IsHomingAttacking) return; // Player isn't attacking

			if (IsHeavyAttackActive) // End active heavy attack
			{
				// Player can see what's happening; skip obvious hint
				if (dialogFlags[1] < 2)
					dialogFlags[1] = 1;

				FinishHeavyAttack(true);
			}

			StartHitFX();
			TakeDamage();
			Character.Lockon.StartBounce(); // Bounce the player

			MoveSpeed = KNOCKBACK; // Start knockback
			damageState = DamageState.Knockback;
		}

		/// <summary>
		/// Called when the player hits one of the eyes on the tail. No damage is actually dealt.
		/// </summary>
		public void OnTraversalHurtboxCollision(Area3D a, bool hitFarEye)
		{
			if (!a.IsInGroup("player")) return;
			if (!Character.Lockon.IsHomingAttacking) return; // Player isn't attacking

			StartHitFX();
			rootAnimationTree.Set(DAMAGE_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			Character.Lockon.StartBounce();
			damageState = DamageState.Hitstun;

			// Disable hurtboxes so the player can't just bounce on the same eye infinitely
			eventAnimator.Play(hitFarEye ? "disable-hurtbox-01" : "disable-hurtbox-02");
		}
		#endregion
	}
}
