using Godot;

namespace Project.Gameplay.Objects;

public partial class Switch : Area3D
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();
	[Signal]
	public delegate void RespawnedEventHandler();

	/// <summary> How long should activation last? </summary>
	[Export(PropertyHint.Range, "0,10,.1")]
	private float activationLength;
	/// <summary> Should the switch start enabled? </summary>
	[Export]
	private bool startActive;
	/// <summary> Should the switch be treated as a toggle? </summary>
	[Export]
	private bool toggleMode;
	/// <summary> Used to record whether the switch's state has already been changed. </summary>
	private bool wasToggled;
	/// <summary> Is the switch currently turned on? </summary>
	private bool isActive;

	[ExportGroup("Components")]
	[Export]
	private AnimationPlayer animator;
	[Export]
	private Timer timer;

	public override void _Ready()
	{
		StageSettings.Instance.ConnectRespawnSignal(this);
		Respawn();
	}

	public void Respawn()
	{
		wasToggled = false;
		isActive = startActive;
		animator.Play(isActive ? "activate-loop" : "RESET");

		EmitSignal(SignalName.Respawned);
	}

	private void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		Activate();
	}

	private void Activate()
	{
		// Check if switch was already toggled.
		if (!toggleMode && wasToggled) return;

		ToggleSwitch();

		if (!wasToggled) // Deactivated; Disable any timers and return early
		{
			timer.Stop();
			return;
		}

		if (!Mathf.IsZeroApprox(activationLength)) // Start timer
		{
			timer.WaitTime = activationLength;
			timer.Start();
		}
	}

	/// <summary> Just toggle the switch, without any checks and timers. </summary>
	private void ToggleSwitch()
	{
		wasToggled = !wasToggled;
		isActive = !isActive;
		animator.Play(isActive ? "activate" : "deactivate");
		EmitSignal(isActive ? SignalName.Activated : SignalName.Deactivated);
	}
}
