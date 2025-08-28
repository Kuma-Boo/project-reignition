using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Bosses;

public partial class CaptainBemoth : PathFollow3D
{
	[ExportGroup("Components")]
	[Export] private AnimationTree animator;
	[Export] private AnimationPlayer eventAnimator;
	[Export] private Node3D root;
	[Export] private CameraTrigger jumpCameraTrigger;
	[Export] private CameraTrigger mainCameraTrigger;
	[Export] private JumpTrigger jumpTrigger;
	[Export] private Node3D shockAttackRoot;
	private int damageDialogIndex;
	private int hintDialogIndex = -1;
	[Export] private DialogTrigger[] damageDialogs;
	[Export] private DialogTrigger[] hintDialogs;

	[Export] private CaptainBemothHorn[] horns;

	// Attacks
	[Export] private BossBombAttack[] bombs;
	[Export] private BossWaveAttack waveLeft;
	[Export] private BossWaveAttack waveRight;
	private Path3D bossPath;

	private PlayerController Player => StageSettings.Player;

	private int currentHealth;
	private readonly int MaxHealth = 4;
	private BemothState currentState;
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

		for (int i = 0; i < horns.Length; i++)
		{
			horns[i].HangStarted += EnterShockAttackState;
			horns[i].Jolted += TakeHornDamage;
			horns[i].Popped += TakeDamage;
			horns[i].Jumped += () => LaunchPlayer(true);
		}

