using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Editor
{
	[Tool]
	public class Plugin : EditorPlugin
	{
		public Plugin plugin;

		private Spatial target;
		private Camera editorCam;

		public override bool Handles(Object obj)
		{
			return obj is DriftTrigger || obj is Majin || obj is Launcher;
		}
		public override void Edit(Object obj) => target = obj as Spatial;

		public override bool ForwardSpatialGuiInput(Camera cam, InputEvent e)
		{
			editorCam = cam;
			UpdateOverlays();
			return base.ForwardSpatialGuiInput(cam, e);
		}

		private const int PREVIEW_RESOLUTION = 32;

		public override void ForwardSpatialDrawOverViewport(Control overlay)
		{
			if (editorCam == null || !target.Visible) return;

			if (target is Launcher)
				UpdateLauncher(overlay);
			else if (target is DriftTrigger)
				UpdateDriftCorner(overlay);
			else if (target is Majin)
				UpdateMajinPath(overlay);
		}

		private void UpdateLauncher(Control overlay)
		{
			Array<Vector2> points = new Array<Vector2>();
			Launcher launcher = (target as Launcher);

			for (int i = 0; i < PREVIEW_RESOLUTION; i++)
			{
				float simulationTime = (i / (float)PREVIEW_RESOLUTION) * launcher.TotalTravelTime;
				Vector3 position = launcher.InterpolatePosition(simulationTime);
				if (!editorCam.IsPositionBehind(position))
					points.Add(editorCam.UnprojectPosition(position));
			}

			Vector2[] pointsList = new Vector2[points.Count];
			points.CopyTo(pointsList, 0);
			overlay.DrawPolyline(pointsList, Colors.Blue, 1, true);
		}

		private void UpdateDriftCorner(Control overlay)
		{
			if (editorCam.IsPositionBehind((target as DriftTrigger).GlobalTransform.origin) ||
			editorCam.IsPositionBehind((target as DriftTrigger).MiddlePosition))
				return;

			Vector2 start = editorCam.UnprojectPosition((target as DriftTrigger).GlobalTransform.origin);
			Vector2 middle = editorCam.UnprojectPosition((target as DriftTrigger).MiddlePosition);
			overlay.DrawLine(start, middle, Colors.Blue, 1, true);

			if (editorCam.IsPositionBehind((target as DriftTrigger).EndPosition)) return;

			Vector2 end = editorCam.UnprojectPosition((target as DriftTrigger).EndPosition);
			overlay.DrawLine(middle, end, Colors.Blue, 1, true);
		}

		private void UpdateMajinPath(Control overlay)
		{
			Majin t = target as Majin;
			if (t.spawnOffset == Vector3.Zero) return;

			Vector3 s = t.GlobalTransform.origin;
			Vector3 e = s + t.spawnOffset;
			if (editorCam.IsPositionBehind(s) ||
			editorCam.IsPositionBehind(e))
				return;

			overlay.DrawLine(editorCam.UnprojectPosition(s), editorCam.UnprojectPosition(e), Colors.Blue, 1, true);
		}
	}
}
