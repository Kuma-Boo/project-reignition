using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Bosses;

public partial class CaptainBemoth : PathFollow3D
{
	[ExportGroup("Components")]
	[Export] private AnimationTree animator;
	[Export] private Node3D root;
	[Export] private JumpTrigger jumpTrigger;

	[Export] private CaptainBemothHorn[] horns;

	// Attacks
	[Export] private BossBombAttack[] bombs;
	[Export] private BossWaveAttack waveLeft;
	[Export] private BossWaveAttack waveRight;
	private Path3D bossPath;

	private PlayerController Player => StageSettings.Player;

	private int currentHealth;
	private readonly int MaxHealth = 4;
	private BemothState currentState = BemothState.Idle;
	private enum BemothState
	{
		Introduction,
		Idle,
		Damaged,
		BombAttack,
		WaveAttack,
		ChargeAttack,
		ShockAttack,
		Defeated
	}

	public override void _Ready()
	{
		animator.Active = true;
		bossPath = GetParent<Path3D>();

		bombs[^1].Exploded += EnterIdleState; // Return to idle when the last bomb explodes
		StageSettings.Instance.Respawned += Respawn;
		// TODO Play introduction cutscene StageSettings.Instance.LevelStarted += StartIntroduction;
		StartBattle();

		foreach (CaptainBemothHorn horn in horns)
		{
			horn.Jolted += TakeHornDamage;
			horn.Popped += TakeDamage;
			horn.Jumped += () => LaunchPlayer(true);
		}
	}

	private readonly StringName IntroCutsceneID = "ps_boss_intro";
	private readonly StringName DefeatCutsceneID = "ps_boss_defeat";
	private readonly StringName IntroTrigger = "parameters/intro_trigger/request";
	private void StartIntroduction()
	{
		Player.Deactivate();
		animator.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		// Reset positions so everything lines up in the cutscene
		root.GlobalPosition = Vector3.Zero;
		root.GlobalBasis = Basis.Identity;
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

		TransitionManager.instance.TransitionProcess += StartBattle;
		SaveManager.ActiveGameData.AllowSkippingCutscene(IntroCutsceneID);
	}

	private void StartBattle()
	{
		TransitionManager.instance.TransitionProcess -= StartBattle;
		animator.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		Respawn();

		TransitionManager.FinishTransition();
		Player.Activate();
	}

