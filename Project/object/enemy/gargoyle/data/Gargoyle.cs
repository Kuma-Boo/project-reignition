using Godot;
using Project.Core;

namespace Project.Gameplay;

/*
Changes from the retail version:
Blocking only occurs as a response to getting damaged -- good players should never see it happen.
Gargoyle always tries to slash if the player is petrified
*/
[Tool]
public partial class Gargoyle : Enemy
{
	[Export] private Node3D windball;
	private bool isWindballActive;
	private readonly float WindballMoveSpeed = 10.0f;

	private State state;
	private enum State
	{
		Statue,
		Idle,
		Hitstun,
		Slash,
		Petrify,
		Block,
	}

	private State queuedState;

	private bool isSlashMovementEnabled;
	private Vector3 homePosition;
	private Vector3 targetPosition;
	private Vector3 velocity;
	private readonly float MovementSmoothing = .5f;

	private bool isActionTimerPaused;
	private float actionTimer;
	private readonly float ActionInterval = 2.0f;
	private readonly float BlockLength = 3.0f;
	public void DisableActionTimer() => isActionTimerPaused = true;
	public void EnableActionTimer() => isActionTimerPaused = false;

	private AnimationNodeStateMachinePlayback MovementStatePlayback => AnimationTree.Get(MovementPlaybackParameter).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName MovementPlaybackParameter = "parameters/movement_state/playback";
	private readonly StringName InitialMovementState = "RESET";
	private readonly StringName IdleState = "idle";
	private readonly StringName SkyroadState = "skyroad";

	private AnimationNodeStateMachinePlayback BlockStatePlayback => AnimationTree.Get(BlockPlaybackParameter).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName BlockPlaybackParameter = "parameters/block_state/playback";
	private readonly StringName BlockStartState = "block-start";
	private readonly StringName BlockStunState = "block-stun";
	private readonly StringName BlockEndState = "block-end";

	private readonly StringName ActionTrigger = "parameters/action_trigger/request";
	private readonly StringName ActionTransition = "parameters/action_transition/transition_request";
	private readonly StringName PetrifyState = "petrify";
	private readonly StringName SlashState = "slash";
	private readonly StringName BlockState = "block";

	private readonly StringName DamageTrigger = "parameters/damage_trigger/request";
	private readonly StringName DamageDirectionTransition = "parameters/damage_direction_transition/transition_request";
	private readonly StringName DefeatTransition = "parameters/defeat_transition/transition_request";

	protected override void SetUp()
	{
		if (Engine.IsEditorHint())
			return;

		base.SetUp();
		homePosition = GlobalPosition + (this.Forward() * 2.0f);
	}

	public override void Respawn()
	{
		state = State.Statue;
		velocity = Vector3.Zero;
		isSlashMovementEnabled = false;
		isWindballActive = false;

		DisableActionTimer();
		actionTimer = ActionInterval;

		currentRotation = 0;
		Root.Rotation = Vector3.Zero;

		MovementStatePlayback.Start(InitialMovementState);
		AnimationTree.Set(DefeatTransition, "disabled");
		AnimationTree.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);
		AnimationTree.Set(ActionTrigger, (int)AnimationNodeOneShot.OneShotRequest.Abort);

		base.Respawn();
	}

	protected override void UpdateEnemy()
	{
		if (state == State.Statue)
			return;

		if (!IsInRange && !Player.IsPetrified)
			return;

		UpdateActions();
	}

	protected override void Spawn()
	{
		base.Spawn();

		StartIdle();
		MovementStatePlayback.Travel(IdleState);
	}

	protected override void UpdateInteraction()
	{
		if (Player.AttackState == PlayerController.AttackStates.OneShot)
		{
			base.UpdateInteraction();
			return;
		}

		if (state == State.Block)
		{
			if (Player.AttackState == PlayerController.AttackStates.Weak)
			{
				actionTimer = Mathf.MoveToward(actionTimer, 0f, 1f);
				BlockStatePlayback.Travel(BlockStunState);
			}
			else if (Player.AttackState == PlayerController.AttackStates.Strong)
			{
				actionTimer = 0f;
			}

			Player.StartBounce();
			SetInteractionProcessed();
			return;
		}

		base.UpdateInteraction();
	}

	public override void TakeDamage(int amount = -1)
	{
		base.TakeDamage(amount);

		if (IsDefeated)
			return;

		state = State.Hitstun;

		// Randomly choose between left and right damage animations
		AnimationTree.Set(DamageDirectionTransition, Runtime.randomNumberGenerator.Randf() > 0.5f ? "left" : "right");
		AnimationTree.Set(DamageTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	protected override void Defeat()
	{
		base.Defeat();
		SetHitboxStatus(false);
		AnimationTree.Set(DefeatTransition, "enabled");
	}

	private void StartAction()
	{
		actionTimer = ActionInterval;
		DisableActionTimer();

		switch (state)
		{
			case State.Idle:
				if (Player.IsPetrified || Runtime.randomNumberGenerator.Randf() < .5f)
				{
					StartSlash();
				}
				else
				{
					actionTimer *= .5f;
					StartPetrify();
				}
				break;
			case State.Block:
				BlockStatePlayback.Travel(BlockEndState);
				StartIdle();
				break;
		}
	}

	private void UpdateActions()
	{
		if (state == State.Idle || isSlashMovementEnabled)
		{
			GlobalPosition = GlobalPosition.SmoothDamp(targetPosition, ref velocity, MovementSmoothing);
			TrackPlayer();
			Root.Rotation = new Vector3(Root.Rotation.X, currentRotation, Root.Rotation.Z);
		}

		if (isWindballActive)
			windball.GlobalPosition += windball.Forward() * WindballMoveSpeed * PhysicsManager.physicsDelta;

		if (isActionTimerPaused)
			return;

		actionTimer = Mathf.MoveToward(actionTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(actionTimer))
			StartAction();
	}

	private void StartPetrify()
	{
		state = State.Petrify;

		AnimationTree.Set(ActionTransition, PetrifyState);
		AnimationTree.Set(ActionTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void ApplyPetrification() => Player.StartPetrify();

	private void StartSlash()
	{
		state = State.Slash;

		AnimationTree.Set(ActionTransition, SlashState);
		AnimationTree.Set(ActionTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void StartSlashMovement()
	{
		isSlashMovementEnabled = true;
		targetPosition = Player.GlobalPosition + ((Player.GlobalPosition - GlobalPosition).Normalized() * 5.0f);
		targetPosition.Y = Mathf.Lerp(homePosition.Y, targetPosition.Y, .5f); // Reduce vertical following ability
	}

	private void StartIdle()
	{
		state = State.Idle;
		targetPosition = homePosition;
		isSlashMovementEnabled = false;
		EnableActionTimer();
	}

	private void StartBlock()
	{
		state = State.Block;
		actionTimer = BlockLength;

		BlockStatePlayback.Start(BlockStartState);
		AnimationTree.Set(ActionTransition, BlockState);
		AnimationTree.Set(ActionTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void StartWindball()
	{
		isWindballActive = true;
		windball.GlobalTransform = Root.GlobalTransform;
	}
	private void StopWindball() => isWindballActive = false;

	/// <summary> Called whenever the player touches the gargoyle's hands. </summary>
	private void OnHitboxEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		Player.StartKnockback();
	}

	private void OnWindballEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		Player.StartBounce();
	}
}
