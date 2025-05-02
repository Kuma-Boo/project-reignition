using Godot;
using Godot.Collections;

namespace Project.CustomNodes;

/// <summary>
/// Plays a group of GPUParticles together.
/// </summary>
[Tool]
public partial class GroupGpuParticles3D : GpuParticles3D
{
	[Export]
	public Array<GpuParticles3D> subSystems;
	public bool IsGroupEmitting { get; private set; }
	[ExportToolButton("Restart Group")]
	public Callable EditorRestartGroup => Callable.From(RestartGroup);
	[ExportToolButton("Stop Group")]
	public Callable EditorStopGroup => Callable.From(StopGroup);

	public new void SetEmitting(bool value)
	{
		for (int i = 0; i < subSystems.Count; i++)
			subSystems[i].Emitting = value;

		Emitting = value;
	}

	public new void SetSpeedScale(double value)
	{
		for (int i = 0; i < subSystems.Count; i++)
			subSystems[i].SpeedScale = value;

		SpeedScale = value;
	}

	public void RestartGroup()
	{
		for (int i = 0; i < subSystems.Count; i++)
			subSystems[i].Restart();

		Restart();
		IsGroupEmitting = true;
	}

	public void StopGroup() => SetEmitting(false);

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (!IsGroupEmitting) return;

		if (subSystems != null)
		{
			for (int i = 0; i < subSystems.Count; i++)
			{
				// Still active
				if (subSystems[i].Emitting) return;
			}
		}

		IsGroupEmitting = Emitting;
	}
}