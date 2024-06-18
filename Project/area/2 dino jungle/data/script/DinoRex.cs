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
	private readonly StringName LowerBiteState = "lower_bite";
	private readonly StringName UpperBiteState = "upper_bite";

	/// <summary> Is the dino currently in the middle of an attack animation? </summary>
	private bool isAttacking;
	private float currentRotation;
	private float targetRotation;
	private float rotationVelocity;
	private readonly float RotationSmoothing = 10.0f;

	private RexStates currentState;
	private RexStates targetState;
	private enum RexStates
	{
		Idle, // Dino is not attacking
		Lower, // Chomp at the sidle section
		Tail, // Swing the T-Rex's tail
		Upper, // Chomp at the top rocks
	}

	public override void _Ready()
	{
		animationTree.Active = true;
		StageSettings.instance.ConnectRespawnSignal(this);
	}

	private void Respawn()
	{
		isAttacking = false;
		currentState = RexStates.Idle;
		targetState = RexStates.Idle;

		currentRotation = targetRotation = rotationVelocity = 0;
		root.RotationDegrees = Vector3.Zero;

		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public override void _PhysicsProcess(double _)
	{
		if (isAttacking)
			return;

		if (currentState != targetState)
		{
			currentState = targetState;
			return;
		}

		UpdateRotation();
	}

	private void UpdateRotation()
	{
		if (currentState == RexStates.Upper && Mathf.IsZeroApprox(rotationVelocity)) // Calculate target rotation
		{
			Vector3 targetPosition = CharacterController.instance.GlobalPosition + (CharacterController.instance.Forward() * 2f);
			targetRotation = (targetPosition - GlobalPosition).Flatten().AngleTo(Vector2.Down);
		}

		if (currentState != RexStates.Idle && Mathf.Abs(Mathf.AngleDifference(currentRotation, targetRotation)) < .1f)
		{
			// Snap rotation
			rotationVelocity = 0;
			currentRotation = targetRotation;
			StartAttack();
		}

		currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, RotationSmoothing * PhysicsManager.physicsDelta);
		root.Rotation = Vector3.Up * currentRotation;
	}

	public void OnLowerBiteEnter(Area3D _, float attackAngle)
	{
		targetState = RexStates.Lower;
		targetRotation = Mathf.DegToRad(attackAngle);
	}

	public void OnUpperBiteEnter(Area3D _) => targetState = RexStates.Upper;
	public void OnReturnToIdle(Area3D _) => targetState = RexStates.Idle;

	private void StartAttack()
	{
		// Play attack animation
		isAttacking = true;
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

		switch (currentState)
		{
			case RexStates.Lower:
				animationTree.Set(AttackTransition, LowerBiteState);
				break;
			case RexStates.Upper:
				animationTree.Set(AttackTransition, UpperBiteState);
				break;
		}
	}

	private void FinishAttack() => isAttacking = false;
}
