using Godot;
using Project.Core;

namespace Project;

/// <summary> Handles whether light shafts are enabled or not. </summary>
public partial class LightShaftEnabler : CanvasLayer
{
	public override void _Ready()
	{
		if (!SaveManager.Config.useVolumetricLighting)
			QueueFree();
	}
}
