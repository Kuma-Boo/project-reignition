using Godot;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class CatapultState : PlayerState
{
	public Catapult Catapult { get; set; }

	private State currentState;
	private enum State
	{
		Control,
		Launch,
	}

	private float launchPower;
	private float launchPowerVelocity;
	/// <summary> How much to smooth launchPower changes. </summary>
	private readonly float PowerAdjustmentSmoothing = .15f;

	private readonly StringName LaunchAction = "action_launch";
	private readonly StringName ExitAction = "action_exit";

	public override void EnterState()
	{
		launchPower = 1f;
		launchPowerVelocity = 0f;
		currentState = State.Control;

		// Disable speedbreak skills
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Effect.StartSpinFX();
		Player.StartExternal(this, Catapult.PlayerPositionNode);
		Player.Animator.StartSpin(3f);
		Player.Animator.SnapRotation(0);

		Catapult.LaunchRatio = 1f;
		Catapult.PlayEnterSfx();

		HeadsUpDisplay.Instance.SetPrompt(LaunchAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(ExitAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		Player.StopExternal();
		Player.Skills.IsSpeedBreakEnabled = true;
		Player.Animator.IsFallTransitionEnabled = false;

		HeadsUpDisplay.Instance.HidePrompts();

		Catapult = null;
	}

	public override PlayerState ProcessPhysics()
	{
		switch (currentState)
		{
			case State.Launch: // Launch the player at the right time
				ProcessLaunch();
				break;
			case State.Control:
				ProcessControls();
				break;
		}

		return null;
	}

	private void ProcessLaunch()
	{
		if (Catapult == null) // State must have been exited early
			return;

		Catapult.TweenStep();
		Player.UpdateExternalControl();
		if (Catapult.IsAtLaunchPoint)
			return;

		Catapult.UnpauseTween();
		Catapult.Activate();
	}

	private void ProcessControls()
	{
		// Check for state changes
		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			currentState = State.Launch;
			Player.Effect.StopSpinFX();
			Catapult.CancelTween();
			return;
		}

		if (Player.Controller.IsActionBufferActive)
		{
			Player.Controller.ResetActionBuffer();
			currentState = State.Launch;
			Catapult.LaunchTween();
			return;
		}

		// Update launch power
		launchPower = (Player.Controller.InputVertical + 1f) * .5f; // Convert to [0 - 1] range
		launchPower = Mathf.Clamp(launchPower, 0, 1);
		Catapult.LaunchRatio = ExtensionMethods.SmoothDamp(Catapult.LaunchRatio, launchPower, ref launchPowerVelocity, PowerAdjustmentSmoothing);
		Catapult.UpdateArmRotation(launchPower);
		Player.UpdateExternalControl();
	}
}
