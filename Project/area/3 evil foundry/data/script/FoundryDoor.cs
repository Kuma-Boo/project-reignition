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
		Fakeout,
		Flip,
	}
	[Export(PropertyHint.Range, ".1,2,.1")] private float swingLength;
	[Export]
	private bool SwingInReverse
	{
		get => swingInReverse;
		set
		{
			swingInReverse = value;
			UpdateState();
		}
	}
	private bool swingInReverse;

	[ExportGroup("Components")]
	[Export(PropertyHint.NodePathValidTypes, "AnimationTree")] private NodePath animator;
	private AnimationTree Animator { get; set; }
	[Export] private NodePath hazard;
	private Hazard Hazard { get; set; }

	[ExportGroup("Animation Properties")]
	/// <summary> Animated via the AnimationPlayer. </summary>
	[Export] private SpikeEnum SpeedBreakDamageMode { get; set; }

	private bool isActivated;
	private bool isForceClosedActivated;
	private bool isInteractingWithPlayer;

	private readonly StringName SpikeTransition = "parameters/spike_transition/transition_request";
	private readonly StringName StateTransition = "parameters/state_transition/transition_request";
	private readonly StringName StateSeek = "parameters/state_seek/seek_request";
	private readonly StringName DoorSpeed = "parameters/state_speed/scale";

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			UpdateState();
			return;
		}

		StageSettings.Instance.Respawned += Respawn;
		Respawn();
	}

	public override void _PhysicsProcess(double _)
	{
		if (isForceClosedActivated || !isInteractingWithPlayer || !StageSettings.Player.Skills.IsSpeedBreakActive) return;

		ForceClose();
	}

	private void ForceClose()
	{
		if (SpeedBreakDamageMode != SpikeEnum.Spikeless && SpeedBreakDamageMode == SpikeState)
		{
			StageSettings.Player.Skills.ToggleSpeedBreak();
			StageSettings.Player.StartKnockback(new()
			{
				ignoreInvincibility = true,
				disableDamage = true
			});
		}

		isForceClosedActivated = true;
		StageSettings.Player.Camera.StartCameraShake(new()
		{
			origin = GlobalPosition,
			magnitude = Vector3.One.RemoveDepth(),
		});

		Hazard.isDisabled = true;
		StageSettings.Player.MoveSpeed = 0;

		if (swingMode == SwingModeEnum.Fakeout) // Flip fakeout doors when closing
			Animator.Set(SpikeTransition, spikeState == SpikeEnum.Enabled ? "disabled" : "enabled");

		Animator.Set(StateTransition, $"close_{pivotPoint.ToString().ToLower()}");
		Animator.Set(DoorSpeed, 10f);
	}

	private void Respawn()
	{
		isActivated = false;
		isForceClosedActivated = false;
		UpdateState();
	}

	/// <summary> Resets the door to its initial state. </summary>
	private void UpdateState()
	{
		Animator = GetNodeOrNull<AnimationTree>(animator);
		Hazard = GetNodeOrNull<Hazard>(hazard);
		if (Animator == null) // No animator!
			return;

		Animator.Set(SpikeTransition, spikeState.ToString().ToLower());
		Animator.Set(StateTransition, $"{swingMode.ToString().ToLower()}_{pivotPoint.ToString().ToLower()}");

		Animator.Set(StateSeek, swingInReverse ? 1f : 0f);
		Animator.Set(DoorSpeed, 0f); // Prevent door from swinging immediately
	}

	public void Activate(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		Activate();
	}

	public void Activate()
	{
		if (isActivated) // Already activated
			return;

		isActivated = true;
		float animationSpeed = 1f / swingLength;
		if (swingInReverse)
			animationSpeed *= -1;
		Animator.Set(DoorSpeed, animationSpeed);
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		isInteractingWithPlayer = true;
	}

	private void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player")) return;
		isInteractingWithPlayer = false;
	}
}
