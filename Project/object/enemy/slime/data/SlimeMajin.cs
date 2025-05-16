using Godot;
using Project.Core;

namespace Project.Gameplay;

[Tool]
public partial class SlimeMajin : Enemy
{
	[ExportSubgroup("Spawn Settings")]
	[Export] private Vector3 spawnOffset;
	[Export] private float spawnHeight = 10f;
	private Vector3 initialPosition;
	private Vector3 InitialPosition => Engine.IsEditorHint() ? GlobalPosition : initialPosition;
	private Vector3 SpawnPosition => InitialPosition + spawnOffset * GlobalBasis.Inverse();
	public bool IsSpawnLaunchEnabled => !spawnOffset.IsZeroApprox();
	private LaunchSettings spawnLaunchSettings;
	public LaunchSettings SpawnLaunchSettings => LaunchSettings.Create(SpawnPosition, InitialPosition, spawnHeight, true);
	private float spawnTimer;

	[ExportSubgroup("Movement Settings")]
	/// <summary> How many times the slime should jump when moving. Setting this to 0 disables movement. </summary>
	[Export(PropertyHint.Range, "0,10,1")] private int jumpCount;
	/// <summary> How far the slime should move. Setting this to 0 allows slimes to jump in place. </summary>
	[Export(PropertyHint.Range, "0,20,0.1,or_greater")] private float movementDistance = 15f;
	[Export(PropertyHint.Range, "0,1,0.1")] private float movementOffset = 0.5f;
	public bool IsMovementEnabled => jumpCount != 0;
	public Vector3 MovementStartPosition => InitialPosition + this.Forward() * movementDistance * (1f - movementOffset);
	public Vector3 MovementEndPosition => InitialPosition + this.Back() * movementDistance * movementOffset;

	[ExportSubgroup("Attack Settings")]
	[Export] private bool isSpitEnabled;
	[Export] private bool isShockEnabled;
	[Export] private float shockRange = -1;
	/// <summary> How long to stay in a shocking state. Set to 0  </summary>
	[Export] private float shockLength;

	private SlimeState slimeState;
	private enum SlimeState
	{
		Unspawned,
		Spawning,
		Idle,
		Spit,
		ShockWarning,
		ShockActive,
		Defeated
	}

	private AnimationNodeStateMachinePlayback SpawnStatePlayback => AnimationTree.Get(SpawnPlayback).Obj as AnimationNodeStateMachinePlayback;
	private AnimationNodeStateMachinePlayback ShockStatePlayback => AnimationTree.Get(ShockPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName SpawnStartAnimation = "spawn-start";
	private readonly StringName SpawnEndAnimation = "spawn-end";

	private readonly StringName MoveTrigger = "parameters/move_trigger/request";
	private readonly StringName ShockTrigger = "parameters/shock_trigger/request";
	private readonly StringName SpitTrigger = "parameters/spit_trigger/request";
	private readonly StringName SpawnTrigger = "parameters/spawn_trigger/request";
	private readonly StringName SpawnPlayback = "parameters/spawn_state/playback";
	private readonly StringName ShockPlayback = "parameters/shock_state/playback";
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";

	protected override void SetUp()
	{
		if (Engine.IsEditorHint())
			return;

		initialPosition = GlobalPosition;
		GlobalPosition = SpawnPosition;

		base.SetUp();

		AnimationTree.Active = true;
	}

	public override void Respawn()
	{
		slimeState = SlimeState.Unspawned;

		// Reset all animations
		AnimationTree.Set(MoveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(ShockTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(SpitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(SpawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(DefeatTransition, "disabled");

		base.Respawn();
	}

	protected override void Spawn()
	{
		if (IsActive)
			return;

		spawnTimer = 0;
		spawnLaunchSettings = SpawnLaunchSettings; // Cache launch settings so we don't have to keep recalculating it

		slimeState = SlimeState.Spawning;
		SpawnStatePlayback.Start(SpawnStartAnimation);
		AnimationTree.Set(SpawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
			default:
				break;
		}
	}

	private void ProcessSpawn()
	{
		spawnTimer = Mathf.MoveToward(spawnTimer, spawnLaunchSettings.TotalTravelTime, PhysicsManager.physicsDelta);
		GlobalPosition = spawnLaunchSettings.InterpolatePositionTime(spawnTimer);

		if (spawnLaunchSettings.IsLauncherFinished(spawnTimer))
		{
			slimeState = SlimeState.Idle;
			SpawnStatePlayback.Start(SpawnEndAnimation);
		}
	}
}
