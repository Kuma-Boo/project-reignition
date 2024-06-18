using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class DinoTrio : PathFollow3D
{
	[Signal]
	public delegate void DamagedPlayerEventHandler();

	private DinoTrioProcessor Processor => DinoTrioProcessor.Instance;
	private CharacterController Character => CharacterController.instance;

	[ExportGroup("Movement")]
	[Export]
	private float traction;
	[Export]
	private float friction;
	/// <summary> Target offset from player. </summary>
	[Export]
	private float preferredOffset;
	[Export(PropertyHint.Range, "0, 1")]
	private float rubberbandingStrength;

	/// <summary> Base movespeed (without rubberbanding). </summary>
	private float moveSpeed;
	private float rubberbandingSpeed;
	public float DeltaProgress => Processor.PlayerProgress - Progress;

	/// <summary> Total movespeed (currentMoveSpeed + rubberbanding). </summary>
	private float TotalMoveSpeed
	{
		get
		{
			float spd = moveSpeed + rubberbandingSpeed;
			if (CurrentAttackState != AttackStates.Inactive)
				spd *= speedMultiplier;

			return spd < 2f ? 0 : spd;
		}
	}

	[ExportGroup("Animation")]
	[Export]
	public AttackStates CurrentAttackState { get; private set; }
	public enum AttackStates
	{
		Inactive,
		Windup,
		Charge,
		Toss,
		Recovery
	}
	[Export(PropertyHint.Range, "0, 5")]
	private float speedMultiplier;

	public override void _Ready()
	{
		animationTree.Active = true;
		StageSettings.instance.ConnectRespawnSignal(this);
		Respawn();
	}

	private void Respawn()
	{
		Progress = 0;
		moveSpeed = rubberbandingSpeed = 0;

		tossedPlayer = false;
		CurrentAttackState = AttackStates.Inactive;

		// Reset animations
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		animationTree.Set(FidgetTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
	}

	public void ProcessDino()
	{
		CalculateMovespeed();
		UpdateAnimations();

		Progress += TotalMoveSpeed * PhysicsManager.physicsDelta;

		if (isInteractingWithPlayer)
			DamagePlayer();
	}

	private void CalculateMovespeed()
	{
		if (Character.Skills.IsSpeedBreakCharging) // Kill speed quickly when starting a speed break
		{
			moveSpeed *= .8f;
			rubberbandingSpeed = 0;
			return;
		}

		if (Processor.IsSlowingDown ||
			(CurrentAttackState != AttackStates.Charge && CurrentAttackState != AttackStates.Inactive) ||
			(Character.IsLockoutActive && Character.ActiveLockoutData.recenterPlayer) ||
			Character.Skills.IsSpeedBreakActive)
		{
			// Dino is slowing down
			moveSpeed = Mathf.MoveToward(moveSpeed, 0, friction * PhysicsManager.physicsDelta);
			rubberbandingSpeed = 0;
			return;
		}

		// Accelerate
		if (CurrentAttackState == AttackStates.Charge)
		{
			moveSpeed = Mathf.Lerp(moveSpeed, Character.MoveSpeed + Mathf.Abs(DeltaProgress * 1.5f), .5f);
			rubberbandingSpeed = 0;
			return;
		}

		// Normal chasing
		float targetSpeed = Mathf.Clamp(Character.MoveSpeed - Processor.SpeedDifference, 0, Character.Skills.GroundSettings.speed);
		if (Mathf.Abs(DeltaProgress) > DinoTrioProcessor.AttackOffset)
			targetSpeed = Character.Skills.GroundSettings.speed - Processor.SpeedDifference;

		moveSpeed = Mathf.MoveToward(moveSpeed, targetSpeed, traction * PhysicsManager.physicsDelta);

		// Rubberbanding
		rubberbandingSpeed = (DeltaProgress - preferredOffset) * rubberbandingStrength;
	}

	[Export]
	private AnimationTree animationTree;
	private readonly StringName EnabledConstant = "enabled";
	private readonly StringName DisabledConstant = "disabled";

	private readonly StringName IdleSeekParameter = "parameters/idle_seek/seek_request";
	private readonly StringName FidgetTrigger = "parameters/fidget_trigger/request";
	private readonly StringName FidgetTransition = "parameters/fidget/transition_request";
	private readonly StringName PawFidgetAnimation = "paw";
	private readonly StringName ShakeFidgetAnimation = "shake";

	private readonly StringName MovingTransition = "parameters/movement_transition/current_state";
	private readonly StringName MovingTransitionRequest = "parameters/movement_transition/transition_request";

	private readonly StringName MovementBlendParameter = "parameters/movement_blend/blend_position";
	private readonly StringName MovementSpeedParameter = "parameters/movement_speed/scale";
	private readonly StringName MovementSeekParameter = "parameters/movement_seek/seek_request";

	private readonly StringName AttackTrigger = "parameters/attack_trigger/request";
	private void UpdateAnimations()
	{
		string movingState = (string)animationTree.Get(MovingTransition);
		if (Mathf.IsZeroApprox(TotalMoveSpeed) && movingState == EnabledConstant)
		{
			animationTree.Set(MovingTransitionRequest, DisabledConstant);
			animationTree.Set(IdleSeekParameter, Runtime.randomNumberGenerator.RandfRange(0, 5));
		}
		else if (!Mathf.IsZeroApprox(TotalMoveSpeed) && movingState == DisabledConstant)
		{
			animationTree.Set(MovingTransitionRequest, EnabledConstant);
			animationTree.Set(MovementSeekParameter, Runtime.randomNumberGenerator.RandfRange(0, 5));
		}

		animationTree.Set(MovementSpeedParameter, 1.5f + (Character.Skills.GroundSettings.GetSpeedRatio(TotalMoveSpeed) * .8f));
		animationTree.Set(MovementBlendParameter, Character.Skills.GroundSettings.GetSpeedRatioClamped(TotalMoveSpeed * 1.5f));
	}

	public void StartIdleFidget()
	{
		if (Runtime.randomNumberGenerator.Randf() < .2f) // Don't fidget
			return;

		animationTree.Set(FidgetTransition, Runtime.randomNumberGenerator.Randf() > .5f ? PawFidgetAnimation : ShakeFidgetAnimation);
		animationTree.Set(FidgetTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void StartAttack() => animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);

	private bool tossedPlayer; // Last state we processed damage in
	private void DamagePlayer()
	{
		switch (CurrentAttackState)
		{
			case AttackStates.Toss: // Powerful launch
				if (tossedPlayer) return; // Already tossed the player

				Character.StartKnockback(new()
				{
					knockForward = true, // Always knock forward
					ignoreInvincibility = true, // Always knockback the player
					overrideKnockbackSpeed = true,
					knockbackSpeed = 40f,
					overrideKnockbackHeight = true,
					knockbackHeight = 3
				});

				break;
			default: // Normal knockback
				Character.StartKnockback(new()
				{
					knockForward = true,
					ignoreInvincibility = true,
					overrideKnockbackSpeed = true,
					knockbackSpeed = Mathf.Max(TotalMoveSpeed, 20f),
				});
				break;
		}

		tossedPlayer = CurrentAttackState == AttackStates.Toss;
		EmitSignal(SignalName.DamagedPlayer);
	}

	private bool isInteractingWithPlayer;
	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = true;
		tossedPlayer = false;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player"))
			return;

		isInteractingWithPlayer = false;
	}
}
