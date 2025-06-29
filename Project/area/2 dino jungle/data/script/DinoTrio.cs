using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class DinoTrio : PathFollow3D
{
	[Signal]
	public delegate void DamagedPlayerEventHandler();
	[Signal]
	public delegate void WindupEventHandler();

	private DinoTrioProcessor Processor => DinoTrioProcessor.Instance;
	private PlayerController Player => StageSettings.Player;

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

	[Export]
	private GroupAudioStreamPlayer3D stepSfx;
	[Export]
	private AudioStreamPlayer3D windupSfx;

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
		StageSettings.Instance.Respawned += Respawn;
		Respawn();
	}

	private void Respawn()
	{
		isInteractingWithPlayer = false;

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
		if (Player.Skills.IsSpeedBreakCharging) // Kill speed quickly when starting a speed break
		{
			moveSpeed *= .8f;
			rubberbandingSpeed = 0;
			return;
		}

		if (Processor.IsSlowingDown ||
			(CurrentAttackState != AttackStates.Charge && CurrentAttackState != AttackStates.Inactive) ||
			(Player.IsLockoutActive && Player.ActiveLockoutData.recenterPlayer) ||
			Player.Skills.IsSpeedBreakActive ||
			(Player.Camera.IsCrossfading && CurrentAttackState == AttackStates.Charge))
		{
			// Dino is slowing down
			moveSpeed = Mathf.MoveToward(moveSpeed, 0, friction * PhysicsManager.physicsDelta);
			rubberbandingSpeed = 0;
			SoundManager.FadeAudioPlayer(stepSfx, 2.0f);
			return;
		}

		float playerSpeed = Mathf.Min(Player.MoveSpeed, Player.Stats.GroundSettings.Speed);
		// Accelerate
		if (CurrentAttackState == AttackStates.Charge)
		{
			moveSpeed = Mathf.Lerp(moveSpeed, playerSpeed + Mathf.Abs(DeltaProgress * 1.5f), .5f);
			rubberbandingSpeed = 0;
			return;
		}

		// Normal chasing
		float targetSpeed = Mathf.Clamp(playerSpeed - Processor.SpeedDifference, 0, Player.Stats.GroundSettings.Speed);
		if (Mathf.Abs(DeltaProgress) > DinoTrioProcessor.AttackOffset)
			targetSpeed = Player.Stats.GroundSettings.Speed - Processor.SpeedDifference;

		moveSpeed = Mathf.MoveToward(moveSpeed, targetSpeed, traction * PhysicsManager.physicsDelta);

		// Rubberbanding
		rubberbandingSpeed = (DeltaProgress - preferredOffset) * rubberbandingStrength;

		stepSfx.VolumeDb = 0;
		if (!stepSfx.Playing)
			stepSfx.PlayInGroup();
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

		animationTree.Set(MovementSpeedParameter, 1.5f + (Player.Stats.GroundSettings.GetSpeedRatio(TotalMoveSpeed) * .8f));
		animationTree.Set(MovementBlendParameter, Player.Stats.GroundSettings.GetSpeedRatioClamped(TotalMoveSpeed * 1.5f));
	}

	public void StartIdleFidget()
	{
		if (Runtime.randomNumberGenerator.Randf() < .2f) // Don't fidget
			return;

		animationTree.Set(FidgetTransition, Runtime.randomNumberGenerator.Randf() > .5f ? PawFidgetAnimation : ShakeFidgetAnimation);
		animationTree.Set(FidgetTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void StartAttack()
	{
		animationTree.Set(AttackTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		windupSfx.Play();
	}

	private bool tossedPlayer; // Last state we processed damage in
	private void DamagePlayer()
	{
		switch (CurrentAttackState)
		{
			case AttackStates.Toss: // Powerful launch
				if (tossedPlayer) return; // Already tossed the player

				Player.StartKnockback(new()
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
				Player.StartKnockback(new()
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
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = true;
		tossedPlayer = false;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = false;
	}
}
