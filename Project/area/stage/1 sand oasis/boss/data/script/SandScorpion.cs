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
		public NodePath pathFollower;
		private PathFollow3D _pathFollower;


		[Export]
		public int maxHealth;
		private int currentHealth;

		[Export]
		public NodePath rootAnimator;
		private AnimationTree _rootAnimator;
		[Export]
		public NodePath lTailAnimator;
		private AnimationTree _lTailAnimator;
		[Export]
		public NodePath rTailAnimator;
		private AnimationTree _rTailAnimator;

		private bool isActive;
		private CharacterController Player => CharacterController.instance;

		private float attackTimer; //Timer for attacks
		private readonly float ATTACK_INTERVAL = .5f; //How long to wait between attacks

		private bool isSecondPhaseActive;
		private float phaseRotation;
		private float phaseRotationVelocity;
		private readonly float PHASE_TRANSITION_SPEED = .8f;

		public override void _Ready()
		{
			currentHealth = maxHealth;
			_pathFollower = GetNode<PathFollow3D>(pathFollower);

			_rootAnimator = GetNode<AnimationTree>(rootAnimator);
			_lTailAnimator = GetNode<AnimationTree>(lTailAnimator);
			_rTailAnimator = GetNode<AnimationTree>(rTailAnimator);
			//_rootAnimator.Active = _lTailAnimator.Active = _rTailAnimator.Active = true; //Activate animation trees

			SetUpMissiles();
			SetUpEyes();
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
				if (Mathf.IsZeroApprox(Player.MoveSpd))
					return; //Wait for the player to do something

				isActive = true;
			}

			UpdatePosition();
			UpdateRotation();
			UpdateMissiles();
		}

		private float MoveSpeed { get; set; }
		private float spdVelocity;
		private readonly float TRACTION = .6f;
		private readonly float FRICTION = .8f;
		private readonly float MOVESPEED = 40.0f;

		private float currentDistance; //Current distance to the player
		private readonly float ATTACK_DISTANCE = 25.0f; //Distance to start using close range attacks
		private readonly float RETREAT_DISTANCE = 50.0f; //Distance to start running away from the player
		private readonly float ADVANCE_DISTANCE = 80.0f; //Distance to start advancing the player
		private void UpdatePosition()
		{
			if (_pathFollower == null) return;

			//Technically it would be better to use the pathfollower's progress to calculate distance, but pathfollowers seem broken right now
			Vector3 delta = GlobalPosition - Player.GlobalPosition;
			currentDistance = delta.Flatten().Length();

			if (currentDistance >= RETREAT_DISTANCE && currentDistance <= ADVANCE_DISTANCE) //Waiting for the player
				MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, 0, ref spdVelocity, FRICTION);
			else
			{
				float speedFactor;
				if (currentDistance < RETREAT_DISTANCE)
					speedFactor = 1f - Mathf.Clamp((currentDistance - ATTACK_DISTANCE) / ATTACK_DISTANCE, 0f, 1f);
				else
					speedFactor = -Mathf.Clamp((currentDistance - ADVANCE_DISTANCE) * .1f, 0f, 1f);

				MoveSpeed = ExtensionMethods.SmoothDamp(MoveSpeed, MOVESPEED * speedFactor, ref spdVelocity, TRACTION);

				//GD.Print($"{currentDistance} {speedFactor}");
			}

			_pathFollower.Progress += MoveSpeed * PhysicsManager.physicsDelta;
			GlobalPosition = _pathFollower.GlobalPosition;
		}

		private void UpdateRotation()
		{
			if (isSecondPhaseActive)
				phaseRotation = ExtensionMethods.SmoothDampAngle(phaseRotation, 0, ref phaseRotationVelocity, PHASE_TRANSITION_SPEED);
			else
				phaseRotation = Mathf.Pi;

			float facingAngle = _pathFollower.Forward().Flatten().AngleTo(Vector2.Down) - phaseRotation;
			GlobalRotation = Vector3.Up * facingAngle;
		}


		[Export]
		public NodePath[] missilePositions; //Where to fire missiles from
		private Node3D[] _missilePositions;
		[Export]
		public PackedScene missileScene; //Missile prefab
		private readonly Array<Missile> missilePool = new Array<Missile>(); //Pool of missiles
		private readonly int MISSILE_COUNT = 5; //Same as the original game, only 5 missiles can be fired at a time

		private void SetUpMissiles()
		{
			for (int i = 0; i < MISSILE_COUNT; i++)
				missilePool.Add(missileScene.Instantiate<Missile>());

			_missilePositions = new Node3D[missilePositions.Length];
			for (int i = 0; i < missilePositions.Length; i++)
				_missilePositions[i] = GetNode<Node3D>(missilePositions[i]);
		}

		private bool missileGroupReset = true;
		private int missileIndex;
		private float missileTimer;
		private readonly float MISSILE_INTERVAL = .1f; //How long between missile shots
		private readonly float MISSILE_GROUP_SPACING = 2.5f; //How long between missile groups
		private void UpdateMissiles()
		{
			if (missileGroupReset && currentDistance <= ATTACK_DISTANCE) //Too close for missiles
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
				missilePool[i].SetUp();
			}

			int spawnFrom = RuntimeConstants.randomNumberGenerator.RandiRange(0, 2);
			Vector3 spawnPosition = _missilePositions[spawnFrom].GlobalPosition;
			Vector3 targetPosition = Player.GlobalPosition;
			targetPosition.y = 0; //Make sure missiles end up on the floor

			//TODO Use path follower to figure out where exactly to aim missiles

			missilePool[i].Launch(Objects.LaunchData.Create(spawnPosition, targetPosition, 5));
			GD.Print($"Spawned missile {i}");
		}

		[Export]
		public NodePath[] eyes; //Generic eyes that always track the player
		private Node3D[] _eyes;
		[Export]
		public NodePath flyingEye; //Actual eyeball
		private Node3D _flyingEye;
		[Export]
		public NodePath flyingEyeRoot; //Position of the entire flying eye
		private Node3D _flyingEyeRoot;
		[Export]
		public NodePath flyingEyeBone; //Position in the body
		private Node3D _flyingEyeBone;
		private void SetUpEyes()
		{
			_eyes = new Node3D[eyes.Length];
			for (int i = 0; i < eyes.Length; i++)
				_eyes[i] = GetNode<Node3D>(eyes[i]);

			_flyingEye = GetNode<Node3D>(flyingEye);
			_flyingEyeRoot = GetNode<Node3D>(flyingEyeRoot);
			_flyingEyeBone = GetNode<Node3D>(flyingEyeBone);
		}

		private void UpdateEyes()
		{
			//Update the eyes to always look at the player
			Vector3 delta = GlobalPosition - Player.GlobalPosition;
			float angle = this.Back().Flatten().AngleTo(delta.Flatten());

			for (int i = 0; i < _eyes.Length; i++)
				_eyes[i].LookAt(Player.GlobalPosition);

			//_flyingEye.LookAt(Player.GlobalPosition);

			//Sync transform of flying eye depending on the attack
			//_flyingEyeRoot.LookAt(Player.GlobalPosition);
		}

		private void ProgressPhase() //Switch to phase two
		{
			//Unparent the flying eye
		}

		public void OnPlayerCollision(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			if (Player.ActionState == CharacterController.ActionStates.JumpDash) return; //Player's jumpdash always takes priority.

			GD.Print("Collided with player.");


			if (Player.Skills.IsSpeedBreakActive)
			{
				Player.Skills.ToggleSpeedBreak();
				Player.MoveSpd = 0;
			}
			else
				Player.TakeDamage(this);
		}
	}
}
