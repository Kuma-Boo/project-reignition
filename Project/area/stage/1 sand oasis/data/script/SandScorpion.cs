using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Bosses
{
	/// <summary> Controls the first boss of the game, the Sand Scorpion. </summary>
	/*
	Behaviour:
	Runs away from the player, unless the player is far away and backing up, in which case walk towards the player to maintain distance.
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
		private PathFollow3D pathFollower;

		private int currentHealth;
		private readonly int MAX_HEALTH = 5;

		[Export]
		private AnimationTree rootAnimator;
		[Export]
		private AnimationTree lTailAnimator;
		[Export]
		private AnimationTree rTailAnimator;
		[Export]
		private AnimationTree eyeAnimator;
		[Export]
		private AnimationPlayer eventAnimator; //Additive animator that manages stuff like damage flashing, hitboxes, etc

		private bool isActive; //Process the boss?
		private CharacterController Character => CharacterController.instance;
		private float LocalPlayerPosition => Character.PathFollower.FlatPlayerPositionDelta.x;

		public override void _Ready()
		{
			rootAnimator.Active = lTailAnimator.Active = rTailAnimator.Active = true; //Activate animation trees

			eyes = new Node3D[eyePaths.Length];
			for (int i = 0; i < eyePaths.Length; i++)
			{
				eyes[i] = GetNode<Node3D>(eyePaths[i]);
			}
			//_eventAnimator.Play("intro");

			SetUpMissiles();

			GD.Print("Skipping intro animation.");
			Respawn();
			ProgressPhase();
		}

		public void Respawn()
		{
			currentHealth = MAX_HEALTH;
			GlobalPosition = Vector3.Forward * 60;
			GlobalRotation = Vector3.Up * Mathf.Pi;

			isSecondPhaseActive = false;
		}

		public override void _ExitTree()
		{
			//Cleanup orphan nodes
			for (int i = 0; i < missilePool.Count; i++)
				missilePool[i].QueueFree();
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateEyes();

			if (!isActive)
			{
				if (Mathf.IsZeroApprox(Character.MoveSpeed))
					return; //Wait for the player to do something

				isActive = true;
			}

			UpdatePosition();
			UpdateRotation();
			UpdateMissiles();
			UpdateAttacks();
		}


		private bool isSecondPhaseActive;
		private float phaseRotation;
		private float phaseRotationVelocity;
		private readonly float PHASE_TRANSITION_SPEED = .6f;
		private readonly string PHASE_PARAMETER = "parameters/SecondPhaseState/current";
		private void ProgressPhase() //Advance to phase two
		{
			isSecondPhaseActive = true;
			rootAnimator.Set(PHASE_PARAMETER, 1);
		}

		private float MoveSpeed { get; set; }
		private float spdVelocity;
		private readonly float STRIKE_TRACTION = .4f;
		private readonly float TRACTION = .2f;
		private readonly float FRICTION = .8f;
		private readonly float HITSTUN_FRICTION = .4f;
		private readonly float MOVESPEED = 40.0f;
		private readonly string MOVESPEED_PARAMETER = "parameters/MoveSpeed/scale";
		private readonly string MOVESTATE_PARAMETER = "parameters/MovementState/current";

		private float currentDistance; //Current distance to the player
		private readonly float STRIKE_DISTANCE = 8.0f; //Target distance when attacking
		private readonly float CHASE_DISTANCE = 24.0f; //Minimum distance to aim for
		private readonly float ATTACK_DISTANCE = 30.0f; //Distance to start using close range attacks
		private readonly float RETREAT_DISTANCE = 55.0f; //Distance to start running away from the player
		private readonly float ADVANCE_DISTANCE = 65.0f; //Distance to start advancing the player
		private float CalculateDistance() //Calculate the distance between the player and the boss based on their respective pathfollowers.
		{
			float bossProgress = pathFollower.Progress + MoveSpeed * PhysicsManager.physicsDelta;
			float playerProgress = Character.PathFollower.Progress + Character.MoveSpeed * PhysicsManager.physicsDelta;
			if (bossProgress < playerProgress)
				bossProgress += Character.PathFollower.ActivePath.Curve.GetBakedLength();

			return bossProgress - playerProgress;
		}

		private void UpdatePosition()
		{
			if (pathFollower == null) return;

			if (damageState != DamageState.Normal && !isSecondPhaseActive)
			{
				MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref spdVelocity, HITSTUN_FRICTION); //Slow down

				if (damageState == DamageState.Knockback)
				{
					if (MoveSpeed < 5.0f) //Because transitioning from speed 0 looks laggy
					{
						if (currentHealth <= 3) //Check for second phase
							ProgressPhase();

						damageState = DamageState.Normal;
					}
				}
				else if (Character.IsOnGround) //Player canceled their assault, resume movement
				{
					FinishHeavyAttack(true);
					damageState = DamageState.Normal;
				}
			}
			else
			{
				currentDistance = CalculateDistance();

				if (currentDistance >= RETREAT_DISTANCE && currentDistance <= ADVANCE_DISTANCE) //Waiting for the player
					MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref spdVelocity, FRICTION);
				else
				{
					if (isStriking && currentDistance < ATTACK_DISTANCE && !isSecondPhaseActive)
					{
						float delta = currentDistance - STRIKE_DISTANCE;
						MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, Mathf.Clamp(MoveSpeed - delta, 0f, MOVESPEED), ref spdVelocity, STRIKE_TRACTION);
					}
					else
					{
						float speedFactor;
						if (currentDistance < RETREAT_DISTANCE)
							speedFactor = 1f - Mathf.Clamp((currentDistance - CHASE_DISTANCE) / (RETREAT_DISTANCE - CHASE_DISTANCE), 0f, 1f);
						else
							speedFactor = -Mathf.Clamp((currentDistance - ADVANCE_DISTANCE) * .1f, 0f, 1f);

						MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, MOVESPEED * speedFactor, ref spdVelocity, TRACTION);
					}
				}
			}

			pathFollower.Progress += MoveSpeed * PhysicsManager.physicsDelta;

			float speedRatio = (MoveSpeed / MOVESPEED) * 1.5f + 1f;
			if (damageState == DamageState.Knockback) //TODO? Add a separate animation for being knocked back
				speedRatio = 0f;
			rootAnimator.Set(MOVESPEED_PARAMETER, speedRatio);
			rootAnimator.Set(MOVESTATE_PARAMETER, Mathf.Abs(MoveSpeed) <= 2f ? 0 : 1);

			GlobalPosition = pathFollower.GlobalPosition;
		}

		private void UpdateRotation()
		{
			if (damageState == DamageState.Normal)
			{
				if (isSecondPhaseActive)
					phaseRotation = ExtensionMethods.SmoothDampAngle(phaseRotation, 0, ref phaseRotationVelocity, PHASE_TRANSITION_SPEED);
				else
					phaseRotation = Mathf.Pi;
			}

			float facingAngle = pathFollower.Back().Flatten().AngleTo(Vector2.Down) - phaseRotation;
			GlobalRotation = Vector3.Up * facingAngle;
		}

		#region Attacks
		[Export]
		private Node3D[] missilePositions; //Where to fire missiles from
		[Export]
		private PackedScene missileScene; //Missile prefab
		private readonly Array<Missile> missilePool = new Array<Missile>(); //Pool of missiles
		private readonly int MISSILE_COUNT = 5; //Same as the original game, only 5 missiles can be fired at a time

		private void SetUpMissiles()
		{
			for (int i = 0; i < MISSILE_COUNT; i++)
				missilePool.Add(missileScene.Instantiate<Missile>());
		}

		private bool missileGroupReset = true;
		private int missileIndex;
		private float missileTimer = 1.5f; //Start with some time so it doesn't fire immediately
		private readonly float MISSILE_INTERVAL = .1f; //How long between missile shots
		private readonly float MISSILE_GROUP_SPACING = 2.5f; //How long between missile groups
		private void UpdateMissiles()
		{
			if (missileGroupReset && currentDistance < ATTACK_DISTANCE) //Too close for missiles
				return;

			missileTimer = Mathf.MoveToward(missileTimer, 0, PhysicsManager.physicsDelta);

			//Spawn a Missile
			if (missileTimer <= 0)
			{
				SpawnMissile(missileIndex);
				missileIndex++;

				//Wait for the next missile group?
				missileGroupReset = missileIndex >= MISSILE_COUNT;
				missileTimer = missileGroupReset ? MISSILE_GROUP_SPACING : MISSILE_INTERVAL;
				if (missileGroupReset) //Loop missile index
					missileIndex = 0;
			}
		}

		private void SpawnMissile(int i)
		{
			if (!missilePool[i].IsInsideTree())
			{
				GetTree().Root.AddChild(missilePool[i]);
			}

			int spawnFrom = RuntimeConstants.randomNumberGenerator.RandiRange(0, 2);
			Vector3 spawnPosition = missilePositions[spawnFrom].GlobalPosition;
			missilePool[i].Launch(Objects.LaunchData.Create(spawnPosition, GetTargetMissilePosition(i), 5));
		}

		private Vector3 GetTargetMissilePosition(int i)
		{
			float progress = pathFollower.Progress; //Cache current progress

			//Try to predict where the player will be when the missile lands
			float dot = Character.GetMovementDirection().Dot(Character.PathFollower.Forward());
			float offsetPrediction = Character.MoveSpeed * 2f * dot;
			pathFollower.Progress = Character.PathFollower.Progress + offsetPrediction;
			pathFollower.HOffset = LocalPlayerPosition; //Works since the path is flat
			if (i != 0 && i < MISSILE_COUNT - 1) //Slightly randomize the middle missiles
				pathFollower.HOffset += RuntimeConstants.randomNumberGenerator.RandfRange(-1f, 1f);

			Vector3 targetPosition = pathFollower.GlobalPosition;
			pathFollower.Progress = progress; //Reset progress
			pathFollower.HOffset = 0; //Reset HOffset
			targetPosition.y = 0; //Make sure missiles end up on the floor

			return targetPosition;
		}

		[Export]
		public Node3D impactEffect;

		private bool isAttacking;
		private bool isStriking;
		private int attackSide; //-1 for left, 1 for right
		private int attackCounter;
		private float attackTimer; //Timer for attacks
		private readonly float ATTACK_INTERVAL = .8f; //How long to wait between attacks

		private float flyingEyePosition; //Lerp value, from 0 - 1
		private float flyingEyeVelocity;
		private readonly float FLYING_EYE_ATTACK_SPEED = .2f; //How fast does the eye move when attacking?
		private readonly float FLYING_EYE_RETREAT_SPEED = .1f; //How fast does the eye move when retreating?
		private readonly float FLYING_EYE_KNOCKBACK = 200.0f; //How quickly to knock the eye back

		private void UpdateAttacks()
		{
			if (damageState != DamageState.Normal) return;

			if (isAttacking) //Process the current attack
			{
				if (isSecondPhaseActive) //Eye attack
				{
					//TODO Improve how flying eye tracks player
					Vector3 targetPosition = isStriking ? Character.GlobalPosition : flyingEyeBone.GlobalPosition;
					Vector2 delta = (targetPosition - flyingEyeRoot.GlobalPosition).Flatten();
					float distance = delta.Length();

					if (isStriking)
					{
						flyingEyeRoot.GlobalRotation = Vector3.Up * delta.AngleTo(Vector2.Down);
						flyingEyePosition = Mathf.MoveToward(flyingEyePosition, 1f, FLYING_EYE_ATTACK_SPEED * PhysicsManager.physicsDelta);
						if (Mathf.IsEqualApprox(flyingEyePosition, 1f))
						{
							isStriking = false;
							eyeAnimator.Set(EYE_PARAMETER, 2);
							flyingEyeHurtbox.Monitorable = flyingEyeHurtbox.Monitoring = true;
						}
					}
					else
					{
						flyingEyePosition = Mathf.MoveToward(flyingEyePosition, 0, FLYING_EYE_RETREAT_SPEED * PhysicsManager.physicsDelta);
						if (Mathf.IsZeroApprox(flyingEyePosition))
							FinishEyeAttack();
					}

					float t = Mathf.SmoothStep(0, 1, flyingEyePosition);
					flyingEyeRoot.GlobalPosition = flyingEyeBone.GlobalPosition.Lerp(Character.GlobalPosition, t);
				}
				else if (!IsHeavyAttackActive) //Light Attack
				{
					if (isStriking)
						attackSide = 0;
					else if (attackSide != 0) //Track the player's position
					{
						float current = (float)lTailAnimator.Get(LIGHT_ATTACK_POSITION_PARAMETER);
						float pos = LocalPlayerPosition;
						if ((attackSide == -1 && pos < 0) || (attackSide == 1 && pos > 0))
							pos = 0;

						pos = 2 * -Mathf.Abs(pos / 4) + 1;
						current = Mathf.Lerp(current, pos, .2f);

						lTailAnimator.Set(LIGHT_ATTACK_POSITION_PARAMETER, current);
						rTailAnimator.Set(LIGHT_ATTACK_POSITION_PARAMETER, current);
					}
				}

				return;
			}

			if (currentDistance > ATTACK_DISTANCE || missileIndex != 0) return; //Out of range, or shooting missiles

			attackTimer -= PhysicsManager.physicsDelta;
			if (attackTimer < 0)
			{
				attackTimer = ATTACK_INTERVAL;
				if (isSecondPhaseActive) //Send eye out
					EyeAttack();
				else if (attackCounter < 1)
					LightAttack();
				else
					HeavyAttack();
			}
		}

		public void StartStrike() => isStriking = true;
		public void StopStrike() => isStriking = false;
		public void FinishAttack()
		{
			GD.Print("ATTACKIN FINISHED");
			isAttacking = false;
		}

		private readonly string LIGHT_ATTACK_PARAMETER = "parameters/LightAttack/active";
		private readonly string LIGHT_ATTACK_POSITION_PARAMETER = "parameters/LightAnimation/blend_position";
		private void LightAttack()
		{
			attackCounter++;
			isAttacking = true;
			if (LocalPlayerPosition > 0) //Left Attack
			{
				attackSide = -1;
				eventAnimator.Play("l-light-attack");
				lTailAnimator.Set(LIGHT_ATTACK_PARAMETER, true);
			}
			else
			{
				attackSide = 1;
				eventAnimator.Play("r-light-attack");
				rTailAnimator.Set(LIGHT_ATTACK_PARAMETER, true);
			}
		}

		private bool IsHeavyAttackActive => isAttacking && attackCounter == 0;
		private readonly string HEAVY_ATTACK_PARAMETER = "parameters/HeavyAttackState/current";
		private readonly string ROOT_HEAVY_ATTACK_PARAMETER = "parameters/HeavyAttack/active";
		private void HeavyAttack()
		{
			attackCounter = 0;
			isAttacking = true;
			if (LocalPlayerPosition > 0) //Left Attack
			{
				attackSide = -1;
				eventAnimator.Play("l-heavy-attack");
				lTailAnimator.Set(HEAVY_ATTACK_PARAMETER, 1);
			}
			else
			{
				attackSide = 1;
				eventAnimator.Play("r-heavy-attack");
				rTailAnimator.Set(HEAVY_ATTACK_PARAMETER, 1);
			}

			rootAnimator.Set(ROOT_HEAVY_ATTACK_PARAMETER, true);
		}

		public void FinishHeavyAttack(bool forced = default)
		{
			if (!forced && (damageState == DamageState.Hitstun || Character.Lockon.IsHomingAttacking)) return;

			StopStrike();
			FinishAttack();

			//Disables all hurtboxes
			eventAnimator.Play("disable-hurtbox-03");
			eventAnimator.Advance(0);

			if (attackSide == 1)
				rTailAnimator.Set(HEAVY_ATTACK_PARAMETER, 2);
			else if (attackSide == -1)
				lTailAnimator.Set(HEAVY_ATTACK_PARAMETER, 2);

		}

		private readonly string EYE_PARAMETER = "parameters/EyeState/current";
		private void EyeAttack()
		{
			eyeAnimator.Set(EYE_PARAMETER, 1); //Start biting
			rootAnimator.Set(EYE_PARAMETER, 1); //Open eye cage

			//Unparent eye
			flyingEyeRoot.GetParent().CallDeferred("remove_child", flyingEyeRoot);
			GetTree().Root.CallDeferred("add_child", flyingEyeRoot);
			flyingEyeRoot.SetDeferred("global_transform", flyingEyeRoot.GlobalTransform);

			isAttacking = true;
			isStriking = true;
		}

		private void FinishEyeAttack()
		{
			eyeAnimator.Set(EYE_PARAMETER, 0); //Reset
			rootAnimator.Set(EYE_PARAMETER, 0); //Close eye cage

			flyingEyeRoot.GetParent().CallDeferred("remove_child", flyingEyeRoot);
			flyingEyeBone.CallDeferred("add_child", flyingEyeRoot);
			flyingEyeRoot.SetDeferred("transform", Transform3D.Identity);
			flyingEyeHurtbox.Monitorable = flyingEyeHurtbox.Monitoring = false;

			FinishAttack();
		}

		public void SetImpactPosition(NodePath n)
		{
			Vector3 p = GetNode<Node3D>(n).GlobalPosition;
			p.y = 0;
			impactEffect.GlobalPosition = p;
		}
		#endregion

		[Export]
		private NodePath[] eyePaths;
		private Node3D[] eyes; //Generic eyes that always track the player
		[Export]
		private Node3D flyingEye; //Actual eyeball
		[Export]
		private Area3D flyingEyeHurtbox; //Eyeball hurtbox
		[Export]
		private Node3D flyingEyeRoot; //Position of the entire flying eye
		[Export]
		private Node3D flyingEyeBone; //Position in the body

		private void UpdateEyes()
		{
			//Update the eyes to always look at the player
			Vector3 delta = GlobalPosition - Character.GlobalPosition;
			float angle = this.Forward().Flatten().AngleTo(delta.Flatten());

			for (int i = 0; i < eyes.Length; i++)
				eyes[i].LookAt(Character.GlobalPosition);

			flyingEye.LookAt(Character.GlobalPosition);
		}

		public void OnHitboxCollision(Area3D a) //Player hit the boss's hitbox
		{
			if (!a.IsInGroup("player")) return;
			if ((damageState == DamageState.Hitstun && Character.VerticalSpd >= 0) || Character.Lockon.IsHomingAttacking) return; //Player's homing attack always takes priority.

			if (Character.Skills.IsSpeedBreakActive)
			{
				Character.Skills.ToggleSpeedBreak();
				Character.Knockback(true); //No damage, just knockback
			}
			else
				Character.Knockback();
		}

		private enum DamageState
		{
			Normal, //Not taking any damage
			Hitstun, //Player is bouncing on the tail
			Knockback //Sliding backwards
		}
		private DamageState damageState;
		private readonly float KNOCKBACK = 80.0f;
		public void OnHurtboxCollision(Area3D a) //Player hit the boss's hurtbox
		{
			if (!a.IsInGroup("player")) return;
			if (!Character.Lockon.IsHomingAttacking) return; //Player isn't attacking

			currentHealth--; //Take damage
			if (currentHealth == 0) //Check for defeat
			{
				return;
			}

			if (!isSecondPhaseActive)
			{
				MoveSpeed = KNOCKBACK; //Add some knockback
				damageState = DamageState.Knockback;

				if (IsHeavyAttackActive)
					FinishHeavyAttack(true);
			}

			eventAnimator.Play("damage");
			Character.Lockon.StartBounce();
		}

		public void OnTraversalHurtboxCollision(Area3D a, bool hitFarEye) //Hit one of the eyes on the tail. Doesn't deal damage.
		{
			if (!a.IsInGroup("player")) return;
			if (!Character.Lockon.IsHomingAttacking) return; //Player isn't attacking

			Character.Lockon.StartBounce();
			damageState = DamageState.Hitstun;

			//Disable hurtboxes so the player can't just bounce on the same eye infinitely
			eventAnimator.Play(hitFarEye ? "disable-hurtbox-01" : "disable-hurtbox-02");
			eventAnimator.Advance(0);
		}
	}
}
