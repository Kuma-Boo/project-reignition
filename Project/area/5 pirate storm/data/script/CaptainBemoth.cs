using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses;

public partial class CaptainBemoth : PathFollow3D
{
	[ExportGroup("Components")]
	[Export] private AnimationTree bodyAnimationTree;
	[Export] private AnimationTree hornBackAnimationTree;
	[Export] private AnimationTree hornFrontAnimationTree;
	[Export] private AnimationTree hornLeftAnimationTree;
	[Export] private AnimationTree hornRightAnimationTree;
	[Export] private Node3D root;

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
		bodyAnimationTree.Active = true;
		bossPath = GetParent<Path3D>();

		bombs[^1].Exploded += EnterIdleState; // Return to idle when the last bomb explodes
		StageSettings.Instance.Respawned += Respawn;
		// TODO Play introduction cutscene StageSettings.Instance.LevelStarted += StartIntroduction;
		StartBattle();
	}

	private readonly StringName IntroCutsceneID = "ps_boss_intro";
	private readonly StringName DefeatCutsceneID = "ps_boss_defeat";
	private readonly StringName IntroTrigger = "parameters/intro_trigger/request";
	private void StartIntroduction()
	{
		Player.Deactivate();
		bodyAnimationTree.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

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
		bodyAnimationTree.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		Respawn();

		TransitionManager.FinishTransition();
		Player.Activate();
	}

	private void Respawn()
	{
		EnterIdleState();
		currentHealth = MaxHealth;
		Progress = StopDistance;

		currentRotation = 0;
		rotationVelocity = 0;

		isAttackActive = false;
		bombAttackCounter = 0;
		waveAttackCounter = 0;

		// Reset local position
		root.Position = Vector3.Zero;
		root.Basis = Basis.Identity;

		bodyAnimationTree.Set(BombTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		bodyAnimationTree.Set(WaveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		waveLeft.Deactivate();
		waveRight.Deactivate();
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

	private bool IsOpen => ((StringName)bodyAnimationTree.Get(CloseState)).Equals("enabled");
	private bool IsClosed => ((StringName)bodyAnimationTree.Get(CloseState)).Equals("disabled");
	private readonly StringName CloseState = "parameters/close_transition/current_state";
	private readonly StringName CloseTransition = "parameters/close_transition/transition_request";
	private void Open() => bodyAnimationTree.Set(CloseTransition, "open");
	private void Close() => bodyAnimationTree.Set(CloseTransition, "close");

	private void EnterIdleState()
	{
		isFacingForward = currentHealth == 1; // Only face the player when almost dead
		currentState = BemothState.Idle;
		attackTimer = AttackTimerInterval;
	}

	private void ProcessIdleState()
	{
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
	private readonly float MinimumDistance = 2f;
	private readonly float MinimumDistanceSmoothingStart = 10f;
	private readonly float StopDistance = 30f;
	private readonly float StopDistanceSmoothingStart = 25f;
	private readonly float WaveAttackDistance = 20f;

	/// <summary> Returns the progress difference between the player and the boss. </summary>
	public float GetDeltaProgress()
	{
		float bossProgress = Player.PathFollower.GetProgress(GlobalPosition);
		float deltaProgress = bossProgress - Player.PathFollower.Progress;
		if (deltaProgress < 0)
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

		moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, targetMoveSpeed, ref moveSpeedVelocity, speedSmoothing * PhysicsManager.physicsDelta);
		Progress += moveSpeed * PhysicsManager.physicsDelta;
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

		if (currentHealth >= 1)
		{
			if (Runtime.randomNumberGenerator.Randf() > 0.5f)
				EnterBombAttackState();
			else
				EnterWaveAttackState();

			return;
		}

		// EnterChargeAttackState();
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
		bodyAnimationTree.Set(BombTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void EmitBomb() => bombs[bombAttackCounter - 1].StartWindup();

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
			bodyAnimationTree.Set(WaveTransition, "both");
		else
			bodyAnimationTree.Set(WaveTransition, Runtime.randomNumberGenerator.Randf() > .5f ? "left" : "right");

		bodyAnimationTree.Set(WaveTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
	#endregion
}
