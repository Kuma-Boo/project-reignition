#if TOOLS
using Godot;

[Tool]
public partial class Plugin : EditorPlugin
{
	public override void _EnterTree()
	{
		var script = GD.Load<Script>("res://addons/object_generator/ObjectGenerator.cs");
		var texture = GD.Load<Texture2D>("res://addons/object_generator/icon.png");
		AddCustomType("ObjectGenerator", "Node3D", script, texture);
	}

	public override void _ExitTree()
	{
		// Clean-up of the plugin goes here.
		// Always remember to remove it from the engine when deactivated.
		RemoveCustomType("ObjectGenerator");
	}
}
#endif