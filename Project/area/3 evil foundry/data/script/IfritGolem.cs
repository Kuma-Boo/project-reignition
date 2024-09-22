using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses;

public partial class IfritGolem : Node3D
{
	[Export]
	private Node3D root;
	[Export]
	private AnimationTree animationTree;

	private GolemState currentState;
	private enum GolemState
	{
		Introduction,
		Idle,
		Step,
		Stunned,
		Damaged,
		SpecialAttack,
		Defeated
	}

	/// <summary> Sector that the golem is currently facing. </summary>
	private int currentSector;
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
		StageSettings.Instance.Respawned += Respawn;
		animationTree.Active = true;

		// TODO Play introduction cutscene
		Respawn();
	}

	private void Respawn()
	{
		currentSector = 0;
		currentState = GolemState.Idle;

		// Reset Animations
		NormalStatePlayback.Start(IdleAnimation);

		// Update rotation
		UpdateRotation();
	}

	public override void _PhysicsProcess(double _)
	{
		switch (currentState)
		{
			case GolemState.Idle:
				ProcessIdle();
				break;
		}
	}

	private void ProcessIdle()
	{
		if (IsFocusingOnPlayer)
			return;

		AttemptStep();
	}

	private float stepTimer;
	private readonly float StepInterval = 0.5f;
	private void AttemptStep()
	{
		stepTimer = Mathf.MoveToward(stepTimer, StepInterval, PhysicsManager.physicsDelta);
		if (Mathf.IsEqualApprox(stepTimer, StepInterval))
			EnterStep();
	}

	private AnimationNodeStateMachinePlayback NormalStatePlayback => animationTree.Get(NormalPlayback).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName NormalPlayback = "parameters/normal_state/playback";
	private readonly StringName StepLeftAnimation = "step-l";
	private readonly StringName StepRightAnimation = "step-r";
	private readonly StringName IdleAnimation = "idle";
	private void EnterStep()
	{
		stepTimer = 0f;
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

		currentState = GolemState.Step;
		currentSector = WrapClampSector(currentSector);
	}
	private void ExitStep() => currentState = GolemState.Idle;

	/// <summary> Updates the rotation of the golem's visual model. </summary>
	private void UpdateRotation() => root.Rotation = Vector3.Up * currentSector * SectorRotationIncrementRad;
}
