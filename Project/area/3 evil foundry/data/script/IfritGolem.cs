using Godot;
using System.Collections.Generic;
using Project.Core;
using Project.Gameplay.Objects;
using Project.Gameplay.Triggers;

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

	[Export] private CameraSettingsResource bounceCameraSettings;

	[Export] private BoneAttachment3D[] boneAttachments;
	[Export] private Core[] cores;
	[Export] private Node3D[] burnPositions;

	[Export] private DialogTrigger[] hitDialogs = [];
	[Export] private DialogTrigger[] hintDialogs = [];
	private int[] dialogFlags = [
		0, // Health flag
		0, // Head flag
		0, // Laser flag
		0 // Water flag
	];

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
	private bool IsFocusingOnPlayer => Mathf.Abs(currentSector - playerSector) <= 1 || Mathf.Abs(currentSector - playerSector) >= MaxSectorCount - 1;
	/// <summary> Calculates the target sector for the Golem to move towards. </summary>
	private int CalculateTargetSector()
	{
		// Player is right in front of the golem--don't move
		if (IsFocusingOnPlayer)
			return currentSector;

		// Get the sector opposite to the player
		int targetSector = WrapClampSector(playerSector + 3);

		if (targetSector == 0 && playerSector < currentSector)
			targetSector += MaxSectorCount;
		if (targetSector > playerSector && playerSector > currentSector)
			targetSector -= MaxSectorCount;

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
		EventAnimator = GetNode<AnimationPlayer>(eventAnimator);

		HeadHurtbox = GetNode<Area3D>(headHurtbox);
		DamagePath = GetNode<Node3D>(damagePath);
		PlayerLaunchTarget = GetNode<Node3D>(playerLaunchTarget);

		LeftEye = GetNode<Node3D>(leftEye);
		RightEye = GetNode<Node3D>(rightEye);
		LaserRoot = GetNode<Node3D>(laserRoot);
		LaserBeam = GetNode<Node3D>(laserBeam);
		LaserVFXRoot = GetNode<Node3D>(laserVFXRoot);

		PoolGasTanks();
		InitializeSpecialAttack();
		foreach (Core core in cores)
		{
			core.CoreDestroyed += OnCoreDestroyed;
		}

		Player.LaunchFinished += FinishLaunch;
		StageSettings.Instance.Respawned += Respawn;
		StageSettings.Instance.LevelStarted += StartIntroduction;
	}

	public override void _ExitTree()
	{
		// Free everything in the pool (The dictionary and list don't need to be freed because all the nodes exist in the scene tree)
		while (gasTankPool.Count != 0)
			gasTankPool.Dequeue().QueueFree();
	}

	private void Respawn()
	{
		currentSector = previousSector = 0;
		Root.Rotation = Vector3.Zero;

		headRotationRatio = targetHeadRotationRatio = 0;

		currentHealth = MaxHealth;
		RespawnCores();
		RespawnGasTanks();
		StopLaserAttack();

		// Reset Animations
		NormalStatePlayback.Start(IdleAnimation);
		AnimationTree.Set(HitstunTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(SpecialAttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		if (currentState == GolemState.Stunned)
			EmitSignal(SignalName.StunEnded);

		specialAttackIntervalCounter = 0;
		EnterIdle();
	}

	public override void _PhysicsProcess(double _)
	{
		switch (currentState)
		{
			case GolemState.Introduction:
				if (Input.IsActionJustPressed("button_pause"))
					FinishIntroduction();
				break;
			case GolemState.Defeated:
				if (Input.IsActionJustPressed("button_pause"))
					AnimationTree.Set(DefeatSeek, 20);
				break;
			case GolemState.Idle:
				ProcessIdle();
				break;
			case GolemState.Stunned:
				ProcessStun();
				break;
			case GolemState.SpecialAttack:
				ProcessSpecialAttack();
				break;
		}

		UpdateHeadRotation();
		ProcessShutters();
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

		// Force-update bone attachments so objects don't go out of sync
		foreach (BoneAttachment3D bone in boneAttachments)
			bone.OnBonePoseUpdate(bone.BoneIdx);
	}

	private readonly StringName IntroTrigger = "parameters/intro_trigger/request";
	private void StartIntroduction()
	{
		// Disable the player for the intro animation
		Player.ProcessMode = ProcessModeEnum.Disabled;
		Interface.PauseMenu.AllowPausing = false;
		HeadsUpDisplay.Instance.Visible = false;
		AnimationTree.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
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
	}

	private void StartBattle()
	{
		TransitionManager.instance.TransitionProcess -= StartBattle;
		EventAnimator.Play("finish-intro");
		EventAnimator.Advance(0.0);
		AnimationTree.Set(IntroTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		Respawn();
		TransitionManager.FinishTransition();
		Player.ProcessMode = ProcessModeEnum.Inherit;
		Interface.PauseMenu.AllowPausing = true;
		HeadsUpDisplay.Instance.Visible = true;
		Player.Camera.Camera.Current = true;
	}

	private void EnterIdle()
	{
		if (AttemptSpecialAttack())
			return;

		ShowCores();
		laserAttackTimer = 0; // Laser attack is queued instantly when transitioning to idle
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

		if (SoundManager.instance.IsDialogActive)
			return;

		if (dialogFlags[1] < 3 && dialogFlags[3] != 1)
		{
			hintDialogs[dialogFlags[1]].Activate();
			dialogFlags[1]++;
		}
	}

	[Signal] public delegate void StunnedEventHandler();
	[Signal] public delegate void StunEndedEventHandler();
	[Signal] public delegate void HitstunLaunchedEventHandler();
	[Signal] public delegate void HitstunLaunchEndedEventHandler();
	[Signal] public delegate void LavaLaunchedEventHandler();
	[Signal] public delegate void LavaLaunchEndedEventHandler();

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

		// Play water hint dialog
		if (dialogFlags[3] == 0)
		{
			dialogFlags[3] = 1;
			hintDialogs[4].Activate();
			return;
		}

		// Play Shahra's voice clip
		if (dialogFlags[0] != 5 && IsSecondPhaseActive)
		{
			dialogFlags[0] = 5;
			hitDialogs[4].Activate();
		}
	}

	private void ExitHitstun()
	{
		isInteractingWithPlayer = false;
		bounceCameraSettings.yawAngle = (currentSector * SectorRotationIncrementRad) + Mathf.Pi;
		RespawnCores();

		// Launch the player back to solid ground
		Player.Animator.SnapRotation(bounceCameraSettings.yawAngle - (Mathf.Pi * 0.5f));
		Player.StartLauncher(LaunchSettings.Create(Player.GlobalPosition, PlayerLaunchTarget.GlobalPosition, 5f));
		Player.Camera.LockonTarget = null;
		Player.Animator.StartSpin(3f);
		EmitSignal(SignalName.HitstunLaunched);
		EmitSignal(SignalName.StunEnded);
	}

	private void FinishLaunch()
	{
		if (currentState != GolemState.Damaged && currentState != GolemState.Recovery)
		{
			EmitSignal(SignalName.LavaLaunchEnded);
			return;
		}

		// Kill residue player speed
		Player.MoveSpeed = 0.0f;
		Player.MovementAngle = Player.PathFollower.ForwardAngle;

		// TODO Play hit dialog
		if (currentHealth < MaxHealth && dialogFlags[0] == 1)
		{
			hitDialogs[1].Activate();
			dialogFlags[0]++;
		}
		else if (currentHealth <= MaxHealth - 3 && dialogFlags[0] == 2)
		{
			hitDialogs[2].Activate();
			dialogFlags[0]++;
		}
		else if (IsSecondPhaseActive && dialogFlags[0] < 4)
		{
			hitDialogs[3].Activate();
			dialogFlags[0] = 4;
		}

		EmitSignal(SignalName.HitstunLaunchEnded);
	}

	private void ProcessStun()
	{
		if (Player.IsOnGround && headHealth != MaxHeadHealth)
		{
			EnterRecovery();
			return;
		}

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

		Player.Camera.LockonTarget = null;
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

		Player.Camera.LockonTarget = HeadHurtbox;

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
			case PlayerController.AttackStates.None:
				EnterRecovery();
				return;
		}

		Player.StartBounce(true);
		SetInteractionProcessed();
	}

	private bool IsSecondPhaseActive => currentHealth <= SecondPhaseRequirement;
	private int currentHealth;
	private int headHealth;
	private readonly int MaxHealth = 9;
	/// <summary> Maximum number of times the player hurt the golem in one cycle (excluding a powerful final hit). </summary>
	private readonly int MaxHeadHealth = 3;
	private readonly int SecondPhaseRequirement = 3;
	private void UpdateHeadDamage(int amount)
	{
		headHealth -= amount;
		currentHealth -= amount;
		EventAnimator.Seek(0.0, true);
		EventAnimator.Play("damage");

		if (dialogFlags[0] == 0)
		{
			dialogFlags[0]++;
			hitDialogs[0].Activate();
		}

		if (headHealth > 0 && currentHealth > 0)
			return;

		if (currentHealth <= 0)
			CallDeferred(MethodName.DefeatBoss);
		else
			CallDeferred(MethodName.EnterDamage);
	}

	private readonly StringName DefeatTrigger = "parameters/defeat_trigger/request";
	private void DefeatBoss()
	{
		TransitionManager.StartTransition(new()
		{
			inSpeed = 0f,
			outSpeed = .5f,
			color = Colors.Black
		});
		TransitionManager.FinishTransition();

		Root.Rotation = Vector3.Zero;
		ExitHitstun();

		if (Player.Skills.IsUsingBreakSkills)
			Player.Skills.CancelBreakSkills();
		Player.Visible = false;
		Player.ProcessMode = ProcessModeEnum.Disabled;
		Player.AddLockoutData(Runtime.Instance.DefaultCompletionLockout);

		AnimationTree.Set(DefeatTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		// Award 8000 points for defeating the boss
		BonusManager.instance.QueueBonus(new(BonusType.Boss, 8000));
		Interface.PauseMenu.AllowPausing = false;
		HeadsUpDisplay.Instance.Visible = false;
		currentState = GolemState.Defeated;
	}

	private readonly StringName DefeatSeek = "parameters/defeat_seek/seek_request";
	private void FinishDefeat()
	{
		EventAnimator.Play("finish-defeat");
		EventAnimator.Advance(0.0);
		AnimationTree.Active = false;
		Player.Visible = true;
		Player.ProcessMode = ProcessModeEnum.Inherit;
		Player.Camera.Camera.Current = true;
		StageSettings.Instance.FinishLevel(true);
	}
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
	private readonly float LaserForwardLeadMultiplier = 60.0f;
	private readonly float LaserBackwardLeadMultiplier = 40.0f;
	private readonly float LaserAttackInterval = 0.5f;
	private void UpdateHeadRotation()
	{
		UpdateHeadTargetRotation();

		headRotationRatio = ExtensionMethods.SmoothDamp(headRotationRatio, targetHeadRotationRatio, ref headRotationVelocity, HeadSmoothing);
		AnimationTree.Set(HeadBlend, headRotationRatio);
	}

	private void UpdateHeadTargetRotation()
	{
		if (currentState != GolemState.Idle && currentState != GolemState.Step && currentState != GolemState.SpecialAttack)
		{
			targetHeadRotationRatio = 0;
			return;
		}

		float delta = ExtensionMethods.SignedDeltaAngleRad(Player.GlobalPosition.Flatten().AngleTo(Vector2.Down), Root.Rotation.Y);
		if (Mathf.Abs(delta) > MaxHeadRotation * 2f)
		{
			if (currentState != GolemState.SpecialAttack)
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

		laserAttackTimer = 0;
		StartLaserAttack();
	}

	private void StartLaserAttack(int eyeIndex = 0)
	{
		// Decide which eye the laser spawns from based on player's position (or alternating eye attack)
		if (eyeIndex == 0)
			isLaserFromRightEye = Player.GlobalPosition.Rotated(Vector3.Up, -Root.Rotation.Y).X < 0;
		else
			isLaserFromRightEye = eyeIndex > 0;

		float progress = Player.PathFollower.Progress;
		if (Player.IsMovingBackward)
			Player.PathFollower.Progress -= Player.MoveSpeed * LaserBackwardLeadMultiplier * PhysicsManager.physicsDelta;
		else
			Player.PathFollower.Progress += Player.MoveSpeed * LaserForwardLeadMultiplier * PhysicsManager.physicsDelta;
		Vector3 samplePosition = Player.PathFollower.GlobalPosition;
		Player.PathFollower.Progress = progress;

		Vector3 targetPosition = isLaserFromRightEye ? RightEye.GlobalPosition : LeftEye.GlobalPosition;
		float targetRotation = (samplePosition - targetPosition).Flatten().AngleTo(Vector2.Down);
		float activeRotation = (Player.PathFollower.GlobalPosition - targetPosition).Flatten().AngleTo(Vector2.Down);

		// Player is out of range
		if (ExtensionMethods.DeltaAngleRad(activeRotation, Root.Rotation.Y) > Mathf.Pi * .4f ||
			ExtensionMethods.DeltaAngleRad(targetRotation, Root.Rotation.Y) > Mathf.Pi * .4f)
		{
			if (currentState == GolemState.SpecialAttack) // Cancel special attack early
				FinishSpecialAttack();

			return;
		}

		LaserRoot.Rotation = Vector3.Up * targetRotation;
		LaserRoot.GlobalPosition = targetPosition;

		// Update VFX positions
		LaserVFXRoot.GlobalPosition = samplePosition + (Vector3.Up * 10.0f);
		LaserVFXRoot.Rotation = Vector3.Up * LaserVFXRoot.GlobalPosition.Flatten().AngleTo(Vector2.Down);
		RaycastHit hit = LaserVFXRoot.CastRay(LaserVFXRoot.GlobalPosition, Vector3.Down * 50.0f, Runtime.Instance.environmentMask);
		if (hit)
			LaserVFXRoot.GlobalPosition = new(LaserVFXRoot.GlobalPosition.X, hit.point.Y, LaserVFXRoot.GlobalPosition.Z);

		EventAnimator.Play(currentState == GolemState.SpecialAttack ? "laser-far" : "laser-close");
		isLaserAttackActive = true;

		// Play laser hint dialog
		if (dialogFlags[2] != 0)
			return;

		dialogFlags[2] = 1;
		hintDialogs[3].Activate();
	}

	private void ProcessLaserAttack()
	{
		LaserRoot.GlobalPosition = isLaserFromRightEye ? RightEye.GlobalPosition : LeftEye.GlobalPosition;
	}

	private void StopLaserAttack()
	{
		isLaserAttackActive = false;
		laserAttackTimer = LaserAttackInterval;
		if (IsSecondPhaseActive)
			laserAttackTimer *= .5f;
		EventAnimator.Play("RESET");
	}

	private float rightShutterTimer;
	private float leftShutterTimer;
	private readonly float LeftShutterInterval = 1.5f;
	private readonly float RightShutterInterval = 0.5f;
	private readonly int MaxShutterIndex = 6;
	private void ProcessShutters()
	{
		if (currentState != GolemState.Idle && currentState != GolemState.Step)
			return;

		rightShutterTimer = Mathf.MoveToward(rightShutterTimer, RightShutterInterval, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(rightShutterTimer, RightShutterInterval))
		{
			rightShutterTimer = 0;
			for (int i = 0; i < 2; i++)
				StartGasTankAttack(true, Runtime.randomNumberGenerator.RandiRange(1, MaxShutterIndex));
		}

		leftShutterTimer = Mathf.MoveToward(leftShutterTimer, LeftShutterInterval, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(leftShutterTimer, LeftShutterInterval))
		{
			leftShutterTimer = 0;
			// Left shutter releases 2 gas tanks at a time so it's easier for the player to hit the cores
			for (int i = 0; i < 2; i++)
				StartGasTankAttack(false, Runtime.randomNumberGenerator.RandiRange(1, MaxShutterIndex));
		}
	}

	private void RespawnGasTanks()
	{
		// Abort shutter animations
		for (int i = 1; i <= MaxShutterIndex; i++)
		{
			AnimationTree.Set(GetShutterTrigger(true, i) + "/request", (int)AnimationNodeOneShot.OneShotRequest.Abort);
			AnimationTree.Set(GetShutterTrigger(false, i) + "/request", (int)AnimationNodeOneShot.OneShotRequest.Abort);
		}

		// Repool active gas tanks
		for (int i = activeGasTanks.Count - 1; i >= 0; i--)
			PoolTank(activeGasTanks[i]);

		int[] keys = [.. queuedGasTanks.Keys];
		for (int i = keys.Length - 1; i >= 0; i--)
		{
			PoolTank(queuedGasTanks[keys[i]]);
			queuedGasTanks.Remove(keys[i]);
		}
	}

	private void StartGasTankAttack(bool isRightHand, int tankIndex)
	{
		StringName trigger = GetShutterTrigger(isRightHand, tankIndex);
		if ((bool)AnimationTree.Get(trigger + "/active")) // Shutter is already busy
			return;

		AnimationTree.Set(trigger + "/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	[ExportGroup("Attacks")]
	[Export] private PackedScene gasTankScene;
	[Export] private Node3D[] gasTankSpawnPositions;
	private readonly Queue<GasTank> gasTankPool = [];
	private readonly List<GasTank> activeGasTanks = [];
	private readonly Dictionary<int, GasTank> queuedGasTanks = [];
	private void PoolGasTanks()
	{
		for (int i = 0; i < 2; i++)
			gasTankPool.Enqueue(GenerateGasTank());
	}

	private GasTank GenerateGasTank()
	{
		GasTank tank = gasTankScene.Instantiate<GasTank>();
		tank.disableRespawning = true;
		tank.Detonated += () => PoolTank(tank);
		return tank;
	}

	private void SpawnGasTank(int index)
	{
		if (!gasTankPool.TryDequeue(out GasTank tank))
			tank = GenerateGasTank();

		if (tank.IsInsideTree())
			tank.GetParent().RemoveChild(tank);

		gasTankSpawnPositions[index].AddChild(tank);
		tank.Call(GasTank.MethodName.Respawn);
		tank.Transform = Transform3D.Identity;

		queuedGasTanks.Add(index, tank);
	}

	private void LaunchGasTank(int index)
	{
		// There isn't any gas tank queued at the given shutter!
		if (!queuedGasTanks.TryGetValue(index, out GasTank tank))
			return;

		Transform3D t = tank.GlobalTransform;
		tank.GetParent().RemoveChild(tank);
		AddChild(tank);
		tank.GlobalTransform = t;

		if (currentState == GolemState.SpecialAttack)
		{
			// Alternative targeting during special attack
			float rotation = Mathf.Pi * .15f * Mathf.Lerp(-1f, 1f, Runtime.randomNumberGenerator.Randf());
			float distance = Mathf.Lerp(-5f, 5f, Runtime.randomNumberGenerator.Randf());
			tank.height = Mathf.Lerp(5f, 10f, Runtime.randomNumberGenerator.Randf());
			tank.endPosition = Player.PathFollower.GlobalPosition.Rotated(Vector3.Up, rotation);
			tank.endPosition += tank.endPosition.Normalized() * distance;
		}
		else
		{
			// Drop nearby
			float distance = Mathf.Lerp(5f, 10f, Runtime.randomNumberGenerator.Randf());
			float offset = Mathf.Lerp(-3f, 3f, Runtime.randomNumberGenerator.Randf());
			tank.height = 1f;
			tank.endPosition = tank.GlobalPosition + (tank.Up() * distance) + (tank.Forward() * offset) + (Vector3.Down * tank.GlobalPosition.Y);
		}
		tank.globalEndPosition = true;
		tank.CallDeferred(GasTank.MethodName.Launch);

		queuedGasTanks.Remove(index);
		activeGasTanks.Add(tank);
	}

	private void PoolTank(GasTank tank)
	{
		if (tank.GetParent() == null) // Already dequeued
			return;

		gasTankPool.Enqueue(tank);
		activeGasTanks.Remove(tank);
		tank.GetParent().RemoveChild(tank);
	}

	private readonly StringName ShutterTreeParameter = "parameters/shutter_tree/";
	private StringName GetShutterTrigger(bool isRightHand, int tankIndex)
	{
		int initialDigit = tankIndex;
		if (tankIndex > 3)
			initialDigit = 11 + (tankIndex % 3);
		string triggerName = initialDigit.ToString("00");
		triggerName += isRightHand ? "_r_trigger" : "_l_trigger";
		return ShutterTreeParameter + triggerName;
	}

	private bool isLaserSpecialAttack;
	private float specialAttackTimer;
	private int specialAttackCount;
	private int targetSpecialAttackCount;
	private int specialAttackIntervalCounter;
	private int[] specialAttackLeftShutterOrder;
	private int[] specialAttackRightShutterOrder;
	private readonly int SpecialAttackInterval = 2;
	private void InitializeSpecialAttack()
	{
		specialAttackLeftShutterOrder = specialAttackRightShutterOrder = [1, 2, 3, 4, 5, 6];
	}

	private bool AttemptSpecialAttack()
	{
		if (!IsSecondPhaseActive)
			return false;

		specialAttackIntervalCounter--;
		if (specialAttackIntervalCounter > 0)
			return false;

		specialAttackRightShutterOrder = RandomizeArray(specialAttackLeftShutterOrder);
		specialAttackRightShutterOrder = RandomizeArray(specialAttackRightShutterOrder);

		StartSpecialAttack();
		return true;
	}

	private int[] RandomizeArray(int[] array)
	{
		System.Random rng = new();
		int n = array.Length;
		while (n > 1)
		{
			int k = rng.Next(n--);
			(array[k], array[n]) = (array[n], array[k]);
		}
		return array;
	}

	private AnimationNodeStateMachinePlayback SpecialStatePlayback => AnimationTree.Get(SpecialPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName SpecialPlayback = "parameters/special_state/playback";
	private readonly StringName SpecialAttackTrigger = "parameters/special_trigger/request";
	private readonly StringName SpecialAttackStartAnimation = "special-attack-start";
	private readonly StringName SpecialAttackStopAnimation = "special-attack-stop";
	private void StartSpecialAttack()
	{
		SpecialStatePlayback.Start(SpecialAttackStartAnimation);
		AnimationTree.Set(SpecialAttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		// Randomly choose between gas tanks and lasers
		isLaserSpecialAttack = Runtime.randomNumberGenerator.Randf() > .5f;

		if (isLaserSpecialAttack)
			targetSpecialAttackCount = 3 + (SecondPhaseRequirement - currentHealth);
		else
			targetSpecialAttackCount = 10 + (SecondPhaseRequirement - currentHealth);

		UpdatePreviousSector();
		currentSector = playerSector;
		if (currentSector % 2 == 0)
			currentSector++;
		currentSector = WrapClampSector(currentSector);

		specialAttackTimer = specialAttackCount = 0;
		specialAttackIntervalCounter = SpecialAttackInterval;
		currentState = GolemState.SpecialAttack;
	}

	private readonly float LaserSpecialAttackInterval = 1.5f;
	private readonly float TankSpecialAttackInterval = .5f;
	private void ProcessSpecialAttack()
	{
		if (specialAttackCount > targetSpecialAttackCount)
			return;

		float targetTime = isLaserSpecialAttack ? LaserSpecialAttackInterval : TankSpecialAttackInterval;
		specialAttackTimer = Mathf.MoveToward(specialAttackTimer, targetTime, PhysicsManager.physicsDelta);

		if (!Mathf.IsEqualApprox(specialAttackTimer, targetTime))
			return;

		specialAttackCount++;
		specialAttackTimer = 0;
		if (specialAttackCount > targetSpecialAttackCount)
		{
			// Finished special attacks
			FinishSpecialAttack();
			return;
		}

		if (isLaserSpecialAttack)
		{
			// Fire laser attacks, alternating between left and right
			StartLaserAttack(specialAttackCount % 2 == 0 ? -1 : 1);
			return;
		}

		// Gas tanks
		bool isRightHand = specialAttackCount < targetSpecialAttackCount / 2;
		int targetShutterIndex = specialAttackCount % (targetSpecialAttackCount / 2);
		targetShutterIndex = isRightHand ? specialAttackRightShutterOrder[targetShutterIndex] : specialAttackLeftShutterOrder[targetShutterIndex];
		StartGasTankAttack(isRightHand, targetShutterIndex);
	}

	private void FinishSpecialAttack()
	{
		specialAttackCount = targetSpecialAttackCount + 1;
		specialAttackIntervalCounter = SpecialAttackInterval;
		SpecialStatePlayback.Travel(SpecialAttackStopAnimation);
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
		if (Player.IsLaunching)
			return;

		if (Player.IsDefeated || Player.IsTeleporting) // Don't bother launching the player when respawning
			return;

		// Launch the player to the correct burn position
		int burnPositionIndex = 0;
		if (playerSector == 0 || playerSector == 5)
			burnPositionIndex = 0;
		else if (playerSector == 1 || playerSector == 2)
			burnPositionIndex = 1;
		else if (playerSector == 3 || playerSector == 4)
			burnPositionIndex = 2;

		Player.StartLauncher(LaunchSettings.Create(Player.GlobalPosition, burnPositions[burnPositionIndex].GlobalPosition, 5f, true));
		EmitSignal(SignalName.LavaLaunched);
	}
	#endregion
}
