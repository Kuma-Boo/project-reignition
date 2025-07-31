using Godot;
using System;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary> Controls the bone hand in Skeleton Dome. </summary>
public partial class Hand : PathFollow3D
{
	/// <summary> Emitted when the hand catches the player and we need to teleport. </summary>
	[Signal] public delegate void PlayerCaughtEventHandler();

	[Export] private AnimationPlayer animator;
	[Export] private Node3D root;
	[Export] private bool isActive;

	private PlayerController Player => StageSettings.Player;
	private bool isGrabbingPlayer;
	private float initialProgress;

	public override void _Ready()
	{
		initialProgress = Progress;
		StageSettings.Instance.Respawned += Respawn;
		Respawn();

		if (isActive)
			Activate();
		else
			Deactivate();
	}

	private void Respawn()
	{
		Progress = initialProgress;
		isGrabbingPlayer = false;
		moveSpeed = moveSpeedVelocity = 0f;
		animator.Play("move");
	}

	private void Activate()
	{
		isActive = true;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	private void Deactivate()
	{
		isActive = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _PhysicsProcess(double _)
	{
		if (isGrabbingPlayer || !isActive)
			return;

		ProcessMovement();
	}

	private void OnEntered(Area3D a)
	{
		if (isGrabbingPlayer || !a.IsInGroup("player detection"))
			return;

		LaunchSettings settings = LaunchSettings.Create(Player.GlobalPosition, root.GlobalPosition, 2f, true);
		Player.StartLauncher(settings);
		Player.Animator.StartSpin();

		isGrabbingPlayer = true;
		animator.Play("grab");
	}

	/// <summary> Copied from CaptainBemoth.cs". </summary>
	private float moveSpeed;
	private float moveSpeedVelocity;
	private readonly float MoveSpeedSmoothing = 10f;
	private readonly float BaseMoveSpeed = 25f;
	private readonly float MinimumDistance = 2f;
	private readonly float MinimumDistanceSmoothingStart = 10f;
	private readonly float StopDistance = 40f;
	public float GetDeltaProgress()
	{
		float bossProgress = Player.PathFollower.GetProgress(GlobalPosition);
		float deltaProgress = bossProgress - Player.PathFollower.Progress;
		if (deltaProgress < -Player.PathFollower.ActivePath.Curve.GetBakedLength() * .5f)
			deltaProgress += Player.PathFollower.ActivePath.Curve.GetBakedLength();
		return deltaProgress;
	}

	private void ProcessMovement()
	{
		float deltaProgress = GetDeltaProgress();
		float speedSmoothing = MoveSpeedSmoothing;
		float speedRatio = 1f - Mathf.Clamp(deltaProgress / StopDistance, 0f, 1f);
		float targetMoveSpeed = BaseMoveSpeed * speedRatio;

		if (!Player.IsHomingAttacking || Player.Lockon.Target != root)
		{
			if (Player.IsMovingBackward)
			{
				targetMoveSpeed -= Player.MoveSpeed;
			}
			else if (deltaProgress <= MinimumDistanceSmoothingStart)
			{
				float smoothingRatio = 1f - ((deltaProgress - MinimumDistance) / (MinimumDistanceSmoothingStart - MinimumDistance));
				targetMoveSpeed += Player.MoveSpeed * smoothingRatio;
				speedSmoothing = Mathf.Lerp(speedSmoothing, 0, smoothingRatio);
			}
		}

		moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, targetMoveSpeed, ref moveSpeedVelocity, speedSmoothing * PhysicsManager.physicsDelta);
		Progress += moveSpeed * PhysicsManager.physicsDelta;
	}
}
