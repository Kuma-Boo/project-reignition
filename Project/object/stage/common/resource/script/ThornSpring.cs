using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	[Tool]
	public partial class ThornSpring : Launcher
	{
		[Export]
		private float height;
		[Export(PropertyHint.Range, "0,5,.1")]
		/// <summary> How long a rotation phase (full or half) takes in seconds. </summary>
		private float rotationTime;
		[Export(PropertyHint.Range, "0,5,.1")]
		/// <summary> How long to wait between rotation phases. </summary>
		private float staticTime;
		[Export]
		/// <summary> Only rotate when player gets launched? </summary>
		private bool rotateOnLaunch;
		[Export]
		/// <summary> Split rotation phase into two? </summary>
		private bool pauseHalfway;
		[Export]
		/// <summary> Only allow targeting when timebreak is active. </summary>
		private bool isTimebreakSpring;

		/// <summary> The amount of time spent in the current state. </summary>
		private float currentTime;
		/// <summary> Is the thorn spring currently rotating? </summary>
		private State rotationState;
		private enum State
		{
			UPRIGHT,
			ROTATING,
			HALFWAY,
			LOOPING,
		}

		[Export]
		private AnimationPlayer animator;
		private readonly StringName RESET_KEY = "RESET";
		/// <summary> Animation name for a single, full rotation. </summary>
		private readonly StringName FULL_KEY = "full";
		/// <summary> First half of the thorn spring's halfway animation. </summary>
		private readonly StringName HALF_KEY = "half";
		/// <summary> Second half of the thorn spring's halfway animation and Timebreak activation. </summary>
		private readonly StringName ENABLE_KEY = "enable";
		/// <summary> Timebreak thorn springs' looping animation. </summary>
		private readonly StringName TIMEBREAK_KEY = "loop";
		/// <summary> Animator time scale for timebreak springs when timebreak isn't active. </summary>
		private const float INACTIVE_TIMEBREAK_TIME_SCALE = 4f;


		public override Vector3 GetLaunchDirection() => Vector3.Up;
		public override void Activate(Area3D a)
		{
			base.Activate(a);

			if (rotateOnLaunch)
				StartRotation();
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

			if (isTimebreakSpring)
			{
				UpdateTimebreakSpring();
				return;
			}

			UpdateRotationTimer();
		}


		/// <summary> Updates a timebreak spring based on the player's break skills. </summary>
		private void UpdateTimebreakSpring()
		{
			GD.Print(rotationState);

			if (!Character.Skills.IsTimeBreakActive)
			{
				if (rotationState != State.LOOPING) // Return to spinning quickly
					StartTimebreakRotation();

				return;
			}


			if (rotationState == State.LOOPING)
				StopTimebreakRotation(); // Stop spinning
			else
				UpdateRotationTimer();
		}


		private void StartTimebreakRotation()
		{
			if (animator.CurrentAnimation == TIMEBREAK_KEY)
				return; // Already in the looping animation

			rotationState = State.LOOPING;
			animator.Play(TIMEBREAK_KEY);
			animator.SpeedScale = INACTIVE_TIMEBREAK_TIME_SCALE;
		}


		/// <summary> Transitions from quickly spinning to stationary. </summary>
		private void StopTimebreakRotation() => animator.Play(ENABLE_KEY);


		private void UpdateRotationTimer()
		{
			if (rotationState == State.ROTATING)
				return; // Don't update times when rotating

			currentTime = Mathf.MoveToward(currentTime, staticTime, PhysicsManager.physicsDelta);
			if (!Mathf.IsEqualApprox(currentTime, staticTime))
				return; // Still waiting


			if (rotationState == State.HALFWAY)
			{
				FinishRotation(); // Finish the current rotation
				return;
			}

			if (Character.Lockon.IsHomingAttacking && Character.Lockon.Target == this)
				return; // Don't start rotating if the player is attacking this spring

			// Start a new rotation
			StartRotation();
		}


		/// <summary> Reset time and stop rotating. </summary>
		private void OnAnimationFinished(StringName animationName)
		{
			currentTime = 0;

			if (animationName == HALF_KEY)
			{
				// Switch to halfway state
				rotationState = State.HALFWAY;
				return;
			}

			if (animationName == FULL_KEY || animationName == ENABLE_KEY)
			{
				// Reset rotation to avoid janky transitions
				animator.Play(RESET_KEY);
				animator.Advance(0.0);
				rotationState = State.UPRIGHT;
			}
		}


		private void StartRotation()
		{
			rotationState = State.ROTATING;
			animator.SpeedScale = 1.0f / rotationTime;
			animator.Play(pauseHalfway ? HALF_KEY : FULL_KEY);
		}


		/// <summary> Completes the spring's rotation. Only called when rotationMode is set to RotationMode.Half. </summary>
		private void FinishRotation()
		{
			rotationState = State.ROTATING;
			animator.SpeedScale = 1.0f / rotationTime;
			animator.Play(ENABLE_KEY);
		}
	}
}
