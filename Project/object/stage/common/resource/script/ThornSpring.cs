using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

[Tool]
public partial class ThornSpring : Launcher
{
	/// <summary> How long a rotation phase (full or half) takes in seconds. </summary>
	[Export(PropertyHint.Range, "0,5,.1")]
	private float rotationTime;
	/// <summary> How long to wait between rotation phases. </summary>
	[Export(PropertyHint.Range, "0,5,.1")]
	private float staticTime;
	/// <summary> Should this spring rotate when player gets launched? </summary>
	[Export]
	private bool rotateOnLaunch;
	/// <summary> Split rotation phase into two? </summary>
	[Export]
	private bool pauseHalfway;
	/// <summary> Should this spring only allow targeting when time break is active? </summary>
	[Export]
	private bool isTimeBreakSpring;

	/// <summary> The amount of time spent in the current state. </summary>
	private float currentTime;
	/// <summary> The current state of the spring. </summary>
	private RotationStates rotationState;
	private enum RotationStates
	{
		Upright,
		Rotating,
		Halfway,
		Looping,
	}

	[Export]
	private AnimationPlayer animator;
	private readonly StringName resetKey = "RESET";
	/// <summary> Animation name for a single, full rotation. </summary>
	private readonly StringName fullKey = "full";
	/// <summary> First half of the thorn spring's animation. </summary>
	private readonly StringName halfKey = "half";
	/// <summary> Second half of the thorn spring's animation and time break activation. </summary>
	private readonly StringName enableKey = "enable";
	/// <summary> Time Break thorn springs' looping animation. </summary>
	private readonly StringName timeBreakKey = "loop";
	/// <summary> Animator timescale for Time Break springs when Time Break isn't active. </summary>
	private const float TimeBreakLoopTimeScale = 4f;
	/// <summary> Flag to pause timebreak rotation so the player doesn't get hurt. </summary>
	private bool pauseTimebreakRotation;

	public override Vector3 GetLaunchDirection() => Vector3.Up;
	public override void Activate(Area3D a)
	{
		if (Character.Lockon.IsHomingAttacking && Character.Lockon.Target == this) // Pause time break rotation temporarily
			pauseTimebreakRotation = true;

		base.Activate(a);

		if (rotateOnLaunch)
			StartRotation();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		if (isTimeBreakSpring)
		{
			UpdateTimeBreakSpring();
			return;
		}

		UpdateRotationTimer();
	}

	/// <summary> Updates a time break spring based on the player's break skills. </summary>
	private void UpdateTimeBreakSpring()
	{
		if (!Character.Skills.IsTimeBreakActive &&
			(!Character.Lockon.IsHomingAttacking || Character.Lockon.Target != this))
		{
			if (rotationState != RotationStates.Looping && !pauseTimebreakRotation) // Return to spinning quickly
				StartTimeBreakRotation();

			return;
		}

		if (rotationState == RotationStates.Looping)
			StopTimeBreakRotation(); // Stop spinning
		else
			UpdateRotationTimer();
	}

	private void StartTimeBreakRotation()
	{
		if (animator.CurrentAnimation == timeBreakKey)
			return; // Already in the looping animation

		rotationState = RotationStates.Looping;
		animator.Play(timeBreakKey);
		animator.SpeedScale = TimeBreakLoopTimeScale;
	}

	/// <summary> Transitions from quickly spinning to stationary. </summary>
	private void StopTimeBreakRotation() => animator.Play(enableKey);

	private void UpdateRotationTimer()
	{
		if (rotationState == RotationStates.Rotating)
			return; // Don't update times when rotating

		currentTime = Mathf.MoveToward(currentTime, staticTime, PhysicsManager.physicsDelta);
		if (!Mathf.IsEqualApprox(currentTime, staticTime))
			return; // Still waiting

		if (rotationState == RotationStates.Halfway)
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

		if (animationName == halfKey)
		{
			// Switch to halfway state
			rotationState = RotationStates.Halfway;
			return;
		}

		if (animationName != fullKey && animationName != enableKey) return;

		// Reset rotation to avoid incorrect transitions
		animator.Play(resetKey);
		animator.Advance(0.0);
		rotationState = RotationStates.Upright;
	}

	private void StartRotation()
	{
		rotationState = RotationStates.Rotating;
		animator.SpeedScale = 1.0f / rotationTime;
		animator.Play(pauseHalfway ? halfKey : fullKey);
	}

	/// <summary> Completes the spring's rotation. </summary>
	private void FinishRotation()
	{
		rotationState = RotationStates.Rotating;
		animator.SpeedScale = 1.0f / rotationTime;
		animator.Play(enableKey);
	}

	public void OnExited(Area3D _) => pauseTimebreakRotation = false;
}