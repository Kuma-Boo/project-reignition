using Godot;
using Project.Core;
using Project.CustomNodes;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

[Tool]
public partial class SlimeMajin : Enemy
{
	[Signal] public delegate void ShockStartedEventHandler();

	[ExportSubgroup("Spawn Settings")]
	[Export] private Vector3 spawnOffset;
	[Export] private float spawnHeight = 10f;
	[Export] private GroupGpuParticles3D instantSpawnFX;
	private Vector3 initialPosition;
	private Vector3 InitialPosition => Engine.IsEditorHint() ? GlobalPosition : initialPosition;
	private Vector3 SpawnPosition => InitialPosition + spawnOffset * GlobalBasis.Inverse();
	public bool IsSpawnLaunchEnabled => !spawnOffset.IsZeroApprox() || !Mathf.IsZeroApprox(spawnHeight);
	private LaunchSettings spawnLaunchSettings;
	public LaunchSettings SpawnLaunchSettings => LaunchSettings.Create(SpawnPosition, InitialPosition, spawnHeight, true);
	private float spawnTimer;

	[ExportSubgroup("Movement Settings")]
	/// <summary> How many times the slime should jump when moving. Setting this to 0 disables movement. </summary>
	[Export(PropertyHint.Range, "0,10,1")] private int jumpCount;
	/// <summary> How far the slime should move. Setting this to 0 allows slimes to jump in place. </summary>
	[Export(PropertyHint.Range, "0,20,0.1,or_greater")] private float movementDistance = 15f;
	[Export(PropertyHint.Range, "0,1,0.1")] private float startingOffset = 0.5f;
	public bool IsMovementEnabled => jumpCount != 0;
	private float StartingOffset => Mathf.FloorToInt(jumpCount * startingOffset) / (float)jumpCount;
	public Vector3 MovementStartPosition => InitialPosition + this.Back() * movementDistance * StartingOffset;
	public Vector3 MovementEndPosition => InitialPosition + this.Forward() * movementDistance * (1f - StartingOffset);
	private int currentJumpCount;
	private bool isMovingBackwards;
	private Vector3 positionVelocity;
	private readonly float PositionSmoothing = 15.0f;

	[ExportSubgroup("Attack Settings")]
	[Export] private bool isSpitEnabled;
	[Export] private bool isShockEnabled;
	/// <summary> After how many jumps should the slime attack? Only takes effect if the slime has movement enabled. </summary>
	[Export] private int movingAttackInterval = 1;
	/// <summary> How frequently should the slime attack? Only takes effect if the slime has movement disabled. </summary>
	[Export] private float staticAttackInterval = 1.0f;
	[Export] private NodePath shockHitbox;
	private Hazard ShockHitbox { get; set; }
	private bool IsAttackingEnabled => isSpitEnabled || isShockEnabled;
	/// <summary> Timer to track things like shock windup. </summary>
	private float attackTimer;
	/// <summary> Keeps track of how many steps the slime has taken since the last attack.  </summary>
	private float attackCounter;
	/// <summary> How long the slime should rattle.  </summary>
	private readonly float ShockWindupTime = 1f;
	/// <summary> How long the shock attack should remain active.  </summary>
	private readonly float ShockAttackLength = 0.8f;
	/// <summary> When both attack modes are enabled, at which point should the slime switch to shocks?  </summary>
	private readonly float ShockRangeSquared = 100f;

	private SlimeState slimeState;
	private enum SlimeState
	{
		Unspawned,
		Spawning,
		Idle, // Also includes moving
		Spit,
		Shock,
		Defeated
	}

