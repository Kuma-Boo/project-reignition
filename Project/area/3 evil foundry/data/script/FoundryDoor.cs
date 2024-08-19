using Godot;

namespace Project.Gameplay.Objects;

[Tool]
public partial class FoundryDoor : Node3D
{
	[Export]
	private SpikeEnum SpikeState
	{
		get => spikeState;
		set
		{
			spikeState = value;
			UpdateState();
		}
	}
	private SpikeEnum spikeState;
	private enum SpikeEnum
	{
		Enabled,
		Disabled,
		Spikeless
	}
	[Export]
	private PivotEnum PivotPoint
	{
		get => pivotPoint;
		set
		{
			pivotPoint = value;
			UpdateState();
		}
	}
	private PivotEnum pivotPoint;
	private enum PivotEnum
	{
		Left,
		Right
	}
	[Export]
	private SwingModeEnum SwingMode
	{
		get => swingMode;
		set
		{
			swingMode = value;
			UpdateState();
		}
	}
	private SwingModeEnum swingMode;
	private enum SwingModeEnum
	{
		Open,
		Close,
		Fakeout
	}
	[Export(PropertyHint.Range, ".01f, 2f")]
	private float swingLength = .2f;

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "AnimationTree")]
	private NodePath animator;
	private AnimationTree Animator { get; set; }

	private bool IsActivated;

	private readonly StringName SpikeTransition = "parameters/spike_transition/transition_request";
	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName DoorSpeed = "parameters/state_speed/scale";

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			UpdateState();
			return;
		}

		StageSettings.instance.ConnectRespawnSignal(this);
		Respawn();
	}

	private void Respawn()
	{
		IsActivated = false;
		UpdateState();
	}

	/// <summary> Resets the door to its initial state. </summary>
	private void UpdateState()
	{
		Animator = GetNodeOrNull<AnimationTree>(animator);
		if (Animator == null) // No animator!
			return;

		Animator.Set(DoorSpeed, 0f); // Prevent door from swinging immediately

		Animator.Set(SpikeTransition, spikeState.ToString().ToLower());
		Animator.Set(StateTransition, $"{swingMode.ToString().ToLower()}_{pivotPoint.ToString().ToLower()}");
	}

	public void Activate(Area3D a)
	{
		GD.Print(a);
		if (!a.IsInGroup("player detection"))
			return;

		Activate();
	}
	public void Activate()
	{
		if (IsActivated) // Already activated
			return;

		IsActivated = true;
		Animator.Set(DoorSpeed, 1f / swingLength);
	}
}
