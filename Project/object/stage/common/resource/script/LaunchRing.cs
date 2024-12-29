using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects;

[Tool]
public partial class LaunchRing : Launcher
{
	[Signal]
	public delegate void EnteredEventHandler();
	[Signal]
	public delegate void ExitedEventHandler();
	[Signal]
	public delegate void DamageEventHandler();

	/// <summary> How long the launch ring should take to wind up. </summary>
	[Export(PropertyHint.Range, "0.1,1,.1,or_greater")] private float windupTime = 1f;

	[ExportGroup("Editor")]
	[Export]
	private Array<NodePath> pieces;
	private readonly Array<Node3D> _pieces = [];
	private readonly int PieceCount = 16;
	private readonly float RingSize = 2.2f;
	/// <summary> Is this the spike variant? </summary>
	[Export] private bool isSpikeVariant;
	[Export] private AnimationPlayer animator;

	public float LaunchRatio => launchRatio;
	public override float GetLaunchRatio() => isSpikeVariant ? 1f : Mathf.SmoothStep(0, 1, launchRatio);

	protected override void SetUp()
	{
		if (Engine.IsEditorHint())
			return;

		base.SetUp();
		InitializePieces();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint() && pieces.Count != _pieces.Count)
			InitializePieces();

		UpdatePieces();
	}

	protected override void LaunchAnimation() => Player.Animator.SetSpinSpeed(5); // Keep spinning, but do it faster

	private void InitializePieces()
	{
		for (int i = 0; i < pieces.Count; i++)
			_pieces.Add(GetNode<Node3D>(pieces[i]));
	}

	private void UpdatePieces()
	{
		if (_pieces.Count == 0) return;

		float interval = Mathf.Tau / PieceCount;
		for (int i = 0; i < _pieces.Count; i++)
		{
			if (_pieces[i] == null) continue;

			Vector3 movementVector = -Vector3.Up.Rotated(Vector3.Forward, interval * (i + .5f)); // Offset rotation slightly, since visual model is offset
			_pieces[i].Position = movementVector * launchRatio * RingSize;
		}
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		animator.Play("charge", -1, 1f / windupTime);
		IsPlayerCentered = false;
		Player.StartLaunchRing(this);
		EmitSignal(SignalName.Entered);
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		animator.Play("RESET", .2 * (1 + launchRatio));

		if (!Player.IsLaunching)
			EmitSignal(SignalName.Exited);
	}

	/// <summary> Called from an AnimationPlayer. </summary>
	private void DamagePlayer() => EmitSignal(SignalName.Damage);
}