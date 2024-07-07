using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Handles the logic of the T-Rex found in Dinosaur Jungle Act 1. </summary>
public partial class DinoRex : Node3D
{
	[Export]
	private Node3D root;
	[Export]
	private AnimationTree animationTree;
	private readonly StringName AttackTrigger = "parameters/attack_trigger/request";
	private readonly StringName AttackTransition = "parameters/attack_transition/transition_request";
	private readonly StringName StepTrigger = "parameters/step_trigger/request";
	private readonly StringName StepTransition = "parameters/step_transition/transition_request";
	private readonly StringName LowerBiteState = "lower_bite";
	private readonly StringName TailAttackState = "tail_attack";
	private readonly StringName UpperBiteState = "upper_bite";
	private readonly StringName LeftState = "left";
	private readonly StringName RightState = "left";

	/// <summary> Is the dino currently in the middle of an attack animation? </summary>
	private bool isAttacking;
	private bool isStepAnimationComplete;
	/// <summary> The dino's current rotation. </summary>
	private float currentRotation;
	/// <summary> The rotation the dino is currently targeting. </summary>
	private float targetRotation;
	/// <summary> The rotation to the player's approximate position. </summary>
	private float playerRotation;
	private float rotationVelocity;
	private readonly float RotationSmoothing = 25.0f;

	private RexStates currentState;
	private RexStates targetState;
	private enum RexStates
	{
		Idle, // Dino is not attacking
		Step, // Dino is taking a step
		Lower, // Chomp at the sidle section
		Tail, // Swing the T-Rex's tail
		Upper, // Chomp at the top rocks
	}

	public override void _Ready()
	{
		animationTree.Active = true;
		StageSettings.instance.ConnectRespawnSignal(this);
		Respawn();
	}

	private void Respawn()
	{
		isAttacking = false;
		currentState = targetState = RexStates.Idle;

		currentRotation = targetRotation = playerRotation = rotationVelocity = 0;
		root.RotationDegrees = Vector3.Zero;

		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(StepTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public override void _PhysicsProcess(double _)
	{
		UpdateStep();
		UpdateRotation();
	}

	private void UpdateStep()
	{
		bool isFacingTargetDirection = Mathf.Abs(Mathf.AngleDifference(currentRotation, targetRotation)) < .02f;
		if (!isFacingTargetDirection)
		{
			StartStep();
			return;
		}

		if (isStepAnimationComplete || isFacingTargetDirection)
		{
			// Snap rotation
			rotationVelocity = 0;
			currentRotation = targetRotation;
			if (targetState == RexStates.Idle) // Dino isn't active
				return;

			StartAttack();
		}
	}

	private void UpdateRotation()
	{
		if (!isAttacking)
			currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, RotationSmoothing * PhysicsManager.physicsDelta);

		root.Rotation = Vector3.Up * currentRotation;
	}

	private void StartStep()
	{
		if (currentState == RexStates.Step)
			return;

		isStepAnimationComplete = false;
		currentState = RexStates.Step;
		targetRotation = playerRotation;
		animationTree.Set(StepTransition, currentRotation > targetRotation ? RightState : LeftState);
		animationTree.Set(StepTrigger, (uint)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void FinishStep()
	{
		currentState = targetState;
		isStepAnimationComplete = true;
	}

	public void OnLowerBiteEnter(Area3D _, float attackAngle)
	{
		targetState = RexStates.Lower;
		playerRotation = Mathf.DegToRad(attackAngle);

		if (currentState == RexStates.Idle)
			targetRotation = playerRotation;
	}

	public void OnTailAttackEnter(Area3D _)
	{
		targetState = RexStates.Tail;
		playerRotation = Mathf.DegToRad(-25);

		if (currentState == RexStates.Idle)
			targetRotation = playerRotation;
	}

	public void OnUpperBiteEnter(Area3D _)
	{
		targetState = RexStates.Upper;
		CalculateUpperTargetRotation();

		if (currentState == RexStates.Idle)
			targetRotation = playerRotation;
	}

	public void OnReturnToIdle(Area3D _) => targetState = RexStates.Idle;

	private void StartAttack()
	{
		if (isAttacking)
			return;

		// Play attack animation
		isAttacking = true;
		currentState = targetState;
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		switch (currentState)
		{
			case RexStates.Lower:
				animationTree.Set(AttackTransition, LowerBiteState);
				break;
			case RexStates.Tail:
				animationTree.Set(AttackTransition, TailAttackState);
				break;
			case RexStates.Upper:
				animationTree.Set(AttackTransition, UpperBiteState);
				break;
		}
	}

	private void FinishAttack()
	{
		if (targetState == RexStates.Upper) // Calculate target rotation
			CalculateUpperTargetRotation();

		targetRotation = playerRotation;
		currentState = RexStates.Idle;
		isAttacking = false;
	}

	private void CalculateUpperTargetRotation()
	{
		Vector3 targetPosition = CharacterController.instance.GlobalPosition + (CharacterController.instance.Forward() * 2f);
		playerRotation = (targetPosition - GlobalPosition).Flatten().AngleTo(Vector2.Down);
	}
}