	private void Respawn()
	{
		EnterIdleState();
		currentHealth = MaxHealth;
		Progress = StopDistance;
		HOffset = 0;
		trackingVelocity = 0;

		currentRotation = 0;
		rotationVelocity = 0;

		isAttackActive = false;
		bombAttackCounter = 0;
		waveAttackCounter = 0;

		// Reset local position
		root.Position = Vector3.Zero;
		root.Basis = Basis.Identity;

		animator.Set(BombTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(WaveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(HornDamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		waveLeft.Deactivate();
		waveRight.Deactivate();

		foreach (CaptainBemothHorn horn in horns)
			horn.Respawn();
	}

	public override void _PhysicsProcess(double _)
	{
		switch (currentState)
		{
			case BemothState.Introduction:
				if ((Input.IsActionJustPressed("button_pause") || Input.IsActionJustPressed("button_jump")) &&
					SaveManager.ActiveGameData.CanSkipCutscene(IntroCutsceneID))
				{
					FinishIntroduction();
				}
				return;
			case BemothState.Defeated:
				if ((Input.IsActionJustPressed("button_pause") || Input.IsActionJustPressed("button_jump")) &&
					SaveManager.ActiveGameData.CanSkipCutscene(DefeatCutsceneID))
				{
					GD.PushError("Defeat is unimplemented!");
					// TODO AnimationTree.Set(DefeatSeek, 20);
				}
				return;
			case BemothState.Idle:
				ProcessIdleState();
				break;
			case BemothState.BombAttack:
				ProcessBombState();
				break;
			case BemothState.WaveAttack:
				ProcessWaveState();
				break;
			case BemothState.ChargeAttack:
				ProcessChargeAttackState();
				break;
			default:
				break;
		}

		UpdateRotation();
		ProcessMovement();
	}

	private bool isFacingForward;
	private float currentRotation;
	private float rotationVelocity;
	private readonly float RotationSmoothing = 20f;
	private void UpdateRotation()
	{
		if (currentHealth == 0)
			return;

		currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, isFacingForward ? Mathf.Pi : 0, ref rotationVelocity, RotationSmoothing * PhysicsManager.physicsDelta);
		root.Rotation = Vector3.Up * currentRotation;
	}

	private bool IsOpen => ((StringName)animator.Get(CloseState)).Equals("enabled");
	private bool IsClosed => ((StringName)animator.Get(CloseState)).Equals("disabled");
	private readonly StringName CloseState = "parameters/close_transition/current_state";
	private readonly StringName CloseTransition = "parameters/close_transition/transition_request";
	private void Open() => animator.Set(CloseTransition, "open");
	private void Close() => animator.Set(CloseTransition, "close");

	private void EnterIdleState()
	{
		if (!IsClosed)
			Close();

		isFacingForward = currentHealth == 1; // Only face the player when almost dead
		currentState = BemothState.Idle;
		attackTimer = AttackTimerInterval;
		EnableHornHurtboxes();
	}

	private void ProcessIdleState()
	{
		HOffset = ExtensionMethods.SmoothDamp(HOffset, 0, ref trackingVelocity, TrackingSmoothing * PhysicsManager.physicsDelta);
		attackTimer -= PhysicsManager.physicsDelta;
		if (attackTimer > 0)
			return;

		attackTimer = 0;
		StartAttack();
	}

	private float moveSpeed;
	private float moveSpeedVelocity;
	private readonly float MoveSpeedSmoothing = 10f;
	private readonly float BaseMoveSpeed = 15f;
	private readonly float ChargeSpeed = -60f;
	private readonly float MinimumDistance = 2f;
	private readonly float MinimumDistanceSmoothingStart = 10f;
	private readonly float StopDistance = 30f;
	private readonly float StopDistanceSmoothingStart = 25f;
	private readonly float ShockAttackSpeed = 5f;
	private readonly float WaveAttackDistance = 20f;

	/// <summary> Returns the progress difference between the player and the boss. </summary>
	public float GetDeltaProgress()
	{
		float bossProgress = Player.PathFollower.GetProgress(GlobalPosition);
		float deltaProgress = bossProgress - Player.PathFollower.Progress;
		if (deltaProgress < -Player.PathFollower.ActivePath.Curve.GetBakedLength() * .5f)
			deltaProgress += Player.PathFollower.ActivePath.Curve.GetBakedLength();
		return deltaProgress;
	}

	/// <summary> Try to match the player's distance. </summary>
	private void ProcessMovement()
	{
		if (currentHealth == 0)
			return;

		float deltaProgress = GetDeltaProgress();
		float speedSmoothing = MoveSpeedSmoothing;
		float speedRatio = 1f - Mathf.Clamp((deltaProgress - StopDistanceSmoothingStart) / (StopDistance - StopDistanceSmoothingStart), 0f, 1f);
		float targetMoveSpeed = BaseMoveSpeed * speedRatio;

		if (currentState == BemothState.WaveAttack)
		{
			speedRatio = Mathf.Clamp((deltaProgress - StopDistanceSmoothingStart) / (WaveAttackDistance - StopDistanceSmoothingStart), 0f, 1f);
			targetMoveSpeed = BaseMoveSpeed * speedRatio;
			targetMoveSpeed += Player.MoveSpeed * (Player.IsMovingBackward ? -1f : 1f);
		}
		else if (currentState == BemothState.ChargeAttack)
		{
			targetMoveSpeed = CalculateChargingMoveSpeed(deltaProgress);
		}
		else if (currentState == BemothState.ShockAttack)
		{
			targetMoveSpeed = ShockAttackSpeed;
		}
		else if (Player.IsMovingBackward)
		{
			targetMoveSpeed -= Player.MoveSpeed;
		}
		else if (deltaProgress <= MinimumDistanceSmoothingStart && !Player.IsHomingAttacking)
		{
			float smoothingRatio = 1f - ((deltaProgress - MinimumDistance) / (MinimumDistanceSmoothingStart - MinimumDistance));
			targetMoveSpeed += Player.MoveSpeed * smoothingRatio;
			speedSmoothing = Mathf.Lerp(speedSmoothing, 0, smoothingRatio);
		}

		moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, targetMoveSpeed, ref moveSpeedVelocity, speedSmoothing * PhysicsManager.physicsDelta);
		Progress += moveSpeed * PhysicsManager.physicsDelta;
	}

	private float CalculateChargingMoveSpeed(float deltaProgress)
	{
		if (isChargeAttackCharging || !IsClosed)
		{
			// Move to charging distance
			return BaseMoveSpeed + Player.MoveSpeed * 1.2f * (Player.IsMovingBackward ? -1f : 1f);
		}

		if (isAttackActive)
			return ChargeSpeed + Player.MoveSpeed * (Player.IsMovingBackward ? -1f : 1f);

		if (deltaProgress < StopDistance)
			return BaseMoveSpeed + Player.MoveSpeed * (Player.IsMovingBackward ? -1f : 1f);

		return 0;
	}

	private void EnableHornHurtboxes()
	{
		DisableHornHurtboxes();

		// Enable the correct horns
		horns[0].EnableLockon();

		if (currentHealth < MaxHealth)
		{
			horns[1].EnableLockon();
			horns[2].EnableLockon();
		}

		/*
		if (currentHealth == 1)
			horns[3].EnableLockon();
		*/
	}

	private void DisableHornHurtboxes()
	{
		foreach (CaptainBemothHorn horn in horns)
			horn.DisableLockon();
	}

	private readonly StringName HornDamageTrigger = "parameters/horn_damage_trigger/request";
	private void TakeHornDamage()
	{
		animator.Set(HornDamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private readonly StringName DamageTrigger = "parameters/damage_trigger/request";
	private void TakeDamage()
	{
		currentHealth--;
		GD.Print(currentHealth);
		animator.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	#region Attacks
	private bool isAttackActive;
	private float attackTimer;
	private readonly float AttackTimerInterval = 3f;

	private void StartAttack()
	{
		if (currentHealth == MaxHealth || GetDeltaProgress() >= BombAttackRange)
		{
			EnterBombAttackState();
			return;
		}

		if (currentHealth > 1)
		{
			if (Runtime.randomNumberGenerator.Randf() > 0.5f)
				EnterBombAttackState();
			else
				EnterWaveAttackState();

			return;
		}

		if (Runtime.randomNumberGenerator.Randf() > 0.5f)
			EnterWaveAttackState();
		else
			EnterChargeAttackState();
	}

	private void FinishAttack() => isAttackActive = false;

	private int bombAttackCounter;
	private readonly float BombAttackRange = 30f;
	private void EnterBombAttackState()
	{
		bombAttackCounter = 0;
		currentState = BemothState.BombAttack;
	}

	private void ProcessBombState()
	{
		if (isAttackActive)
			return;

		if (bombAttackCounter > bombs.Length)
			return;

		StartBombAttack();
	}

	private readonly StringName BombTrigger = "parameters/bomb_trigger/request";
	private void StartBombAttack()
	{
		bombAttackCounter++;
		if (bombAttackCounter > bombs.Length)
			return;

		isAttackActive = true;
		bombs[bombAttackCounter - 1].Respawn(); // Prep the next bomb
		animator.Set(BombTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void EmitBomb()
	{
		if (currentState != BemothState.BombAttack) // Canceled?
			return;

		bombs[bombAttackCounter - 1].StartWindup();
	}

	private int waveAttackCounter;
	/// <summary> Direction of the wave attack. </summary>
	private readonly StringName WaveTransition = "parameters/wave_transition/transition_request";
	/// <summary> Activation of the wave attack. </summary>
	private readonly StringName WaveTrigger = "parameters/wave_trigger/request";
	private readonly int FinalWaveAttackCounter = 3;
	private void EnterWaveAttackState()
	{
		waveAttackCounter = 0;
		currentState = BemothState.WaveAttack;
		isFacingForward = true; // Turn to face the player
		if (IsClosed)
			Open();

		DisableHornHurtboxes();
	}

	private void ProcessWaveState()
	{
		if (!IsOpen)
			return;

		if (isAttackActive) // Already attacking (Attack times are handled via animations)
			return;

		StartWaveAttack();
	}

	private void StartWaveAttack()
	{
		isAttackActive = true;
		waveAttackCounter++;

		if (waveAttackCounter == FinalWaveAttackCounter) // Both sides
			animator.Set(WaveTransition, "both");
		else
			animator.Set(WaveTransition, Runtime.randomNumberGenerator.Randf() > .5f ? "left" : "right");

		animator.Set(WaveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	/// <summary> Activate the actual wave objects. -1 => left, 1 => right, 0 => both. </summary>
	private void ActivateWave(int direction)
	{
		// Activates depending on whether we're NOT activating the other side exclusively
		if (direction != -1)
			waveRight.Activate(Progress);

		if (direction != 1)
			waveLeft.Activate(Progress);
	}

	private bool isChargeAttackCharging;
	private void EnterChargeAttackState()
	{
		if (IsClosed)
			Open();

		isFacingForward = true;
		isChargeAttackCharging = true;
		currentState = BemothState.ChargeAttack;
		DisableHornHurtboxes();
	}

	private float trackingVelocity;
	private readonly float TrackingSmoothing = 20f;
	private readonly float ChargeTrackingSmoothing = 10f;
	private void ProcessChargeAttackState()
	{
		float deltaProgress = GetDeltaProgress();
		if (isChargeAttackCharging)
		{
			HOffset = ExtensionMethods.SmoothDamp(HOffset, -Player.PathFollower.LocalPlayerPositionDelta.X, ref trackingVelocity, ChargeTrackingSmoothing * PhysicsManager.physicsDelta);
			if (deltaProgress >= StopDistance)
			{
				if (IsOpen)
				{
					isChargeAttackCharging = false;
					Close();
				}
			}

			return;
		}

		if (isAttackActive)
		{
			if (deltaProgress < 1f) // Stop charge
				isAttackActive = false;

			return;
		}

		if (deltaProgress >= StopDistance)
		{
			HOffset = ExtensionMethods.SmoothDamp(HOffset, -Player.PathFollower.LocalPlayerPositionDelta.X, ref trackingVelocity, ChargeTrackingSmoothing * PhysicsManager.physicsDelta);
			// Wait until we're fully closed before charging
			if (IsClosed) // Start charge
				isAttackActive = true;

			return;
		}

		if (deltaProgress > MinimumDistance)
			EnterIdleState();
	}

	private void EnterShockAttackState()
	{
		// Cancel all other attacks
		foreach (BossBombAttack bomb in bombs)
		{
			if (bomb.IsActive) // Already flying
				continue;

			bomb.CancelLaunch();
		}

		attackTimer = 0;
		currentState = BemothState.ShockAttack;
	}
	#endregion

	private readonly float LaunchFallHeight = 8f;
	private readonly float LaunchProgressSearchInterval = 10f;
	private readonly float LaunchHeightCheckLength = 20f;

	private void LaunchPlayer(bool isJump)
	{
		float initialProgress = Player.PathFollower.Progress;
		RaycastHit hit = new();

		while (!hit)
		{
			Player.PathFollower.Progress -= LaunchProgressSearchInterval;
			hit = this.CastRay(Player.PathFollower.GlobalPosition + Vector3.Up * LaunchHeightCheckLength * 0.5f,
				Vector3.Down * LaunchHeightCheckLength,
				Runtime.Instance.environmentMask);
		}

		Player.PathFollower.Progress = initialProgress;
		jumpTrigger.GlobalPosition = hit.point;
		jumpTrigger.jumpHeight = isJump ? 10f : 1f;
		if (!isJump) // Teleport to the proper location
		{
			Player.GlobalPosition = jumpTrigger.GlobalPosition + Vector3.Up * LaunchFallHeight;
			Player.PathFollower.Resync();
		}


		jumpTrigger.Activate();
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);

		if (isJump)
		{
			Player.Animator.StartSpin(3f);
			Player.Effect.StartSpinFX();
		}

		// TODO Finish shock if it's already charged
		EnterIdleState();

		foreach (CaptainBemothHorn horn in horns)
		{
			if (horn.IsPopping)
				horn.Despawn();
		}
	}
}