using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Makes the player do a 90 degree turn when entering fast enough.
/// </summary>
[Tool]
public partial class DriftTrigger : Area3D
{
	[Signal]
	public delegate void DriftStartedEventHandler();
	[Signal]
	public delegate void DriftCompletedEventHandler();

	[Export]
	private bool isRightTurn; // Which way is the corner?

	// Public for the editor
	public Vector3 EndPosition => MiddlePosition + (ExitDirection * slideDistance);
	public Vector3 MiddlePosition => GlobalPosition + (this.Back() * slideDistance);
	public Vector3 ExitDirection => this.Right() * (isRightTurn ? 1 : -1);

	private float entrySpeed; // Entry speed
	private PlayerController Player => StageSettings.Player;

	private enum DriftStatus
	{
		Inactive, // Drift is not active
		Waiting, // Waiting for drift validation
		Processing, // Drift is being processed
		TimingFail, // The player mistimed the input
		WaitFail, // The player slid to a stop
		JumpFail, // Player jumped out of the drift
		Success, // Player successfully drifted
	}
	/// <summary> Result of the drift. </summary>
	private DriftStatus driftStatus;

	/// <summary> For smooth damping. </summary>
	private Vector3 driftVelocity;
	/// <summary> Positional smoothing. </summary>
	private const float DriftSmoothing = .25f;
	/// <summary> How generous the input window is (Due to player's decceleration, it's harder to get an early drift.) </summary>
	private const float InputWindowDistance = 1f;

	/// <summary> How far to slide. </summary>
	[Export(PropertyHint.Range, "1, 10")]
	private int slideDistance = 10;
	/// <summary> Entrance speed (ratio) required to start a drift. </summary>
	private const float EntranceSpeedRatio = .9f;

	[ExportGroup("Components")]
	[Export]
	private AudioStreamPlayer sfx;
	[Export]
	private LockoutResource lockout;
	private float startingVolume;
	private bool isFadingSFX;
	private float MinStartingVolume = -6f; // SFX volume when player enters slowly
	/// <summary> Delay animation state reset for this amount of time. </summary>
	private float driftAnimationTimer;
	/// <summary> Length of animation when player doesn't do anything. </summary>
	private const float DefaultAnimationLength = .2f;
	/// <summary> Length of animation when player succeeds. </summary>
	private const float LaunchAnimationLength = .4f;
	/// <summary> Length of animation when player faceplants. </summary>
	private const float FailAnimationLength = .8f;

	public override void _PhysicsProcess(double _)
	{
		if (driftStatus == DriftStatus.Inactive)
		{
			if (isFadingSFX)
				isFadingSFX = SoundManager.FadeAudioPlayer(sfx);

			if (driftAnimationTimer > 0)
			{
				driftAnimationTimer = Mathf.MoveToward(driftAnimationTimer, 0, PhysicsManager.physicsDelta);

				if (Mathf.IsZeroApprox(driftAnimationTimer))
					Player.Animator.ResetState(.4f);
			}

			return;
		}

		if (driftStatus == DriftStatus.Waiting &&
			Player.PathFollower.Progress < Player.PathFollower.GetProgress(GlobalPosition))
		{
			if (!IsDriftValid())
				return;

			StartDrift(); // Drift started successfully
		}

		if (driftStatus == DriftStatus.Processing || driftStatus == DriftStatus.TimingFail)
			UpdateDrift();
	}

	private bool IsDriftValid() // Checks whether the player is in a state where a drift is possible
	{
		if (Player.IsMovingBackward) return false; // Can't drift backwards
		if (!Player.IsOnGround || Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) < EntranceSpeedRatio) return false; // In air/too slow

		// REFACTOR TODO if (Player.MovementState == PlayerController.MovementStates.External) return false; // Player is already busy

