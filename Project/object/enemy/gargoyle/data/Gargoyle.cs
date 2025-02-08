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

	private Vector3 homePosition;
	private Vector3 targetPosition;
	private Vector3 velocity;
	private readonly float MovementSmoothing = .5f;

	private bool isActionTimerPaused;
	private float actionTimer;
	private readonly float ActionInterval = 3.0f;
	public void DisableActionTimer() => isActionTimerPaused = true;
	public void EnableActionTimer() => isActionTimerPaused = false;

	private AnimationNodeStateMachinePlayback MovementState => AnimationTree.Get(MovementPlaybackParameter).Obj as AnimationNodeStateMachinePlayback;
	private readonly StringName MovementPlaybackParameter = "parameters/movement_state/playback";
	private readonly StringName StatueState = "statue";
	private readonly StringName IdleState = "idle";
	private readonly StringName SkyroadState = "skyroad";

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
		base.SetUp();
		homePosition = GlobalPosition + (this.Forward() * 2.0f);
	}

	public override void Respawn()
	{
		base.Respawn();

		velocity = Vector3.Zero;

		DisableActionTimer();
		actionTimer = ActionInterval;

		MovementState.Start(StatueState);
		AnimationTree.Set(DefeatTransition, "reset");
	}

	protected override void UpdateEnemy()
	{
		if (state == State.Statue)
			return;

		if (!IsInRange)
			return;

		UpdateActions();
	}

	protected override void Spawn()
	{
		base.Spawn();

		state = State.Idle;
		targetPosition = homePosition;
		MovementState.Travel(IdleState);
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
			Player.StartBounce();
			actionTimer = 0;
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
		AnimationTree.Set(DefeatTransition, "enabled");
	}

	private void StartAction()
	{
		actionTimer = ActionInterval;
		DisableActionTimer();

		switch (state)
		{
			case State.Idle:
				float randomNumber = Runtime.randomNumberGenerator.Randf();
				if (randomNumber < .5f)
					StartPetrify();
				else
					StartSlash();

				break;
			case State.Hitstun:
				break;
		}
	}

	private void UpdateActions()
	{
		if (state == State.Idle)
		{
			GlobalPosition = GlobalPosition.SmoothDamp(targetPosition, ref velocity, MovementSmoothing);
		}

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

	private void StartSlash()
	{
		state = State.Slash;
		targetPosition = Player.GlobalPosition;
		targetPosition.Y = Mathf.Lerp(homePosition.Y, targetPosition.Y, .5f); // Reduce vertical following ability

		AnimationTree.Set(ActionTransition, SlashState);
		AnimationTree.Set(ActionTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void StartIdle()
	{
		state = State.Idle;
		EnableActionTimer();
	}

	/// <summary> Called whenever the player touches the gargoyle's hands. </summary>
	private void OnHitboxEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		Player.StartKnockback();
	}
}
