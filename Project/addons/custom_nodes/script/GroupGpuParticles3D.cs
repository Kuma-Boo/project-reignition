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
	[Export]
	private bool EditorRestartGroup { get; set; }

	public void SetEmitting(bool value)
	{
		for (int i = 0; i < subSystems.Count; i++)
			subSystems[i].Emitting = value;

		Emitting = value;
	}

	public void RestartGroup()
	{
		for (int i = 0; i < subSystems.Count; i++)
			subSystems[i].Restart();

		Restart();
		IsGroupEmitting = true;
		EditorRestartGroup = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (EditorRestartGroup)
			RestartGroup();

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