using Godot;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Sword : Node3D
{
	[Signal] public delegate void ActivatedEventHandler();

	[Export] private Vector3 endOffset;
	[Export] private float launchHeight;
	[ExportSubgroup("Components")]
	[Export] private Node3D launchPoint;
	[Export] private AnimationPlayer animator;

	private PlayerController Player => StageSettings.Player;
	private Vector3 StartPoint => launchPoint == null ? GlobalPosition : launchPoint.GlobalPosition;
	private Vector3 EndPoint => StartPoint + GlobalBasis.Rotated(GlobalBasis.Y, Mathf.Pi * 0.5f) * endOffset;

	public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(StartPoint, EndPoint, launchHeight);

	private void Activate()
	{
		animator.Play("bounce");
		LaunchSettings settings = GetLaunchSettings();
		settings.AllowJumpDash = true;

		Player.StartLauncher(settings);
		Player.Animator.ResetState(.1f);
		Player.Animator.StartSpin(5f);
		Player.Effect.StartSpinFX();
		Player.Effect.StartTrailFX();
		EmitSignal(SignalName.Activated);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		// Player must either perform a homing attack or stomp onto the sword
		if (!Player.IsJumpDashOrHomingAttack && !Player.IsStomping)
			return;

		Activate();
	}
}
