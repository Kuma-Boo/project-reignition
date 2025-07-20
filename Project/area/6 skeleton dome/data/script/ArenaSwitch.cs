using Godot;
using System;

namespace Project.Gameplay;

/// <summary> Handles the switches in Skeleton Dome's Arena. </summary>
public partial class ArenaSwitch : Node3D
{
	// Using a static variable because I'm too lazy to do it properly
	public static int CurrentSwitchIndex;

	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void RespawnedEventHandler();

	[Export] private AnimationPlayer animator;
	[Export(PropertyHint.Range, "1, 5")] private int switchIndex = 1;
	private bool isActivated;

	public override void _Ready()
	{
		InitializeSwitches();
		StageSettings.Instance.Respawned += ResetSwitch;
	}

	/// <summary> Resets the current switch index. Only performed by switch #1. </summary>
	private void InitializeSwitches()
	{
		if (switchIndex != 1)
			return;

		// Reset progress
		CurrentSwitchIndex = 1;
	}

	private void ResetSwitch()
	{
		isActivated = false;
		animator.Play("RESET");
		EmitSignal(SignalName.Respawned);
	}

	private void ActivateSwitch()
	{
		isActivated = true;
		CurrentSwitchIndex++;
		animator.Play("activated");
		EmitSignal(SignalName.Activated);
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		if (isActivated || CurrentSwitchIndex != switchIndex)
			return;

		ActivateSwitch();
	}
}
