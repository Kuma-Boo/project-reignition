using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
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

		//Public for the editor
		public Vector3 EndPosition => MiddlePosition + ExitDirection * slideDistance;
		public Vector3 MiddlePosition => GlobalPosition + this.Back() * slideDistance;
		public Vector3 ExitDirection => this.Right() * (isRightTurn ? 1 : -1);

		private float entrySpeed; // Entry speed
		private CharacterController Character => CharacterController.instance;

		/// <summary> Is this drift trigger currently being processed? </summary>
		private bool isProcessing;
		private enum DriftResults
		{
			WAITING, // Waiting for drift input
			TIME_FAIL, // The player mistimed the input
			WAIT_FAIL, // The player slid to a stop
			SUCCESS, // Player successfully drifted
			JUMPED, // Player jumped out of the drift
		}
		/// <summary> Result of the drift. </summary>
		private DriftResults driftResult;

		/// <summary> For smooth damping. </summary>
		private Vector3 driftVelocity;
		/// <summary> Positional smoothing. </summary>
		private const float DRIFT_SMOOTHING = .25f;
		/// <summary> How generous the input window is (Due to player's decceleration, it's harder to get an early drift.) </summary>
		private const float INPUT_WINDOW_DISTANCE = 1f;

		/// <summary> How far to slide. </summary>
		[Export(PropertyHint.Range, "1, 10")]
		private int slideDistance = 10;
		/// <summary> Entrance speed (ratio) required to start a drift. </summary>
		private const float ENTRANCE_SPEED_RATIO = .9f;

		[ExportGroup("Components")]
		[Export]
		private AudioStreamPlayer sfx;
		[Export]
		private LockoutResource lockout;
		private float startingVolume;
		private bool isFadingSFX;
		private float MIN_STARTING_VOLUME = -6f; //SFX volume when player enters slowly
		/// <summary> Delay animation state reset for this amount of time. </summary>
		private float driftAnimationTimer;
		/// <summary> Length of animation when player doesn't do anything. </summary>
		private const float DEFAULT_ANIMATION_LENGTH = .2f;
		/// <summary> Length of animation when player succeeds. </summary>
		private const float LAUNCH_ANIMATION_LENGTH = .4f;
		/// <summary> Length of animation when player faceplants. </summary>
		private const float FAIL_ANIMATION_LENGTH = .8f;

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
			if (!Character.IsOnGround || Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < ENTRANCE_SPEED_RATIO) return false; //In air/too slow
			if (Character.MovementState == CharacterController.MovementStates.External) return false; //Player is already busy

			// Check for any obstructions
			RaycastHit hit = Character.CastRay(Character.CenterPosition, Character.PathFollower.Back() * slideDistance, Runtime.Instance.environmentMask);
			if (hit && !hit.collidedObject.IsInGroup("level wall"))
				return false;

			return true; // Valid drift
		}

		private void StartDrift() // Initialize drift
		{
			isProcessing = true;

			entrySpeed = Character.MoveSpeed;
			driftVelocity = Vector3.Zero;

			//Reset sfx volume
			float speedRatio = Character.GroundSettings.GetSpeedRatioClamped(entrySpeed) - ENTRANCE_SPEED_RATIO / (1 - ENTRANCE_SPEED_RATIO);
			startingVolume = Mathf.Lerp(MIN_STARTING_VOLUME, 0, speedRatio);
			isFadingSFX = false;
			sfx.VolumeDb = startingVolume;
			sfx.Play();

			driftAnimationTimer = DEFAULT_ANIMATION_LENGTH;
			Character.StartExternal(this); // For future reference, this is where speedbreak gets disabled
			Character.Effect.StartDust();
			Character.Animator.ExternalAngle = Character.MovementAngle;
			Character.Animator.StartDrift(isRightTurn);
			Character.Connect(CharacterController.SignalName.Knockback, new Callable(this, MethodName.CompleteDrift));

			EmitSignal(SignalName.DriftStarted);
		}

		private void UpdateDrift()
		{
			if (driftResult != DriftResults.WAITING) return; // Drift was already failed

			Vector3 targetPosition = MiddlePosition + this.Back() * INPUT_WINDOW_DISTANCE;

			// Process drift
			float distance = Character.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());
			Character.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DRIFT_SMOOTHING, entrySpeed);
			Character.Velocity = driftVelocity;
			Character.MoveAndSlide();
			Character.UpDirection = Character.PathFollower.Up(); // Use pathfollower's up direction when drifting
			Character.UpdateExternalControl();
			Character.UpdateOrientation(true);
			Character.PathFollower.Resync(); //Resync

			// Fade out sfx based on distance
			float volume = distance / slideDistance;
			sfx.VolumeDb = Mathf.SmoothStep(startingVolume, -80f, volume);

			bool isAttemptingDrift = (Input.IsActionJustPressed("button_action") && Character.Skills.isManualDriftEnabled) ||
				(!Character.Skills.isManualDriftEnabled && distance <= INPUT_WINDOW_DISTANCE);

			if (Input.IsActionJustPressed("button_jump")) //Allow character to jump out of drift at any time
			{
				driftAnimationTimer = 0;
				driftResult = DriftResults.JUMPED;
				CompleteDrift();
				ApplyBonus();

				Character.MoveSpeed = driftVelocity.Length(); // Keep speed from drift
				return;
			}

			if (isAttemptingDrift)
			{
				if (distance <= INPUT_WINDOW_DISTANCE * 2f) // Successful drift
				{
					driftResult = DriftResults.SUCCESS;
					driftAnimationTimer = LAUNCH_ANIMATION_LENGTH;

					Character.Animator.LaunchDrift();
					Character.AddLockoutData(lockout); //Apply lockout
				}
				else // Too early! Fail drift attempt and play a special animation
				{
					driftResult = DriftResults.TIME_FAIL;
					driftAnimationTimer = FAIL_ANIMATION_LENGTH;
				}

				ApplyBonus();
				CompleteDrift();
				return;
			}

			if (distance >= .1f) return;

			// Slid to a stop
			driftResult = DriftResults.WAIT_FAIL;
			driftAnimationTimer = PhysicsManager.physicsDelta;
			Character.MoveSpeed = 0f; //Reset Movespeed
			CompleteDrift();
		}

		private void CompleteDrift()
		{
			isFadingSFX = true; //Fade sound effect
			isProcessing = false;

			//Turn 90 degrees
			if (driftResult == DriftResults.JUMPED)
				Character.Animator.ResetState(0f);
			else
			{
				Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(ExitDirection, Character.PathFollower.Up());
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

			wasBonusApplied = true;
		}

		public void OnEntered(Area3D _)
		{
			driftResult = DriftResults.WAITING;

			if (!IsDriftValid())
			{
				ApplyBonus(); // Invalid drift, skip bonus
				return;
			}

			StartDrift(); // Drift started successfully
		}
	}
}