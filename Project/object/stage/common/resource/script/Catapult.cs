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

	[ExportGroup("Components")]
	[Export]
	private Node3D playerPositionNode;
	public Node3D PlayerPositionNode => playerPositionNode;
	[Export]
	private Node3D armNode;
	[Export]
	private AudioStreamPlayer3D enterSFX;
	[Export]
	private AudioStreamPlayer3D aimSFX;

	private Tween tweener;
	public float LaunchRatio
	{
		get => launchRatio;
		set => launchRatio = value;
	}

	public bool IsAtLaunchPoint => armNode.Rotation.X < Mathf.Pi * .5f;

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
			UpdateArmRotation();
			return;
		}

		if (aimSFX.Playing && !Player.IsCatapultActive)
			SoundManager.FadeAudioPlayer(aimSFX);
	}

	public override void Activate()
	{
		launchRatio = Mathf.SmoothStep(0, 1, launchRatio); // Cheat launch power slightly towards extremes
		base.Activate();
	}

	public void PlayEnterSfx() => enterSFX.Play();
	protected override void PlayLaunchSfx()
	{
		if (IsSfxActive)
			return;

		base.PlayLaunchSfx();
	}

	public void UpdateArmRotation(float targetLaunchPower = 0)
	{
		aimSFX.VolumeDb = Mathf.LinearToDb(Mathf.Abs(LaunchRatio - targetLaunchPower) / .1f);
		if (!aimSFX.Playing)
			aimSFX.Play();
		float targetRotation = Mathf.Lerp(Mathf.Pi * .25f, 0, launchRatio);
		armNode.Rotation = Vector3.Right * targetRotation;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		if (Player.IsCatapultActive || Player.IsLaunching)
			return; // Already in the catapult

		Player.LaunchFinished += EnterCatapult;

		// Have the player jump into the catapult
		var settings = LaunchSettings.Create(Player.GlobalPosition, playerPositionNode.GlobalPosition, 2f);
		settings.IsJump = true;
		Player.StartLauncher(settings);
		EmitSignal(SignalName.PlayerEntered);
	}

	private void EnterCatapult()
	{
		tweener?.Kill();
		Player.StartCatapult(this);
		Player.LaunchFinished -= EnterCatapult;
	}

	private void EjectPlayer()
	{
		// Have the player jump out backwards
		Vector3 destination = (this.Back().RemoveVertical() * 2f) + (Vector3.Down * 2f);
		destination += Player.GlobalPosition;

		var settings = LaunchSettings.Create(Player.GlobalPosition, destination, 1f);
		settings.IsJump = true;
		Player.StartLauncher(settings);
		Player.MovementAngle = Player.PathFollower.ForwardAngle;
		Player.Animator.SnapRotation(Player.MovementAngle); // Reset visual rotation
		PlayEnterSfx();
		EmitSignal(SignalName.PlayerExited);
	}

	public void TweenStep() => tweener.CustomStep(PhysicsManager.physicsDelta);
	public void UnpauseTween() => tweener.Play();

	public void CancelTween()
	{
		tweener = CreateTween();
		tweener.SetProcessMode(Tween.TweenProcessMode.Physics);
		tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .2f * (1 - LaunchRatio)).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
		tweener.TweenCallback(new Callable(this, MethodName.EjectPlayer));
	}

	public void LaunchTween()
	{
		tweener = CreateTween();
		tweener.SetProcessMode(Tween.TweenProcessMode.Physics);
		PlayLaunchSfx(); // Play launching sfx
		tweener.TweenProperty(armNode, "rotation", Vector3.Right * Mathf.Pi, .25f * (LaunchRatio + 1)).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tweener.TweenProperty(armNode, "rotation", Vector3.Zero, .4f).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
		tweener.Pause();
	}
}