	private AnimationNodeStateMachinePlayback SpawnStatePlayback => AnimationTree.Get(SpawnPlayback).Obj as AnimationNodeStateMachinePlayback;
	private AnimationNodeStateMachinePlayback ShockStatePlayback => AnimationTree.Get(ShockPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName SpawnStartAnimation = "spawn-start";
	private readonly StringName SpawnEndAnimation = "spawn-end";

	private readonly StringName MoveTrigger = "parameters/move_trigger/request";
	private readonly StringName MoveTriggerActive = "parameters/move_trigger/active";
	private readonly StringName ShockTrigger = "parameters/shock_trigger/request";
	private readonly StringName ShockWarnState = "shock-warn";
	private readonly StringName ShockStartState = "shock-start";
	private readonly StringName ShockLoopState = "shock";
	private readonly StringName ShockEndState = "shock-end";
	private readonly StringName SpitTrigger = "parameters/spit_trigger/request";
	private readonly StringName SpawnTrigger = "parameters/spawn_trigger/request";
	private readonly StringName SpawnPlayback = "parameters/spawn_state/playback";
	private readonly StringName ShockPlayback = "parameters/shock_state/playback";
	private readonly StringName StateTransition = "parameters/state_transition/transition_request";

	protected override void SetUp()
	{
		if (Engine.IsEditorHint())
			return;

		ShockHitbox = GetNodeOrNull<Hazard>(shockHitbox);
		initialPosition = GlobalPosition;

		base.SetUp();

		AnimationTree.Active = true;
	}

	public override void Respawn()
	{
		slimeState = SlimeState.Unspawned;
		Root.GlobalPosition = SpawnPosition;
		attackTimer = 0;

		if (IsMovementEnabled)
		{
			currentJumpCount = Mathf.FloorToInt(jumpCount * startingOffset);
			isMovingBackwards = false;
			positionVelocity = Vector3.Zero;
			currentRotation = 0;
			rotationVelocity = 0;
			attackCounter = 0;
		}

		// Reset all animations
		AnimationTree.Set(MoveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(ShockTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(SpitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(SpawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(StateTransition, "init");

		base.Respawn();
	}

	protected override void Spawn()
	{
		if (IsActive)
			return;

		if (IsSpawnLaunchEnabled)
		{
			spawnTimer = 0;
			spawnLaunchSettings = SpawnLaunchSettings; // Cache launch settings so we don't have to keep recalculating it

			slimeState = SlimeState.Spawning;
			SpawnStatePlayback.Start(SpawnStartAnimation);
			AnimationTree.Set(SpawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
		else
		{
			// Instant spawn
			instantSpawnFX.RestartGroup();
		}

		AnimationTree.Set(StateTransition, "active");
		IsActive = true;
		base.Spawn();
	}

	protected override void UpdateEnemy()
	{
		switch (slimeState)
		{
			case SlimeState.Spawning:
				ProcessSpawn();
				break;
			case SlimeState.Idle:
				if (IsMovementEnabled)
					ProcessMovement();
				break;
			case SlimeState.Shock:
				ProcessShock();
				break;
			default:
				break;
		}

		ProcessAttackTimers();
	}

	private void ProcessMovement()
	{
		bool isMovementActive = (bool)AnimationTree.Get(MoveTriggerActive);

		if (!isMovementActive)
		{
			if (IsAttackingEnabled &&
				attackCounter >= movingAttackInterval &&
				!isMovingBackwards &&
				Player.PathFollower.GetProgress(GlobalPosition) > Player.PathFollower.Progress)
			{
				StartAttack();
				return;
			}

			// Start jumping
			currentJumpCount += isMovingBackwards ? -1 : 1;
			attackCounter++;
			AnimationTree.Set(MoveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

			if (currentJumpCount == jumpCount + 1)
			{
				// Reached the end of movement; turn around
				currentJumpCount = jumpCount;
				isMovingBackwards = true;
			}
			else if (currentJumpCount == -1)
			{
				// Turn forward again
				currentJumpCount = 0;
				isMovingBackwards = false;
			}

			return;
		}

		Vector3 targetPosition = MovementStartPosition.Lerp(MovementEndPosition, currentJumpCount / (float)jumpCount);
		GlobalPosition = GlobalPosition.SmoothDamp(targetPosition, ref positionVelocity, PositionSmoothing * PhysicsManager.physicsDelta);

		float targetRotation = isMovingBackwards ? Mathf.Pi : 0f;
		ProcessRotation(targetRotation);
	}

	private void ProcessAttackTimers()
	{
		if (!IsAttackingEnabled || slimeState != SlimeState.Idle)
			return;

		if (IsMovementEnabled) // Attack timer is handled via attackCounter
			return;

		attackTimer += PhysicsManager.physicsDelta;
		if (attackTimer > staticAttackInterval)
			StartAttack();
	}

	private void StartAttack()
	{
		if (isShockEnabled &&
			(!isSpitEnabled || GlobalPosition.DistanceSquaredTo(Player.CenterPosition) < ShockRangeSquared))
		{
			StartShockAttack();
			return;
		}

		if (isSpitEnabled)
			StartSpitAttack();
	}

	private void StartSpitAttack()
	{
		slimeState = SlimeState.Spit;
		AnimationTree.Set(SpitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void StartShockAttack()
	{
		attackTimer = 0;
		slimeState = SlimeState.Shock;
		ShockStatePlayback.Start(ShockWarnState);
		AnimationTree.Set(ShockTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		EmitSignal(SignalName.ShockStarted);
	}

	private void ProcessShock()
	{
		if (ShockStatePlayback.GetCurrentNode() == ShockEndState)
			return;

		if (ShockStatePlayback.GetCurrentNode() == ShockLoopState)
		{
			// Process shock length
			attackTimer += PhysicsManager.physicsDelta;
			if (attackTimer < ShockAttackLength)
				return;

			attackTimer = 0;
			ShockStatePlayback.Travel(ShockEndState);
			return;
		}

		if (attackTimer > ShockWindupTime)
			return;

		attackTimer += PhysicsManager.physicsDelta;
		if (attackTimer < ShockWindupTime) // Still winding up
			return;

		attackTimer = 0;
		ShockStatePlayback.Travel(ShockStartState);
	}

	/// <summary> Resnaps the slime to the ground. Prevents clipping into the ground (called from the animator). </summary>
	private void GroundSnap()
	{
		// Prevent clipping into the ground
		RaycastHit hit = this.CastRay(GlobalPosition + this.Up() * 0.5f, this.Down(), Runtime.Instance.environmentMask);
		if (hit)
			GlobalPosition = hit.point;
	}

	private void ProcessSpawn()
	{
		spawnTimer = Mathf.MoveToward(spawnTimer, spawnLaunchSettings.TotalTravelTime, PhysicsManager.physicsDelta);
		Root.GlobalPosition = spawnLaunchSettings.InterpolatePositionTime(spawnTimer);

		if (spawnLaunchSettings.IsLauncherFinished(spawnTimer))
		{
			ReturnToIdle();
			SpawnStatePlayback.Start(SpawnEndAnimation);
		}
	}

	/// <summary> Called from animations whenever the slime finishes an attack. </summary>
	private void ReturnToIdle()
	{
		attackCounter = 0;
		attackTimer = 0;
		slimeState = SlimeState.Idle;
	}

	protected override void Defeat()
	{
		SetHitboxStatus(false);
		AnimationTree.Set(StateTransition, "defeated");

		base.Defeat();
	}
}
