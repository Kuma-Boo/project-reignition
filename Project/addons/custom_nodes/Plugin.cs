using Godot;

namespace Project.CustomNodes
{
#if TOOLS
	[Tool]
	public partial class Plugin : EditorPlugin
	{
		private ObjectGeneratorInspectorPlugin _generatorInspector;
		public override void _EnterTree()
		{
			var script = GD.Load<Script>("res://addons/custom_nodes/script/ObjectGenerator.cs");
			var texture = GD.Load<Texture2D>("res://addons/custom_nodes/icon/object generator.svg");
			AddCustomType("ObjectGenerator", "Node3D", script, texture);
			_generatorInspector = new ObjectGeneratorInspectorPlugin();
			AddInspectorPlugin(_generatorInspector);

			script = GD.Load<Script>("res://addons/custom_nodes/script/Sprite2DPlus.cs");
			texture = GD.Load<Texture2D>("res://addons/custom_nodes/icon/sprite 2d plus.svg");
			AddCustomType("Sprite2DPlus", "Sprite2D", script, texture);

			script = GD.Load<Script>("res://addons/custom_nodes/script/GroupGpuParticles3D.cs");
			texture = GD.Load<Texture2D>("res://addons/custom_nodes/icon/group gpu particle 3d.svg");
			AddCustomType("GroupGPUParticles3D", "GpuParticles3D", script, texture);
		}

		public override void _ExitTree()
		{
			// Clean-up of the plugin goes here.
			// Always remember to remove it from the engine when deactivated.
			RemoveCustomType("ObjectGenerator");
			RemoveInspectorPlugin(_generatorInspector);
			RemoveCustomType("Sprite2DPlus");
			RemoveCustomType("GroupGPUParticles3D");
		}
	}
#endif
}
