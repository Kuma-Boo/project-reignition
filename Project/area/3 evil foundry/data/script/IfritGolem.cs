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
	[Export(PropertyHint.NodeType, "Area3D")] private NodePath headHurtbox;
	private Area3D HeadHurtbox { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath damagePath;
	private Node3D DamagePath { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")] private NodePath playerLaunchTarget;
	private Node3D PlayerLaunchTarget { get; set; }

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
		HeadHurtbox = GetNode<Area3D>(headHurtbox);
		DamagePath = GetNode<Node3D>(damagePath);
		PlayerLaunchTarget = GetNode<Node3D>(playerLaunchTarget);

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

		currentHealth = MaxHealth;
		RespawnCores();

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
		if (IsFocusingOnPlayer)
			return;

		AttemptStep();
	}

	private void ExitIdle()
	{
		HideCores();
	}

	private float stepTimer;
	private readonly float StepInterval = 0.5f;
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
		Player.StartLauncher(LaunchSettings.Create(Player.GlobalPosition, PlayerLaunchTarget.GlobalPosition, 10f));
		Player.Animator.StartSpin(3f);
		EmitSignal(SignalName.StunEnded);
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

		if (currentHealth > 0)
			EnterDamage();
	}
	#endregion

	#region Signals
	private void OnHeadEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;
	}

	private void OnHeadExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}
	#endregion
}
