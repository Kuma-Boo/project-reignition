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
	private CharacterController Character => CharacterController.instance;

	/// <summary> Is this drift trigger currently being processed? </summary>
	private bool isProcessing;
	private enum DriftResults
	{
		Waiting, // Waiting for drift input
		TimingFail, // The player mistimed the input
		WaitFail, // The player slid to a stop
		JumpFail, // Player jumped out of the drift
		Success, // Player successfully drifted
	}
	/// <summary> Result of the drift. </summary>
	private DriftResults driftResult;

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
	private const float FAilAnimationLength = .8f;

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
		{
			if (isFadingSFX)
				isFadingSFX = SoundManager.FadeAudioPlayer(sfx);

			if (driftAnimationTimer > 0)
			{
				driftAnimationTimer = Mathf.MoveToward(driftAnimationTimer, 0, PhysicsManager.physicsDelta);

				if (Mathf.IsZeroApprox(driftAnimationTimer))
					Character.Animator.ResetState(.4f);
			}

			return; // Inactive
		}

		UpdateDrift();
	}

	private bool IsDriftValid() // Checks whether the player is in a state where a drift is possible
	{
		if (Character.IsMovingBackward) return false; // Can't drift backwards
		if (!Character.IsOnGround || Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < EntranceSpeedRatio) return false; // In air/too slow
		if (Character.MovementState == CharacterController.MovementStates.External) return false; // Player is already busy

		// Check for any obstructions
		RaycastHit hit = Character.CastRay(Character.CollisionPosition, Character.PathFollower.Forward() * slideDistance, Runtime.Instance.environmentMask);
		return !hit || hit.collidedObject.IsInGroup("level wall"); // Valid drift
	}

	private void StartDrift() // Initialize drift
	{
		isProcessing = true;

		entrySpeed = Character.MoveSpeed;
		driftVelocity = Vector3.Zero;

		// Reset sfx volume
		float speedRatio = Character.GroundSettings.GetSpeedRatioClamped(entrySpeed) - (EntranceSpeedRatio / (1 - EntranceSpeedRatio));
		startingVolume = Mathf.Lerp(MinStartingVolume, 0, speedRatio);
		isFadingSFX = false;
		sfx.VolumeDb = startingVolume;
		sfx.Play();

		driftAnimationTimer = DefaultAnimationLength;
		Character.StartExternal(this); // For future reference, this is where speedbreak gets disabled
		Character.Effect.StartDust();
		Character.Animator.ExternalAngle = Character.MovementAngle;
		Character.Animator.StartDrift(isRightTurn);
		Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift));

		EmitSignal(SignalName.DriftStarted);
	}

	private void UpdateDrift()
	{
		if (driftResult != DriftResults.Waiting) return; // Drift was already failed

		Vector3 targetPosition = MiddlePosition + (this.Back() * InputWindowDistance);

		// Process drift
		float distance = Character.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());
		Character.GlobalPosition = Character.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DriftSmoothing, entrySpeed);
		Character.UpDirection = Character.PathFollower.Up(); // Use pathfollower's up direction when drifting
		Character.UpdateExternalControl(true);
		Character.UpdateOrientation(true);

		// Fade out sfx based on distance
		float volume = distance / slideDistance;
		sfx.VolumeDb = Mathf.SmoothStep(startingVolume, -80f, volume);

		bool isManualDrift = Character.Skills.IsSkillEquipped(SkillKey.DriftExperience);
		bool isAttemptingDrift = (Input.IsActionJustPressed("button_action") && isManualDrift) ||
			(!isManualDrift && distance <= InputWindowDistance);

		if (Input.IsActionJustPressed("button_jump")) // Allow character to jump out of drift at any time
		{
			driftAnimationTimer = 0;
			driftResult = DriftResults.JumpFail;
			CompleteDrift();
			ApplyBonus();

			Character.MoveSpeed = driftVelocity.Length(); // Keep speed from drift
			return;
		}

		if (isAttemptingDrift)
		{
			if (distance <= InputWindowDistance * 2f) // Successful drift
			{
				driftResult = DriftResults.Success;
				driftAnimationTimer = LaunchAnimationLength;
			}
			else // Too early! Fail drift attempt and play a special animation
			{
				driftResult = DriftResults.TimingFail;
				driftAnimationTimer = FAilAnimationLength;
			}

			ApplyBonus();
			CompleteDrift();
			return;
		}

		if (distance >= .1f) return;

		// Slid to a stop
		driftResult = DriftResults.WaitFail;
		driftAnimationTimer = PhysicsManager.physicsDelta;
		Character.MoveSpeed = 0f; // Reset Movespeed
		CompleteDrift();
	}

	private void CompleteDrift()
	{
		isFadingSFX = true; // Fade sound effect
		isProcessing = false;

		// Turn 90 degrees
		if (driftResult == DriftResults.JumpFail)
		{
			Character.Animator.ResetState(0f);
		}
		else
		{
			Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(ExitDirection, Character.PathFollower.Up());
			Character.MovementAngle -= Mathf.Pi * .1f * Character.InputVector.X;

			Character.AddLockoutData(lockout); // Apply lockout

			Character.Animator.LaunchDrift();
			Character.Animator.ExternalAngle = Character.MovementAngle;
		}

		Character.ResetMovementState();
		Character.Effect.StopDust();

		if (Character.IsConnected(CharacterController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift)))
			Character.Disconnect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift));

		EmitSignal(SignalName.DriftCompleted);
	}

	/// <summary> Tracks whether drift bonus was already applied. </summary>
	private bool wasBonusApplied;
	private void ApplyBonus()
	{
		if (wasBonusApplied) return; // Bonus was already applied

		if (driftResult == DriftResults.Success)
			BonusManager.instance.QueueBonus(new(BonusType.Drift));

		wasBonusApplied = true;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		driftResult = DriftResults.Waiting;

		if (!IsDriftValid())
		{
			ApplyBonus(); // Invalid drift, skip bonus
			return;
		}

		StartDrift(); // Drift started successfully
	}
}