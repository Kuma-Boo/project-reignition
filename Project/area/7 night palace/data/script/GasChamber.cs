using Godot;
using Project.Gameplay.Triggers;
using System;

namespace Project.Gameplay.Hazards;

public partial class GasChamber : StageTriggerModule
{
	[Export] private float damageInterval = 1.0f;
	private Timer timer;

	public override void _Ready()
	{
		// Setup timer
		timer = new Timer()
		{
			OneShot = false,
			Autostart = false,
			WaitTime = damageInterval,
		};

		AddChild(timer);
		timer.Timeout += TakeDamage;

		base._Ready();
	}

	private void TakeDamage()
	{
		if (StageSettings.Instance.CurrentRingCount == 0)
		{
			StageSettings.Player.StartKnockback();
			return;
		}

		// Remove a ring
		StageSettings.Instance.UpdateRingCount(1, StageSettings.MathModeEnum.Subtract, true);
	}

	public override void Respawn() => Deactivate();
	public override void Activate() => timer.Start();
	public override void Deactivate() => timer.Stop();
}
