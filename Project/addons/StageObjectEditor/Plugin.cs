using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Editor
{
	[Tool]
	public class Plugin : EditorPlugin
	{
		public Plugin plugin;
		public GrindRailInspector GrindInspector { get; private set; }

		private Spatial target;
		private Camera editorCam;

		public override void _EnterTree()
		{
			GrindInspector = new GrindRailInspector();
			GrindInspector.plugin = this;
			AddInspectorPlugin(GrindInspector);
		}

		public override void _ExitTree()
		{
			RemoveInspectorPlugin(GrindInspector);
		}

		public override bool Handles(Object obj)
		{
			return obj is Launcher || obj is DriftCorner;
		}
		public override void Edit(Object obj) => target = obj as Spatial;

		public override bool ForwardSpatialGuiInput(Camera cam, InputEvent e)
		{
			editorCam = cam;
			UpdateOverlays();
			return base.ForwardSpatialGuiInput(cam, e);
		}

		private const int PREVIEW_RESOLUTION = 15;

		public override void ForwardSpatialDrawOverViewport(Control overlay)
		{
			if (editorCam == null || !target.Visible) return;

			if (target is Launcher)
			{
				Array<Vector2> points = new Array<Vector2>();

				for (int i = 0; i < PREVIEW_RESOLUTION; i++)
				{
					Vector3 point = (target as Launcher).CalculatePosition(i / (float)PREVIEW_RESOLUTION);
					if (!editorCam.IsPositionBehind(point))
						points.Add(editorCam.UnprojectPosition(point));
				}

				Vector2[] pointsList = new Vector2[points.Count];
				points.CopyTo(pointsList, 0);
				overlay.DrawPolyline(pointsList, Colors.Blue, 1, true);
			}
			else if (target is DriftCorner)
			{
				if (editorCam.IsPositionBehind((target as DriftCorner).GlobalTransform.origin) ||
				editorCam.IsPositionBehind((target as DriftCorner).MiddlePosition))
					return;

				Vector2 start = editorCam.UnprojectPosition((target as DriftCorner).GlobalTransform.origin);
				Vector2 middle = editorCam.UnprojectPosition((target as DriftCorner).MiddlePosition);
				overlay.DrawLine(start, middle, Colors.Blue, 1, true);

				if (editorCam.IsPositionBehind((target as DriftCorner).EndPosition)) return;

				Vector2 end = editorCam.UnprojectPosition((target as DriftCorner).EndPosition);
				overlay.DrawLine(middle, end, Colors.Blue, 1, true);
			}
		}

		public bool IsEditable(Node node)
		{
			Node root = node.GetTree().EditedSceneRoot;
			if (root == null)
				return false;
			string rootPath = root.Filename;
			if (rootPath.Empty())
				return false;

			PackedScene rootScene = ResourceLoader.Load<PackedScene>(rootPath);
			Dictionary state = rootScene._Bundled;

			Array array = state["editable_instances"] as Array;
			return array.Contains(root.GetPathTo(node));
		}
	}
}