		bombs[^1].Exploded += EnterIdleState; // Return to idle when the last bomb explodes
		StageSettings.Instance.Respawned += Respawn;
		StageSettings.Instance.LevelStarted += StartIntroduction;
	}

	private readonly string IntroCutsceneID = "ps_boss_intro";
	private readonly string DefeatCutsceneID = "ps_boss_defeat";
	private readonly string IntroTrigger = "parameters/intro_trigger/request";
	private void StartIntroduction()
	{
		Player.Deactivate();
		animator.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		waveLeft.Deactivate();
		waveRight.Deactivate();

		// Reset positions so everything lines up in the cutscene
		root.GlobalTransform = Transform3D.Identity;
		currentState = BemothState.Introduction;
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
		eventAnimator.Play("finish-intro");
		eventAnimator.Advance(0.0);
	}

	private void StartBattle()
	{
		TransitionManager.instance.TransitionProcess -= StartBattle;
		animator.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		Respawn();

		TransitionManager.FinishTransition();
		Player.Activate();
		attackTimer = ShortAttackInterval;
	}

	private readonly string DefeatTrigger = "parameters/defeat_trigger/request";
	private readonly string DefeatSeek = "parameters/defeat_seek/seek_request";
	private void DefeatBoss()
	{
		TransitionManager.StartTransition(new()
		{
			inSpeed = 0f,
			outSpeed = .5f,
			color = Colors.Black
		});

		Progress = 0;
		root.GlobalTransform = Transform3D.Identity;
		animator.Set(DefeatTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		Player.Skills.DisableBreakSkills();
		Player.Animator.PlayOneshotAnimation(DefeatCutsceneID);

		BonusManager.instance.QueueBonus(new(BonusType.Boss, 8000));
		Interface.PauseMenu.AllowPausing = false;
		HeadsUpDisplay.Instance.SetVisibility(false);
		currentState = BemothState.Defeated;

		TransitionManager.FinishTransition();
	}

	private void FinishDefeat()
	{
		if (!StageSettings.Instance.IsLevelIngame)
			return;

		eventAnimator.Play("finish-defeat");
		eventAnimator.Advance(0f);
		Player.Animator.CancelOneshot();
		CallDeferred(MethodName.FinishStage);
	}

	private void FinishStage()
	{
		DisablePoppedHorns();

		// Return player to starting position
		Player.Position = Vector3.Up * LaunchFallHeight;
		Player.PathFollower.Resync();
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);
		jumpTrigger.GlobalPosition = Player.GetParentNode3D().GlobalPosition;
		jumpTrigger.Activate();

		Player.Activate();
		StageSettings.Instance.FinishLevel(true);
		SaveManager.ActiveGameData.AllowSkippingCutscene(DefeatCutsceneID);
	}

	private void Respawn()
	{
		EnterIdleState();
		StopMotionBlur();
		currentHealth = MaxHealth;
		Progress = StopDistanceSmoothingStart;
		HOffset = 0;
		trackingVelocity = 0;

		currentRotation = 0;
		rotationVelocity = 0;

		damageDialogIndex = 0;

		isAttackActive = false;
		isAttackQueued = false;
		isShockAttackActive = false;
		bombAttackCounter = 0;
		waveAttackCounter = 0;
		eventAnimator.Play("RESET");

		// Reset local position
		root.Position = Vector3.Zero;
		root.Basis = Basis.Identity;

		animator.Set(BombTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(WaveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(HornDamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animator.Set(ShockTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		waveLeft.Deactivate();
		waveRight.Deactivate();

		foreach (CaptainBemothHorn horn in horns)
			horn.Respawn();

		DisableHornHurtboxes(); // Don't allow immediate attacks
	}

	public override void _PhysicsProcess(double _)
	{
		switch (currentState)
		{
			case BemothState.Introduction:
				if ((Input.IsActionJustPressed("sys_pause") || Input.IsActionJustPressed("button_jump")) &&
					SaveManager.ActiveGameData.CanSkipCutscene(IntroCutsceneID))
				{
					FinishIntroduction();
				}
				return;
			case BemothState.Defeated:
				if ((Input.IsActionJustPressed("sys_pause") || Input.IsActionJustPressed("button_jump")) &&
					SaveManager.ActiveGameData.CanSkipCutscene(DefeatCutsceneID))
				{
					animator.Set(DefeatSeek, 20f);
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
			case BemothState.ShockAttack:
				ProcessShockAttack();
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
	private readonly string CloseState = "parameters/close_transition/current_state";
	private readonly string CloseTransition = "parameters/close_transition/transition_request";
	private void Open() => animator.Set(CloseTransition, "open");
	private void Close() => animator.Set(CloseTransition, "close");

	private void EnterIdleState()
	{
		if (!IsClosed)
			Close();

		currentState = BemothState.Idle;
		isFacingForward = currentHealth == 1; // Only face the player when almost dead
		attackTimer = AttackTimerInterval;
	}

	private void ProcessIdleState()
	{
		HOffset = ExtensionMethods.SmoothDamp(HOffset, 0, ref trackingVelocity, TrackingSmoothing * PhysicsManager.physicsDelta);

		attackTimer = Mathf.MoveToward(attackTimer, 0, PhysicsManager.physicsDelta);
		if (attackTimer > 0 || isAttackDisabled)
			return;

		attackTimer = 0;
		StartAttack();
	}

	private float moveSpeed;
	private float moveSpeedVelocity;
	private readonly float MoveSpeedSmoothing = 10f;
	private readonly float DamageSpeedSmoothing = 30f;
	private readonly float BaseMoveSpeed = 20f;
	private readonly float ChargeSpeed = -100f;
	private readonly float MinimumDistance = 2f;
	private readonly float MinimumDistanceSmoothingStart = 10f;
	private readonly float StopDistance = 40f;
	private readonly float StopDistanceSmoothingStart = 35f;
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
		float speedRatio = 1f - Mathf.Clamp(deltaProgress / StopDistance, 0f, 1f);
		float targetMoveSpeed = BaseMoveSpeed * speedRatio;

		if (!Player.IsHomingAttacking)
		{
			if (currentState == BemothState.WaveAttack)
			{
				speedRatio = 1f - Mathf.Clamp(deltaProgress / WaveAttackDistance, 0f, 1f);
				targetMoveSpeed = BaseMoveSpeed * speedRatio;
				targetMoveSpeed += Player.MoveSpeed * (Player.IsMovingBackward ? -1f : 1f);
			}
			else if (currentState == BemothState.ChargeAttack)
			{
				targetMoveSpeed = CalculateChargingMoveSpeed(deltaProgress);

				if (Player.IsKnockback)
					moveSpeed = targetMoveSpeed;
			}
			else if (currentState == BemothState.ShockAttack)
			{
				targetMoveSpeed = isShockAttackActive ? 0f : BaseMoveSpeed;
			}
			else if (currentState == BemothState.Damaged)
			{
				targetMoveSpeed = 0f;
				speedSmoothing = DamageSpeedSmoothing;
			}
			else if (Player.IsMovingBackward)
			{
				targetMoveSpeed -= Player.MoveSpeed;
			}
			else if (deltaProgress <= MinimumDistanceSmoothingStart)
			{
				float smoothingRatio = 1f - ((deltaProgress - MinimumDistance) / (MinimumDistanceSmoothingStart - MinimumDistance));
				targetMoveSpeed += Player.MoveSpeed * smoothingRatio;
				speedSmoothing = Mathf.Lerp(speedSmoothing, 0, smoothingRatio);
			}
		}

		moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, targetMoveSpeed, ref moveSpeedVelocity, speedSmoothing * PhysicsManager.physicsDelta);
		Progress += moveSpeed * PhysicsManager.physicsDelta;
	}

	private float CalculateChargingMoveSpeed(float deltaProgress)
	{
		if (Player.IsKnockback)
			return 0;

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
		// Enable the correct horns
		horns[0].CallDeferred(CaptainBemothHorn.MethodName.EnableLockon);

		if (currentHealth < MaxHealth)
		{
			horns[1].CallDeferred(CaptainBemothHorn.MethodName.EnableLockon);
			horns[2].CallDeferred(CaptainBemothHorn.MethodName.EnableLockon);
		}

		if (currentHealth == 1)
			horns[3].CallDeferred(CaptainBemothHorn.MethodName.EnableLockon);
	}

	private void DisableHornHurtboxes()
	{
		foreach (CaptainBemothHorn horn in horns)
			horn.CallDeferred(CaptainBemothHorn.MethodName.DisableLockon);
	}

	private readonly string HornDamageTrigger = "parameters/horn_damage_trigger/request";
	private void TakeHornDamage() => animator.Set(HornDamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

	private readonly string DamageTrigger = "parameters/damage_trigger/request";
	private void TakeDamage()
	{
		hintDialogIndex = hintDialogs.Length; // Disable hint dialogs. The player figured it out

		isAttackQueued = true;
		currentHealth--;
		currentState = BemothState.Damaged;

		if (isAttackDisabled || !isAttackQueued) // Allow immediate followup when in no-attack zone
			EnableHornHurtboxes();
		else
			DisableHornHurtboxes();

		CancelShockAttack();

		if (currentHealth == 0)
			DefeatBoss();
		else
			animator.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	#region Attacks
	/// <summary> Is an attack being queued directly after taking damage? </summary>
	private bool isAttackQueued;
	private bool isAttackDisabled;
	private bool isAttackActive;
	private float attackTimer;
	private readonly float ShortAttackInterval = 0.8f;
	private readonly float AttackTimerInterval = 3f;

	private void StartAttack()
	{
		if (hintDialogIndex < hintDialogs.Length && !SoundManager.instance.IsDialogActive)
		{
			if (hintDialogIndex >= 0) // Give some room from the start of the fight and the first hint
				hintDialogs[hintDialogIndex].Activate();

			hintDialogIndex++;
			hintDialogIndex = Mathf.Min(hintDialogIndex, hintDialogs.Length - 1);
		}

		if (isAttackQueued) // Queued attacks are always waves
		{
			isAttackQueued = false;
			EnterWaveAttackState();
			return;
		}

		if (currentHealth == MaxHealth || GetDeltaProgress() >= BombAttackRange)
		{
			EnterBombAttackState();
			return;
		}

		if (currentHealth > 1)
		{
			if (Runtime.randomNumberGenerator.Randf() > 0.8f)
				EnterBombAttackState();
			else
				EnterWaveAttackState();

			return;
		}

		if (Runtime.randomNumberGenerator.Randf() > 0.7f)
			EnterWaveAttackState();
		else
			EnterChargeAttackState();
	}

	private void FinishAttack() => isAttackActive = false;

	private int bombAttackCounter;
	private readonly float BombAttackRange = 15f;
	private void EnterBombAttackState()
	{
		bombAttackCounter = 0;
		currentState = BemothState.BombAttack;
		EnableHornHurtboxes();
	}

	private void ProcessBombState()
	{
		if (isAttackActive)
			return;

		if (bombAttackCounter > bombs.Length)
			return;

		StartBombAttack();
	}

	private readonly string BombTrigger = "parameters/bomb_trigger/request";
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

	private void CancelBombAttacks()
	{
		foreach (BossBombAttack bomb in bombs)
		{
			if (bomb.IsActive) // Already flying
				continue;

			bomb.CancelLaunch();
		}
	}

	private int waveAttackCounter;
	/// <summary> Direction of the wave attack. </summary>
	private readonly string WaveTransition = "parameters/wave_transition/transition_request";
	/// <summary> Activation of the wave attack. </summary>
	private readonly string WaveTrigger = "parameters/wave_trigger/request";
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

		shockTimer = 0;
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
				StopChargeAttack();

			return;
		}

		if (deltaProgress >= StopDistance)
		{
			HOffset = ExtensionMethods.SmoothDamp(HOffset, -Player.PathFollower.LocalPlayerPositionDelta.X, ref trackingVelocity, ChargeTrackingSmoothing * PhysicsManager.physicsDelta);
			// Wait until we're fully closed before charging
			if (IsClosed) // Start charge
			{
				isAttackActive = true;
				eventAnimator.Play("charge");
			}

			return;
		}

		if (deltaProgress > MinimumDistance)
			EnterIdleState();
	}

	private void StopChargeAttack()
	{
		if (currentState != BemothState.ChargeAttack)
			return;

		isAttackActive = false;
		eventAnimator.Play("RESET");
		EnableHornHurtboxes();
	}

	private bool hasPlayerJumpedOffHorn;
	private bool isShockAttackActive;
	private float shockTimer;
	private readonly float ShockAttackLongDelay = 4f;
	private readonly float ShockAttackShortDelay = .5f;
	private readonly float ShockAttackChargeLength = 5f;
	private readonly string ShockTrigger = "parameters/shock_trigger/request";
	private void EnterShockAttackState()
	{
		CancelBombAttacks();

		attackTimer = 0;
		isAttackActive = false;
		isAttackQueued = false;
		isShockAttackActive = false;
		hasPlayerJumpedOffHorn = false;
		currentState = BemothState.ShockAttack;

		// Delay shock to give time to one-cycle horns
		shockTimer = currentHealth == MaxHealth || currentHealth == 1 ? -ShockAttackLongDelay : -ShockAttackShortDelay;
		shockAttackRoot.Scale = Vector3.One * 0.001f;
	}

	private void ProcessShockAttack()
	{
		if (isShockAttackActive)
			return;

		shockTimer = Mathf.MoveToward(shockTimer, ShockAttackChargeLength, PhysicsManager.physicsDelta);
		if (Mathf.Abs(shockTimer) <= PhysicsManager.physicsDelta) // Play shockFX
			eventAnimator.Play("shock");

		float fxScale = Mathf.Clamp(shockTimer / ShockAttackChargeLength, 0f, 1f);
		shockAttackRoot.Scale = (Vector3.One * 0.001f).Lerp(Vector3.One, fxScale);

		if (Mathf.IsEqualApprox(shockTimer, ShockAttackChargeLength))
		{
			isShockAttackActive = true;
			eventAnimator.Play("RESET"); // Momentary pause before the burst
			animator.Set(ShockTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			Player.SetHornPullable(false);
			isAttackQueued = true;
		}
	}

	private void CancelShockAttack()
	{
		shockTimer = 0;
		isShockAttackActive = false;
		eventAnimator.Play("RESET");
	}

	private void ActivateShockScreenShake()
	{
		ShakeScreen();

		if (hasPlayerJumpedOffHorn) // Player has already left the horn
			return;

		jumpCameraTrigger.GlobalPosition = Player.Camera.Camera.GlobalPosition; // Sync jump camera's position
		jumpCameraTrigger.Activate();
	}

	private void ShakeScreen()
	{
		Player.Camera.StartCameraShake(new()
		{
			magnitude = Vector3.One.RemoveDepth() * 5f,
			intensity = Vector3.One * 100.0f,
			duration = 1f,
			origin = root.GlobalPosition,
			maximumDistance = StopDistance,
		});
	}

	/// <summary> Locks the player in for damage. </summary>
	private void LockPlayerJump() => Player.SetHornJumpable(false);

	private void DamagePlayer()
	{
		if (hasPlayerJumpedOffHorn) // Player must have jumped off already
			return;

		if (Player.IsHomingAttacking) // Prevent unfair damage
			return;

		// Shock attack
		Player.StartKnockback(new()
		{
			ignoreMovementState = true,
			overrideKnockbackHeight = true,
			knockbackHeight = LaunchFallHeight,
		});
	}

	private void FinishPlayerDamage()
	{
		if (!hasPlayerJumpedOffHorn)
			LaunchPlayer(false);

		EnterIdleState();
	}

	private bool isRequestingMotionBlur;
	private void StartMotionBlur()
	{
		isRequestingMotionBlur = true;
		Player.Camera.RequestMotionBlur();
	}

	private void StopMotionBlur()
	{
		if (!isRequestingMotionBlur)
			return;

		Player.Camera.UnrequestMotionBlur();
		isRequestingMotionBlur = false;
	}
	#endregion

	private readonly float LaunchFallHeight = 8f;
	private readonly float LaunchProgressSearchInterval = 5f;
	private readonly float BaseLaunchOffset = 10f;
	private readonly float LaunchHeightCheckLength = 20f;

	private void LaunchPlayer(bool isJump)
	{
		float initialProgress = Player.PathFollower.Progress;
		Player.PathFollower.Progress -= BaseLaunchOffset;
		RaycastHit hit = CheckLaunchGround();

		while (!hit)
		{
			Player.PathFollower.Progress -= LaunchProgressSearchInterval;
			hit = CheckLaunchGround();
		}

		Player.PathFollower.Progress = initialProgress;
		jumpTrigger.GlobalPosition = hit.point;
		if (isJump)
		{
			jumpTrigger.Position += Vector3.Up * LaunchFallHeight;
			jumpCameraTrigger.GlobalPosition = Player.Camera.Camera.GlobalPosition; // Sync jump camera's position
			jumpCameraTrigger.Activate();
			jumpTrigger.JumpFinished += FinishJump;
			hasPlayerJumpedOffHorn = true;
		}
		else
		{
			// Teleport to the proper location
			Player.GlobalPosition = jumpTrigger.GlobalPosition + Vector3.Up * LaunchFallHeight;
			Player.PathFollower.Resync();
			mainCameraTrigger.Activate();

			// Play dialog
			if (!hasPlayerJumpedOffHorn && currentState == BemothState.Damaged)
			{
				damageDialogs[damageDialogIndex].Activate();
				damageDialogIndex++;
				damageDialogIndex = Mathf.Min(damageDialogIndex, damageDialogs.Length - 1);
			}
		}

		jumpTrigger.Activate();
		Player.Animator.SnapRotation(Player.PathFollower.ForwardAngle);

		if (isJump)
			Player.Animator.StartSpin(3f);

		// Only change states when shock wasn't charged
		if (!isShockAttackActive)
		{
			CancelShockAttack();
			EnterIdleState();
		}

		attackTimer = ShortAttackInterval;
		DisablePoppedHorns();
	}

	private RaycastHit CheckLaunchGround() => this.CastRay(Player.PathFollower.GlobalPosition + Vector3.Up * LaunchHeightCheckLength * 0.5f,
		Vector3.Down * LaunchHeightCheckLength,
		Runtime.Instance.environmentMask);

	private void FinishJump()
	{
		jumpTrigger.JumpFinished -= FinishJump;
		CallDeferred(MethodName.LaunchPlayer, false); // Transition back to gameplay
	}

	private void DisablePoppedHorns()
	{
		foreach (CaptainBemothHorn horn in horns)
		{
			if (horn.IsPopping)
				horn.Despawn();
		}
	}

	private void EnableAttacks(Area3D a)
	{
		if (!a.IsInGroup("no attack zone"))
			return;

		isAttackDisabled = false;
		isAttackQueued = true;
	}

	private void DisableAttacks(Area3D a)
	{
		if (!a.IsInGroup("no attack zone"))
			return;

		if (currentState != BemothState.ShockAttack)
		{
			EnterIdleState();
			eventAnimator.Play("RESET");
		}

		EnableHornHurtboxes();
		isAttackDisabled = true;
	}
}