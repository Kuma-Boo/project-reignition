using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class DinoTrioProcessor : Path3D
{
	public static DinoTrioProcessor Instance;

	/// <summary> How much faster the player should be compared to the dinos. </summary>
	[Export]
	public float SpeedDifference { get; private set; }
	/// <summary> List of all the dinos in the trio. </summary>
	[Export]
	private DinoTrio[] dinos;

	/// <summary> Player's offset on the curve. </summary>
	public float PlayerProgress { get; private set; }
	public bool IsSlowingDown => !Mathf.IsZeroApprox(hitRecoveryTimer);
	private PlayerController Player => StageSettings.Player;

	/// <summary> Timer to keep track of how long to wait after hitting the player. </summary>
	private float hitRecoveryTimer;
	/// <summary> Timer to determine when to attack. </summary>
	private float attackTimer;
	/// <summary> How long to wait after hitting the player. </summary>
	public const float HitRecoveryLength = 3f;
	/// <summary> How long to wait between attacks. </summary>
	public const float AttackInterval = 5f;
	public const float AttackOffset = 10.0f;

	public override void _EnterTree() => Instance = this;
	public override void _Ready()
	{
		StageSettings.Instance.ConnectRespawnSignal(this);

		for (int i = 0; i < dinos.Length; i++)
			dinos[i].Connect(DinoTrio.SignalName.DamagedPlayer, new(this, MethodName.DamagedPlayer));
	}

	public override void _PhysicsProcess(double _)
	{
		ProcessPositions();
		ProcessAttacks();
		UpdatePlayerStaggerTimer();

		for (int i = 0; i < dinos.Length; i++)
			dinos[i].ProcessDino();
	}

	private void ProcessPositions()
	{
		Vector3 localPosition = GlobalTransform.Basis.Inverse() * (Player.GlobalPosition - GlobalPosition);
		PlayerProgress = Curve.GetClosestOffset(localPosition);

		// Update reverse prevention to the position of the dino that is furthest away
		float targetProgress = Mathf.Inf;
		for (int i = 0; i < dinos.Length; i++)
		{
			if (dinos[i].Progress < targetProgress)
				targetProgress = dinos[i].Progress;
		}
	}

	private void ProcessAttacks()
	{
		if (!Mathf.IsZeroApprox(hitRecoveryTimer)) // Still recovering from an attack
			return;

		if (Player.Camera.IsCrossfading) // Don't process timer when crossfading
			return;

		for (int i = 0; i < dinos.Length; i++)
		{
			if (dinos[i].CurrentAttackState != DinoTrio.AttackStates.Inactive) // Don't update timer when a dino is already attacking
				return;
		}

		attackTimer = Mathf.MoveToward(attackTimer, 0, PhysicsManager.physicsDelta);
		if (!Mathf.IsZeroApprox(attackTimer)) // Not time to attack yet
			return;

		// Calculate which dino attacks

		int closestDinoIndex = GetAttackingDinoIndex();
		if (Player.Camera.IsBehindCamera(dinos[closestDinoIndex].GlobalPosition)) // Prevent unfair offscreen attacks
			return;

		if (Mathf.Abs(dinos[closestDinoIndex].Progress - PlayerProgress) > AttackOffset || Player.Skills.IsSpeedBreakActive) // Too far away to attack
			return;

		dinos[closestDinoIndex].StartAttack();
		attackTimer = AttackInterval; // Reset timer
	}

	private void UpdatePlayerStaggerTimer()
	{
		if (Mathf.IsZeroApprox(hitRecoveryTimer))
			return;

		hitRecoveryTimer = Mathf.MoveToward(hitRecoveryTimer, 0, PhysicsManager.physicsDelta); // Update timer
		if (Mathf.Abs(dinos[GetClosestDinoIndex()].DeltaProgress) > AttackOffset)
			hitRecoveryTimer = 0; // Player is running away; start the chase again
	}

	/// <summary> Returns the index of the dino that is the closest to the player's lane. </summary>
	private int GetAttackingDinoIndex()
	{
		Vector3 playerPosition = GlobalPosition + Curve.SampleBaked(PlayerProgress);
		float playerLocalPosition = (GlobalTransform.Basis.Inverse() * (playerPosition - Player.GlobalPosition)).X;

		float closestDeltaPosition = Mathf.Inf;
		int dinoIndex = -1;
		for (int i = 0; i < dinos.Length; i++)
		{
			float deltaPosition = Mathf.Abs(playerLocalPosition - dinos[i].HOffset);
			if (deltaPosition >= closestDeltaPosition)
				continue;

			dinoIndex = i;
			closestDeltaPosition = deltaPosition;
		}

		return dinoIndex;
	}

	/// <summary> Returns the index of the dino that is the closest to the player's progress. </summary>
	private int GetClosestDinoIndex()
	{
		int dinoIndex = 0;
		for (int i = 0; i < dinos.Length; i++)
		{
			if (dinos[dinoIndex].Progress >= dinos[i].Progress)
				continue;

			dinoIndex = i;
		}

		return dinoIndex;
	}

	private void DamagedPlayer()
	{
		// Reset timers
		hitRecoveryTimer = HitRecoveryLength;
		attackTimer = AttackInterval;
	}

	private void Respawn()
	{
		hitRecoveryTimer = 0;
		attackTimer = AttackInterval;
	}
}
