using Godot;
using Project.Gameplay;

namespace Project.Editor
{
	public class GrindRailInspector : EditorInspectorPlugin
	{
		public Plugin plugin;
		private GrindRail target;

		public override bool CanHandle(Object o)
		{
			if (o is GrindRail)
			{
				target = o as GrindRail;
				return true;
			}

			target = null;
			return false;
		}

		public override void ParseBegin(Object obj)
		{
			Button b = new Button();
			b.Text = "Rebuild";
			b.Connect("pressed", this, nameof(Rebuild));
			AddCustomControl(b);
		}

		private void Rebuild()
		{
			if (!plugin.IsEditable(target))
			{
				GD.PrintErr("Please make the target grind rail editable and save the scene.");
				return;
			}

			target.Rebuild();
		}
	}
}
