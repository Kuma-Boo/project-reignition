using Godot;
using Project.Core;

namespace Project.Gameplay.Bosses;

public partial class BossWaveAttack : PathFollow3D
{
	[Export] private AnimationPlayer animator;
	[Export] private Timer timer;

	private readonly float WaveSpeed = 50f;

	public override void _PhysicsProcess(double _)
	{
		Progress -= WaveSpeed * PhysicsManager.physicsDelta;
		if (!StageSettings.Player.IsMovingBackward) // Slow down slightly to give players more time to react
			Progress += StageSettings.Player.MoveSpeed * 0.5f * PhysicsManager.physicsDelta;

		if (!this.CastRay(GlobalPosition, Vector3.Down, Runtime.Instance.environmentMask))
			VOffset -= Runtime.Gravity * PhysicsManager.physicsDelta;
	}

	public void Activate(float progress)
	{
		VOffset = 0;
		Progress = progress;
		timer.Start();
		animator.Play("activate");
	}

	public void Deactivate()
	{
		timer.Stop();
		animator.Play("deactivate");
	}
}
