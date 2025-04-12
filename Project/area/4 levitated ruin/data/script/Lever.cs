using Godot;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Lever : Node3D
{
	[Signal] public delegate void StartedEventHandler();
	[Signal] public delegate void ActivatedEventHandler();

	[Export] public bool IsRightLever { get; private set; }
	[Export(PropertyHint.NodeType, "EventTrigger")] private NodePath root;
	private EventTrigger _root;
	public Vector3 TargetStandingPosition => GlobalPosition + this.Forward() * (IsRightLever ? -0.25f : 0.25f);

	[ExportToolButton("Update Lever")]
	public Callable EditorUpdateLever => Callable.From(UpdateLever);

	private PlayerController Player => StageSettings.Player;

	public override void _Ready() => UpdateLever();

	private void UpdateLever()
	{
		_root = GetNodeOrNull<EventTrigger>(root);
		if (!IsInstanceValid(_root))
		{
			GD.PrintErr("Root node is not set.");
			return;
		}

		_root.Rotation = Vector3.Up * (IsRightLever ? 0f : Mathf.Pi);
	}

	public void StartLeverTurn() => _root.Activate();

	public void Activate() => EmitSignal(SignalName.Activated);

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		// Connect signal
		Callable startLeverCallable = Callable.From(() => Player.StartLever(this));
		Player.Connect(PlayerController.SignalName.LaunchFinished, startLeverCallable, (int)ConnectFlags.OneShot + (int)ConnectFlags.Deferred);

		// Jump to the correct spot for animations
		LaunchSettings settings = LaunchSettings.Create(Player.GlobalPosition, TargetStandingPosition, 2f, true);
		settings.IgnoreCollisions = true;
		settings.IsJump = true;
		settings.UseAutoAlign = true;
		Player.StartLauncher(settings);

		EmitSignal(SignalName.Started);
	}
}
