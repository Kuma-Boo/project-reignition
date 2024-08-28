using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary>
/// Launches the player a variable amount, using <see cref="launchPower"/> as the blend of close and far settings
/// </summary>
[Tool]
public partial class Catapult : Launcher
{
	[Signal]
	public delegate void PlayerEnteredEventHandler();
	[Signal]
	public delegate void PlayerExitedEventHandler();

	private bool isProcessing;
	private CatapultState currentState;
	private enum CatapultState
	{
		Disabled,
		Enter,
		Control,
		Launch,
	}

	private float targetLaunchPower;
	private float launchPowerVelocity;
	/// <summary> How much to change launchPower per-frame. </summary>
	private readonly float PowerAdjustmentSpeed = .14f; // How fast to adjust the power
	/// <summary> The strength of the shot, between 0 and 1. Exported for easier editing in the editor. </summary>
	private readonly float PowerAdjustmentSmoothing = .2f;

	[ExportGroup("Components")]
	[Export]
	private Node3D playerPositionNode;
	[Export]
	private Node3D armNode;
	[Export]
	private AudioStreamPlayer3D enterSFX;
	[Export]
	private AudioStreamPlayer3D aimSFX;
	private Tween tweener;

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
			UpdateArmRotation();
			return;
		}

		if (aimSFX.Playing) // REFACTOR TODO && Character.ExternalController != this)
			SoundManager.FadeAudioPlayer(aimSFX);

		switch (currentState)
		{
			case CatapultState.Launch: // Launch the player at the right time
				ProcessLaunch();
				break;
			case CatapultState.Control:
				ProcessControls();
				break;
		}
	}

	private void ProcessLaunch()
	{
		tweener.CustomStep(PhysicsManager.physicsDelta);
		/* REFACTOR TODO
		if (Player.ExternalController != this)
			return;

		Player.UpdateExternalControl();
		*/
		if (armNode.Rotation.X < Mathf.Pi * .5f)
			return;

		Activate();
	}

	private void ProcessControls()
	{
		// Check for state changes
		if (Input.IsActionJustPressed("button_jump"))
		{
			LaunchPlayer(true);
			return;
		}

		if (Input.IsActionJustPressed("button_action"))
		{
			LaunchPlayer(false);
			return;
		}

		// Update launch power
		targetLaunchPower += Player.Controller.InputVertical * PowerAdjustmentSpeed;
		targetLaunchPower = Mathf.Clamp(targetLaunchPower, 0, 1);
		launchRatio = ExtensionMethods.SmoothDamp(launchRatio, targetLaunchPower, ref launchPowerVelocity, PowerAdjustmentSmoothing);

		aimSFX.VolumeDb = Mathf.LinearToDb(Mathf.Abs(launchRatio - targetLaunchPower) / .1f);
		if (!aimSFX.Playing)
			aimSFX.Play();

		UpdateArmRotation();
		/*
		REFACTOR TODO
		Player.UpdateExternalControl();
		*/
	}

	private void UpdateArmRotation()
	{
		float targetRotation = Mathf.Lerp(Mathf.Pi * .25f, 0, launchRatio);
		armNode.Rotation = Vector3.Right * targetRotation;
	}

	private void OnEnteredCatapult()
	{
		currentState = CatapultState.Control;

		Player.Effect.StartSpinFX();
		/* REFACTOR TODO
		Player.StartExternal(this, playerPositionNode);
		Player.Animator.StartSpin(3f);
		Player.Animator.SnapRotation(0);
		*/

		launchRatio = 1f;
		targetLaunchPower = 0f;
		launchPowerVelocity = 0f;

		tweener?.Kill();
		enterSFX.Play();
	}

	private void LaunchPlayer(bool isCancel)
	{
		currentState = CatapultState.Launch;

		tweener = CreateTween();
		tweener.SetProcessMode(Tween.TweenProcessMode.Physics);

		if (isCancel)
		{
			// REFACTOR TODO
			// Player.Effect.StopSpinFX();
			tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .2f * (1 - launchRatio)).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
			tweener.TweenCallback(new Callable(this, MethodName.CancelCatapult));
		}
		else
		{
			PlayLaunchSfx(); // Play launching sfx
			tweener.TweenProperty(armNode, "rotation", Vector3.Right * Mathf.Pi, .25f * (launchRatio + 1)).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .4f).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
			tweener.Pause();
		}

		tweener.TweenCallback(new Callable(this, MethodName.StopProcessing));
	}

	public override void Activate()
	{
		// Cheat launch power slightly towards extremes
		launchRatio = Mathf.SmoothStep(0, 1, launchRatio);
		base.Activate();

		// REFACTOR TODO
		// Player.Animator.IsFallTransitionEnabled = false;
	}

	private void CancelCatapult()
	{
		if (currentState != CatapultState.Launch)
			return;

		currentState = CatapultState.Disabled;

		// Have the player jump out backwards
		Vector3 destination = (this.Back().RemoveVertical() * 2f) + (Vector3.Down * 2f);
		destination += Player.GlobalPosition;

		var settings = LaunchSettings.Create(Player.GlobalPosition, destination, 1f);
		settings.IsJump = true;
		Player.StartLauncher(settings);
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		// REFACTOR TODO
		// Player.Animator.SnapRotation(Player.MovementAngle); // Reset visual rotation
		enterSFX.Play();
		EmitSignal(SignalName.PlayerExited);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		if (currentState != CatapultState.Disabled)
			return; // Already in the catapult

		StartProcessing();
		currentState = CatapultState.Enter; // Start entering

		// Disable break skills
		Player.Skills.IsSpeedBreakEnabled = Player.Skills.IsTimeBreakEnabled = false;
		Player.Connect(CharacterController.SignalName.LaunchFinished, new Callable(this, MethodName.OnEnteredCatapult), (uint)ConnectFlags.OneShot);

		// Have the player jump into the catapult
		var settings = LaunchSettings.Create(Player.GlobalPosition, playerPositionNode.GlobalPosition, 2f);
		settings.IsJump = true;
		Player.StartLauncher(settings);
		EmitSignal(SignalName.PlayerEntered);
	}

	protected override void PlayLaunchSfx()
	{
		if (IsSfxActive)
			return;

		base.PlayLaunchSfx();
	}

	private void StartProcessing() => isProcessing = true;
	private void StopProcessing()
	{
		isProcessing = false;
		currentState = CatapultState.Disabled;
	}
}