		// Check for any obstructions
		RaycastHit hit = Player.CastRay(Player.CollisionPosition, Player.PathFollower.Forward() * slideDistance, Runtime.Instance.environmentMask);
		return !hit || hit.collidedObject.IsInGroup("level wall"); // Valid drift
	}

	private void StartDrift() // Initialize drift
	{
		driftStatus = DriftStatus.Processing;
		entrySpeed = Player.MoveSpeed;
		driftVelocity = Vector3.Zero;

		// Reset sfx volume
		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(entrySpeed) - (EntranceSpeedRatio / (1 - EntranceSpeedRatio));
		startingVolume = Mathf.Lerp(MinStartingVolume, 0, speedRatio);
		isFadingSFX = false;
		sfx.VolumeDb = startingVolume;
		sfx.Play();

		driftAnimationTimer = DefaultAnimationLength;
		Player.State.StartExternal(this); // For future reference, this is where speedbreak gets disabled
		Player.Effect.StartDust();
		Player.Animator.ExternalAngle = Player.MovementAngle;
		Player.Animator.StartDrift(isRightTurn);
		Player.Connect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift));

		EmitSignal(SignalName.DriftStarted);
	}

	private void UpdateDrift()
	{
		Vector3 targetPosition = MiddlePosition + (this.Back() * InputWindowDistance);

		// Process drift
		float distance = Player.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());
		Player.GlobalPosition = Player.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DriftSmoothing, entrySpeed);
		Player.UpDirection = Player.PathFollower.Up(); // Use pathfollower's up direction when drifting
		Player.State.UpdateExternalControl(true);
		Player.UpdateOrientation();

		// Fade out sfx based on distance
		float volume = distance / slideDistance;
		sfx.VolumeDb = Mathf.SmoothStep(startingVolume, -80f, volume);

		bool isManualDrift = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.DriftExp);
		bool isAttemptingDrift = ((Input.IsActionJustPressed("button_action") && isManualDrift) ||
			(!isManualDrift && distance <= InputWindowDistance)) && driftStatus != DriftStatus.TimingFail;

		if (Input.IsActionJustPressed("button_jump")) // Allow character to jump out of drift at any time
		{
			driftAnimationTimer = 0;
			driftStatus = DriftStatus.JumpFail;
			CompleteDrift();
			ApplyBonus();

			Player.MoveSpeed = driftVelocity.Length(); // Keep speed from drift
			return;
		}

		if (isAttemptingDrift)
		{
			if (distance <= InputWindowDistance * 2f) // Successful drift
			{
				driftStatus = DriftStatus.Success;
				driftAnimationTimer = LaunchAnimationLength;

				if (isManualDrift)
				{
					BonusManager.instance.QueueBonus(new(BonusType.EXP, 100));
					Player.Skills.ModifySoulGauge(10); // Not written in skill description, but that's what the original game does -_-
					Player.Effect.PlayDarkSpiralFX();
				}
			}
			else // Too early! Fail drift attempt and play a special animation?
			{
				driftStatus = DriftStatus.TimingFail;
				driftAnimationTimer = FailAnimationLength;
				return;
			}

			ApplyBonus();
			CompleteDrift();
			return;
		}

		if (distance >= .3f || !isManualDrift) return;

		// Slid to a stop
		driftStatus = DriftStatus.WaitFail;
		driftAnimationTimer = PhysicsManager.physicsDelta;
		Player.MoveSpeed = 0f; // Reset Movespeed
		CompleteDrift();
	}

	private void CompleteDrift()
	{
		isFadingSFX = true; // Fade sound effect

		// Turn 90 degrees
		if (driftStatus == DriftStatus.JumpFail)
		{
			Player.Animator.ResetState(0f);
		}
		else if (driftStatus == DriftStatus.Success)
		{
			Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(ExitDirection, Player.PathFollower.Up());
			Player.MovementAngle -= Mathf.Pi * .1f * Player.Controller.InputHorizontal;

			Player.State.AddLockoutData(lockout); // Apply lockout

			Player.Animator.LaunchDrift();
			Player.Animator.ExternalAngle = Player.MovementAngle;
		}

		driftStatus = DriftStatus.Inactive; // Reset to inactive state
		Player.State.StopExternal();
		Player.Effect.StopDust();

		if (Player.IsConnected(PlayerController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift)))
			Player.Disconnect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift));

		EmitSignal(SignalName.DriftCompleted);
	}

	/// <summary> Tracks whether drift bonus was already applied. </summary>
	private bool wasBonusApplied;
	private void ApplyBonus()
	{
		if (wasBonusApplied) return; // Bonus was already applied

		if (driftStatus == DriftStatus.Success)
			BonusManager.instance.QueueBonus(new(BonusType.Drift));

		wasBonusApplied = true;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		driftStatus = DriftStatus.Waiting;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		if (driftStatus == DriftStatus.Waiting)
		{
			ApplyBonus(); // Invalid drift, skip bonus (if possible)
			driftStatus = DriftStatus.Inactive; // Reset to inactive state
		}
	}
}