using Godot;

namespace Project.Gameplay.Objects;

/// <summary>
/// Launches the player a variable amount, using <see cref="launchPower"/> as the blend of close and far settings
/// </summary>
[Tool]
public partial class Catapult : Node3D
{
	[ExportGroup("Launch Settings")]
	[Export]
	private float closeDistance;
	[Export]
	private float closeMidHeight;
	[Export]
	private float closeEndHeight;
	[Export]
	private float farDistance;
	[Export]
	private float farMidHeight;
	[Export]
	private float farEndHeight;

	private bool isProcessing;
	private CatapultState currentState;
	private enum CatapultState
	{
		Disabled,
		Enter,
		Control,
		Eject,
	}

	/// <summary> The strength of the shot, between 0 and 1. Exported for easier editing in the editor. </summary>
	[Export(PropertyHint.Range, "0, 1")]
	public float launchPower;
	private float targetLaunchPower;
	private float launchPowerVelocity;
	public LaunchSettings GetLaunchSettings()
	{
		float distance = Mathf.Lerp(closeDistance, farDistance, launchPower);
		float midHeight = Mathf.Lerp(closeMidHeight, farMidHeight, launchPower);
		float endHeight = Mathf.Lerp(closeEndHeight, farEndHeight, launchPower);
		Vector3 startPoint = GlobalPosition + (this.Up() * 3.5f);
		Vector3 endPoint = startPoint + (this.Forward() * distance) + (Vector3.Up * endHeight);

		LaunchSettings settings = LaunchSettings.Create(startPoint, endPoint, midHeight);
		settings.UseAutoAlign = true;
		return settings;
	}
	/// <summary> How much to change launchPower per-frame. </summary>
	private readonly float PowerAdjustmentSpeed = .14f; // How fast to adjust the power
	/// <summary> The strength of the shot, between 0 and 1. Exported for easier editing in the editor. </summary>
	private readonly float PowerAdjustmentSmoothing = .2f;

	[ExportGroup("Components")]
	[Export]
	private Node3D launchNode;
	[Export]
	private Node3D armNode;
	private Tween tweener;
	public CharacterController Character => CharacterController.instance;

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint() || !isProcessing)
			return;

		if (currentState == CatapultState.Eject) // Launch the player at the right time
		{
			if (armNode.Rotation.X > Mathf.Pi * .5f)
			{
				LaunchPlayer();
				return;
			}

			Character.UpdateExternalControl();
			return;
		}

		if (currentState == CatapultState.Control)
			ProcessControls();
	}

	private void ProcessControls()
	{
		// Check for state changes
		if (Input.IsActionJustPressed("button_jump"))
		{
			EjectPlayer(true);
			return;
		}

		if (Input.IsActionJustPressed("button_action"))
		{
			EjectPlayer(false);
			return;
		}

		// Update launch power
		targetLaunchPower += Character.InputVertical * PowerAdjustmentSpeed;
		targetLaunchPower = Mathf.Clamp(targetLaunchPower, 0, 1);
		launchPower = ExtensionMethods.SmoothDamp(launchPower, targetLaunchPower, ref launchPowerVelocity, PowerAdjustmentSmoothing);

		float targetRotation = Mathf.Lerp(Mathf.Pi * .25f, 0, launchPower);
		armNode.Rotation = Vector3.Right * targetRotation;

		Character.UpdateExternalControl();
	}

	private void OnEnteredCatapult()
	{
		currentState = CatapultState.Control;
		Character.StartExternal(this, launchNode);
		Character.Effect.StartSpinFX();
		Character.Animator.StartSpin(3.0f);
		launchPower = 1f;
		targetLaunchPower = 0.0f;
		launchPowerVelocity = 0f;

		tweener?.Kill();
	}

	private void EjectPlayer(bool isCancel)
	{
		currentState = CatapultState.Eject;
		tweener = CreateTween();

		if (isCancel)
		{
			Character.Effect.StopSpinFX();
			tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .2f * (1 - launchPower)).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
			tweener.TweenCallback(new Callable(this, MethodName.CancelCatapult));
		}
		else
		{
			tweener.TweenProperty(armNode, "rotation", Vector3.Right * Mathf.Pi, .25f * (launchPower + 1)).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .4f).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
		}

		tweener.TweenCallback(new Callable(this, MethodName.StopProcessing));
	}

	private void LaunchPlayer()
	{
		currentState = CatapultState.Disabled;
		Character.StartLauncher(GetLaunchSettings(), null);
	}

	private void CancelCatapult()
	{
		if (currentState != CatapultState.Eject) return;
		currentState = CatapultState.Disabled;

		// Have the player jump out backwards
		Vector3 destination = (this.Back().RemoveVertical() * 2f) + (Vector3.Down * 2f);
		destination += Character.GlobalPosition;

		var settings = LaunchSettings.Create(Character.GlobalPosition, destination, 1f);
		settings.IsJump = true;
		Character.StartLauncher(settings);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		StartProcessing();

		if (currentState != CatapultState.Disabled) return; // Already in the catapult
		currentState = CatapultState.Enter; // Start entering

		// Disable break skills
		Character.Skills.IsSpeedBreakEnabled = Character.Skills.IsTimeBreakEnabled = false;
		Character.Connect(CharacterController.SignalName.LaunchFinished, new Callable(this, MethodName.OnEnteredCatapult), (uint)ConnectFlags.OneShot);

		// Have the player jump into the catapult
		var settings = LaunchSettings.Create(Character.GlobalPosition, launchNode.GlobalPosition, 2f);
		settings.IsJump = true;
		Character.StartLauncher(settings);
	}

	private void StartProcessing() => isProcessing = true;
	private void StopProcessing() => isProcessing = false;
}
