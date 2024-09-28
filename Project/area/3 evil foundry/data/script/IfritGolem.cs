using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses;

public partial class IfritGolem : Node3D
{
	[ExportGroup("Components")]
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath root;
	private Node3D Root { get; set; }
	[Export(PropertyHint.NodeType, "AnimationTree")] private NodePath animationTree;
	private AnimationTree AnimationTree { get; set; }
	[Export(PropertyHint.NodeType, "AnimationPlayer")] private NodePath eventAnimator;
	private AnimationPlayer EventAnimator { get; set; }
	[Export(PropertyHint.NodeType, "Area3D")] private NodePath headHurtbox;
	private Area3D HeadHurtbox { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath damagePath;
	private Node3D DamagePath { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath playerLaunchTarget;
	private Node3D PlayerLaunchTarget { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath rightEye;
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath leftEye;
	private Node3D RightEye { get; set; }
	private Node3D LeftEye { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath laserRoot;
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath laserBeam;
	private Node3D LaserRoot { get; set; }
	private Node3D LaserBeam { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath laserVFXRoot;
	private Node3D LaserVFXRoot { get; set; }

	[Export] private Core[] cores;
	[Export] private Node3D[] burnPositions;
	[ExportGroup("Animated Properties")]
	/// <summary> Used in animations to blend rotations (because exporting stepped keyframes doesn't work...) </summary>
	[Export(PropertyHint.Range, "0,1")] private float rotationBlend;
	[Export] private bool updateRotations;

	private GolemState currentState;
	private enum GolemState
	{
		Introduction,
		Idle,
		Step,
		Stunned,
		Damaged,
		Recovery,
		SpecialAttack,
		Defeated
	}

	private PlayerController Player => StageSettings.Player;

	/// <summary> Sector that the golem is currently facing. </summary>
	private int currentSector;
	/// <summary> Sector that the golem was previously facing (Used for blending rotation angles). </summary>
	private int previousSector;
	private void UpdatePreviousSector() => previousSector = currentSector;
	/// <summary> Sector that the player is currently standing in. </summary>
	private int playerSector = 4;
	/// <summary> Called from stage signals. </summary>
	private void SetPlayerSector(int sector) => playerSector = sector;
	private bool IsFocusingOnPlayer => Mathf.Abs(currentSector - playerSector) <= 1;
	/// <summary> Calculates the target sector for the Golem to move towards. </summary>
	private int CalculateTargetSector()
	{
		// Player is right in front of the golem--don't move
		if (IsFocusingOnPlayer)
			return currentSector;

		// Get the sector opposite to the player
		int targetSector = WrapClampSector(playerSector + 3);

		// Prevent the golem from getting stuck at a single position
		if (targetSector == currentSector)
			targetSector++;
		return targetSector;
	}
	private int WrapClampSector(int sector)
	{
		if (sector >= MaxSectorCount)
			sector -= MaxSectorCount;
		else if (sector < 0)
			sector += MaxSectorCount;
		return sector;
	}
	private readonly int MaxSectorCount = 6;
	private readonly float SectorRotationIncrementRad = Mathf.Pi / 3.0f;

	public override void _Ready()
	{
		Root = GetNode<Node3D>(root);
		AnimationTree = GetNode<AnimationTree>(animationTree);
		AnimationTree.Active = true;
		EventAnimator = GetNode<AnimationPlayer>(eventAnimator);
		HeadHurtbox = GetNode<Area3D>(headHurtbox);
		DamagePath = GetNode<Node3D>(damagePath);
		PlayerLaunchTarget = GetNode<Node3D>(playerLaunchTarget);

		LeftEye = GetNode<Node3D>(leftEye);
		RightEye = GetNode<Node3D>(rightEye);
		LaserRoot = GetNode<Node3D>(laserRoot);
		LaserBeam = GetNode<Node3D>(laserBeam);
		LaserVFXRoot = GetNode<Node3D>(laserVFXRoot);

		foreach (Core core in cores)
		{
			core.CoreDestroyed += OnCoreDestroyed;
		}

		StageSettings.Instance.Respawned += Respawn;
		// TODO Play introduction cutscene
		Respawn();
	}

	private void Respawn()
	{
		currentSector = previousSector = 0;
		currentState = GolemState.Idle;
		Root.Rotation = Vector3.Zero;

		headRotationRatio = targetHeadRotationRatio = 0;

		currentHealth = MaxHealth;
		RespawnCores();

		StopLaserAttack();

		// Reset Animations
		NormalStatePlayback.Start(IdleAnimation);
		AnimationTree.Set(HitstunTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		if (currentState == GolemState.Stunned)
			EmitSignal(SignalName.StunEnded);

		EnterIdle();
	}

	public override void _PhysicsProcess(double _)
	{
		switch (currentState)
		{
			case GolemState.Idle:
				ProcessIdle();
				break;
			case GolemState.Stunned:
				ProcessStun();
				break;
		}

		UpdateHeadRotation();
		if (isLaserAttackActive)
			ProcessLaserAttack();
	}

	public override void _Process(double _)
	{
		if (!updateRotations)
			return;

		// Update visual rotations
		float currentRotation = currentSector * SectorRotationIncrementRad;
		float previousRotation = previousSector * SectorRotationIncrementRad;
		if (currentState == GolemState.Stunned)
			currentRotation = Mathf.Lerp(previousRotation, currentRotation, rotationBlend);
		else
			currentRotation = Mathf.LerpAngle(previousRotation, currentRotation, rotationBlend);

		Root.Rotation = Vector3.Up * (currentRotation % Mathf.Tau);
		DamagePath.Rotation = Root.Rotation;
	}

	private void EnterIdle()
	{
		ShowCores();
		currentState = GolemState.Idle;
	}

	private void ProcessIdle()
	{
		if (isLaserAttackActive) // Wait for laser attack to finish
			return;

		if (IsFocusingOnPlayer)
		{
			AttemptLaserAttack();
			return;
		}

		AttemptStep();
	}

	private void ExitIdle()
	{
		HideCores();
	}

	private float stepTimer;
	private readonly float StepInterval = 1f;
	private void AttemptStep()
	{
		stepTimer = Mathf.MoveToward(stepTimer, StepInterval, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(stepTimer, StepInterval))
			EnterStep();
	}

	private AnimationNodeStateMachinePlayback NormalStatePlayback => AnimationTree.Get(NormalPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName NormalPlayback = "parameters/normal_state/playback";
	private readonly StringName StepLeftAnimation = "step-l";
	private readonly StringName StepRightAnimation = "step-r";
	private readonly StringName IdleAnimation = "idle";
	private void EnterStep()
	{
		ExitIdle();
		UpdatePreviousSector();

		int targetSector = CalculateTargetSector();
		if (targetSector > currentSector)
		{
			// Step left
			currentSector++;
			NormalStatePlayback.Travel(StepLeftAnimation);
		}
		else
		{
			// Step right
			currentSector--;
			NormalStatePlayback.Travel(StepRightAnimation);
		}

		stepTimer = 0f;
		currentState = GolemState.Step;
		currentSector = WrapClampSector(currentSector);
	}
	private void ExitStep()
	{
		if (leftHandCores == 0 && rightHandCores == 0)
			RespawnCores();

		EnterIdle();
	}

	[Signal] public delegate void StunnedEventHandler();
	[Signal] public delegate void StunEndedEventHandler();

	private AnimationNodeStateMachinePlayback HitstunStatePlayback => AnimationTree.Get(HitstunPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName HitstunPlayback = "parameters/hitstun_state/playback";
	private readonly StringName HitstunTrigger = "parameters/hitstun_trigger/request";
	private readonly StringName HitstunLeftAnimation = "hitstun-l";
	private readonly StringName HitstunRightAnimation = "hitstun-r";
	private readonly StringName DamageAnimation = "damage";
	private readonly StringName RecoveryAnimation = "recovery";
	private void EnterHitstun(bool isRightHand)
	{
		// Update sectors
		UpdatePreviousSector();
		HideCores();
		currentSector += currentSector % 2 == 0 ? 0 : 1;
		if (!isRightHand)
			currentSector += 2;

		headHealth = MaxHeadHealth;
		currentState = GolemState.Stunned;
		HitstunStatePlayback.Start(isRightHand ? HitstunRightAnimation : HitstunLeftAnimation);
		AnimationTree.Set(HitstunTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void ExitHitstun()
	{
		isInteractingWithPlayer = false;
		Player.Camera.LockonTarget = null;

		// Launch the player back to solid ground
		Player.StartLauncher(LaunchSettings.Create(Player.GlobalPosition, PlayerLaunchTarget.GlobalPosition, 5f));
		Player.Animator.StartSpin(3f);
		EmitSignal(SignalName.StunEnded);
	}

	private void ProcessStun()
	{
		/*
		if (Player.IsOnGround && headHealth != MaxHeadHealth)
		{
			EnterRecovery();
			return;
		}
		*/

		if (isInteractingWithPlayer)
			UpdateInteraction();
		else if (isInteractionProcessed && Player.AttackState == PlayerController.AttackStates.None)
			ResetInteractionProcessed();
	}

	private void EnterDamage()
	{
		ExitHitstun();

		HitstunStatePlayback.Start(DamageAnimation);
		currentState = GolemState.Damaged;
	}

	private void EnterRecovery()
	{
		ExitHitstun();

		HitstunStatePlayback.Start(RecoveryAnimation);
		currentState = GolemState.Recovery;
	}

	#region Damageable Objects
	private int rightHandCores;
	private int leftHandCores;
	private readonly int CoresPerHand = 3;
	private void ShowCores()
	{
		foreach (Core core in cores)
		{
			if (core.IsDamaged)
				continue;

			core.ShowCore();
		}
	}

	private void HideCores()
	{
		foreach (Core core in cores)
		{
			if (core.IsDamaged)
				continue;

			core.HideCore();
		}
	}

	private void RespawnCores()
	{
		rightHandCores = leftHandCores = CoresPerHand;
		foreach (Core core in cores)
		{
			core.Respawn();
		}
	}

	private void OnCoreDestroyed(bool isRightHand)
	{
		if (isRightHand)
		{
			// Process right hand
			rightHandCores--;
			if (rightHandCores != 0)
				return;

			EnterHitstun(true);
			return;
		}

		// Process left hand
		leftHandCores--;
		if (leftHandCores != 0)
			return;

		EnterHitstun(false);
	}

	private bool isInteractingWithPlayer;
	private bool isInteractionProcessed;
	private void SetInteractionProcessed()
	{
		isInteractionProcessed = true;
		Player.AttackStateChange += ResetInteractionProcessed;
	}

	private void ResetInteractionProcessed()
	{
		isInteractionProcessed = false;
		Player.AttackStateChange -= ResetInteractionProcessed;
	}

	private void UpdateInteraction()
	{
		if (isInteractionProcessed)
			return;

		switch (Player.AttackState)
		{
			case PlayerController.AttackStates.OneShot:
				UpdateHeadDamage(3);
				break;
			case PlayerController.AttackStates.Weak:
				UpdateHeadDamage(1);
				break;
			case PlayerController.AttackStates.Strong:
				UpdateHeadDamage(2);
				break;
		}

		Player.Camera.LockonTarget = HeadHurtbox;
		Player.StartBounce(false);
		SetInteractionProcessed();
	}

	private int headHealth;
	private readonly int MaxHeadHealth = 3;
	private void UpdateHeadDamage(int amount)
	{
		headHealth -= amount;

		if (headHealth > 0)
			return;

		Damage();
	}

	private int currentHealth;
	private readonly int MaxHealth = 3;
	private void Damage()
	{
		currentHealth--;

		if (currentHealth <= 0)
			CallDeferred(MethodName.DefeatBoss);
		else
			CallDeferred(MethodName.EnterDamage);
	}

	private void DefeatBoss()
	{
		GD.Print("Boss Defeated");
		ExitHitstun();

		// TODO Play defeat cutscene
		Player.LaunchFinished += FinishLevel;
		BonusManager.instance.QueueBonus(new(BonusType.Boss, 8000));
		currentState = GolemState.Defeated;
	}

	private void FinishLevel() => StageSettings.Instance.FinishLevel(true);
	#endregion

	#region Attacks
	private bool isLaserAttackActive;
	private bool isLaserFromRightEye;
	private float laserAttackTimer;
	private float headRotationRatio;
	private float targetHeadRotationRatio;
	private float headRotationVelocity;
	private readonly StringName HeadBlend = "parameters/head_blend/blend_amount";
	private readonly float MaxHeadRotation = Mathf.Pi / 4f;
	private readonly float HeadSmoothing = 0.1f;
	private readonly float LaserLeadMultiplier = 40.0f;
	private readonly float LaserAttackInterval = 1f;
	private readonly float LaserAttackLength = .5f;
	private void UpdateHeadRotation()
	{
		UpdateHeadTargetRotation();

		headRotationRatio = ExtensionMethods.SmoothDamp(headRotationRatio, targetHeadRotationRatio, ref headRotationVelocity, HeadSmoothing);
		AnimationTree.Set(HeadBlend, headRotationRatio);
	}

	private void UpdateHeadTargetRotation()
	{
		if (currentState != GolemState.Idle && currentState != GolemState.Step)
		{
			targetHeadRotationRatio = 0;
			return;
		}

		float delta = ExtensionMethods.SignedDeltaAngleRad(Player.GlobalPosition.Flatten().AngleTo(Vector2.Down), Root.Rotation.Y);
		if (Mathf.Abs(delta) > MaxHeadRotation * 2f)
		{
			targetHeadRotationRatio = 0;
			return;
		}

		delta = Mathf.Clamp(delta / MaxHeadRotation, -1f, 1f);
		targetHeadRotationRatio = delta;
	}

	private void AttemptLaserAttack()
	{
		if (isLaserAttackActive)
			return;

		laserAttackTimer -= PhysicsManager.physicsDelta;
		if (laserAttackTimer > 0)
			return;

		StartLaserAttack();
	}

	private void StartLaserAttack()
	{
		laserAttackTimer = 0;

		// TODO Decide which eye the laser spawns from based on player's position (or alternating eye attack)
		isLaserFromRightEye = Player.GlobalPosition.Rotated(Vector3.Up, -Root.Rotation.Y).X < 0;

		float progress = Player.PathFollower.Progress;
		if (Player.IsMovingBackward)
			Player.PathFollower.Progress -= Player.MoveSpeed * LaserLeadMultiplier * PhysicsManager.physicsDelta;
		else
			Player.PathFollower.Progress += Player.MoveSpeed * LaserLeadMultiplier * PhysicsManager.physicsDelta;
		Vector3 samplePosition = Player.PathFollower.GlobalPosition;
		Player.PathFollower.Progress = progress;

		Vector3 targetPosition = isLaserFromRightEye ? RightEye.GlobalPosition : LeftEye.GlobalPosition;
		float targetRotation = (samplePosition - targetPosition).Flatten().AngleTo(Vector2.Down);

		// Player is out of range
		GD.Print(ExtensionMethods.DeltaAngleRad(targetRotation, Root.Rotation.Y));
		if (ExtensionMethods.DeltaAngleRad(targetRotation, Root.Rotation.Y) > Mathf.Pi * .3f)
			return;

		LaserRoot.Rotation = Vector3.Up * targetRotation;
		LaserRoot.GlobalPosition = targetPosition;

		// Update VFX positions
		LaserVFXRoot.GlobalPosition = samplePosition + (Vector3.Up * 10.0f);
		LaserVFXRoot.Rotation = Vector3.Up * LaserVFXRoot.GlobalPosition.Flatten().AngleTo(Vector2.Down);
		RaycastHit hit = LaserVFXRoot.CastRay(LaserVFXRoot.GlobalPosition, Vector3.Down * 50.0f, Runtime.Instance.environmentMask);
		if (hit)
			LaserVFXRoot.GlobalPosition = new(LaserVFXRoot.GlobalPosition.X, hit.point.Y, LaserVFXRoot.GlobalPosition.Z);

		EventAnimator.Play("laser-close");
		isLaserAttackActive = true;
	}

	private void ProcessLaserAttack()
	{
		LaserRoot.GlobalPosition = isLaserFromRightEye ? RightEye.GlobalPosition : LeftEye.GlobalPosition;
	}

	private void StopLaserAttack()
	{
		isLaserAttackActive = false;
		laserAttackTimer = LaserAttackInterval;
	}
	#endregion

	#region Signals
	private void OnHeadEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;

		if (currentState == GolemState.Stunned)
			ProcessStun();
	}

	private void OnHeadExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}

	private void OnLavaDamagedPlayer()
	{
		// Launch the player to the correct burn position
		int burnPositionIndex = 0;
		if (playerSector == 0 || playerSector == 5)
			burnPositionIndex = 0;
		else if (playerSector == 1 || playerSector == 2)
			burnPositionIndex = 1;
		else if (playerSector == 3 || playerSector == 4)
			burnPositionIndex = 2;

		Player.StartLauncher(LaunchSettings.Create(Player.GlobalPosition, burnPositions[burnPositionIndex].GlobalPosition, 5f, true));
	}
	#endregion
}
