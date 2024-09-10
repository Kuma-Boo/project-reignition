#if TOOLS
using Godot;

namespace PathPlacer;

[Tool]
public partial class PathPlacerPlugin : EditorPlugin
{
	PathPlacerGizmoPlugin gizmo = new PathPlacerGizmoPlugin();
	PathPlacerInspectorPlugin inspector = new PathPlacerInspectorPlugin();
	public override void _EnterTree()
	{

		var script = GD.Load<Script>("res://addons/path_placer/PathPlacer.cs");
		var texture = GD.Load<Texture2D>("res://addons/path_placer/PathPlacer3D.svg");
		AddCustomType("PathPlacer", "Node3D", script, texture);
		AddNode3DGizmoPlugin(gizmo);
		AddInspectorPlugin(inspector);
	}

	public override void _ExitTree()
	{
		// Clean-up of the plugin goes here.
		RemoveCustomType("PathPlacer");
		RemoveNode3DGizmoPlugin(gizmo);
		RemoveInspectorPlugin(inspector);
	}
}
#endif
