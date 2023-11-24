using Godot;
using Godot.Collections;
using Project.Gameplay;
using Project.Gameplay.Triggers;
using Project.Gameplay.Objects;

namespace Project.Editor.StageObjectPreviewer
{
#if TOOLS
	[Tool]
	public partial class Plugin : EditorPlugin
	{
		public Plugin plugin;

		private Node target;
		public static Camera3D editorCam;

		public override bool _Handles(GodotObject var) => true;
		public override void _Edit(GodotObject var)
		{
			if (var is Node)
				target = (Node)var;
		}

		public override int _Forward3DGuiInput(Camera3D cam, InputEvent e)
		{
			if (cam != null)
			{
				editorCam = cam;
				UpdateOverlays();
			}

			return base._Forward3DGuiInput(cam, e);
		}

		private const int PREVIEW_RESOLUTION = 32;

		public override void _Forward3DDrawOverViewport(Control overlay)
		{
			if (!IsInstanceValid(target) || !target.IsInsideTree() || !IsInstanceValid(editorCam)) return;

			if (target is Launcher)
				DrawLaunchSettings(overlay, (target as Launcher).GetLaunchSettings(), DEFAULT_DRAW_COLOR);
			else if (target is JumpTrigger)
				DrawLaunchSettings(overlay, (target as JumpTrigger).GetLaunchSettings(), DEFAULT_DRAW_COLOR);
			else if (target is Catapult)
				DrawLaunchSettings(overlay, (target as Catapult).GetLaunchSettings(), DEFAULT_DRAW_COLOR.Lerp(SPECIAL_DRAW_COLOR, (target as Catapult).launchPower));
			else if (target is LaunchRing)
				DrawLaunchSettings(overlay, (target as LaunchRing).GetLaunchSettings(), DEFAULT_DRAW_COLOR.Lerp(SPECIAL_DRAW_COLOR, (target as LaunchRing).LaunchRatio));
			else if (target is ItemBox)
				UpdateItemBox(overlay);
			else if (target is FlyingPot)
				UpdatePot(overlay);
			else if (target is DriftTrigger)
				UpdateDriftCorner(overlay);
			else if (target is MovingObject)
				UpdateMovingObject(overlay);
			else if (target is Majin)
				UpdateMajinPath(overlay);
		}

		private readonly Color DEFAULT_DRAW_COLOR = Colors.Blue;
		private readonly Color SPECIAL_DRAW_COLOR = Colors.Red;
		private void DrawLaunchSettings(Control overlay, LaunchSettings LaunchSettings, Color overrideColor)
		{
			Array<Vector2> points = new Array<Vector2>();

			for (int i = 0; i < PREVIEW_RESOLUTION; i++)
			{
				float simulationRatio = i / ((float)PREVIEW_RESOLUTION - 1);
				Vector3 position = LaunchSettings.InterpolatePositionRatio(simulationRatio);
				if (!editorCam.IsPositionBehind(position))
					points.Add(editorCam.UnprojectPosition(position));
			}

			if (points.Count < 2) return; //Can't draw!!!

			Vector2[] pointsList = new Vector2[points.Count];
			points.CopyTo(pointsList, 0);
			overlay.DrawPolyline(pointsList, overrideColor, 1, true);
		}

		private void UpdatePot(Control overlay)
		{
			Array<Vector3> points = new Array<Vector3>();
			FlyingPot pot = (target as FlyingPot);

			Vector3 bottomRight = pot.GlobalPosition + pot.Right() * (pot.travelBounds.X + pot.boundOffset);
			Vector3 bottomLeft = pot.GlobalPosition + pot.Left() * (pot.travelBounds.X - pot.boundOffset);
			points.Add(bottomRight);
			points.Add(bottomRight + Vector3.Up * pot.travelBounds.Y);
			points.Add(bottomLeft + Vector3.Up * pot.travelBounds.Y);
			points.Add(bottomLeft);

			Vector2[] pointsList = new Vector2[points.Count];
			for (int i = 0; i < points.Count; i++)
			{
				if (!editorCam.IsPositionBehind(points[i]))
					pointsList[i] = editorCam.UnprojectPosition(points[i]);
			}

			overlay.DrawPolyline(pointsList, DEFAULT_DRAW_COLOR
			, 1, true);
		}

		private void UpdateDriftCorner(Control overlay)
		{
			if (editorCam.IsPositionBehind((target as DriftTrigger).GlobalPosition) ||
			editorCam.IsPositionBehind((target as DriftTrigger).MiddlePosition))
				return;

			Vector2 start = editorCam.UnprojectPosition((target as DriftTrigger).GlobalPosition);
			Vector2 middle = editorCam.UnprojectPosition((target as DriftTrigger).MiddlePosition);
			overlay.DrawLine(start, middle, DEFAULT_DRAW_COLOR, 1, true);

			if (editorCam.IsPositionBehind((target as DriftTrigger).EndPosition)) return;

			Vector2 end = editorCam.UnprojectPosition((target as DriftTrigger).EndPosition);
			overlay.DrawLine(middle, end, SPECIAL_DRAW_COLOR, 1, true);
		}

		private void UpdateItemBox(Control overlay)
		{
			ItemBox box = (target as ItemBox);

			if (box.spawnPearls) return;

			DrawLaunchSettings(overlay, box.GetLaunchSettings(), DEFAULT_DRAW_COLOR);
		}

		private void UpdateMovingObject(Control overlay)
		{
			MovingObject obj = target as MovingObject;
			if (obj.IsMovementInvalid()) return; //Don't draw

			Array<Vector2> points = new Array<Vector2>();

			for (int i = 0; i < PREVIEW_RESOLUTION; i++)
			{
				float simulationRatio = i / ((float)PREVIEW_RESOLUTION - 1);
				Vector3 position = obj.InterpolatePosition(simulationRatio);
				if (!editorCam.IsPositionBehind(position))
					points.Add(editorCam.UnprojectPosition(position));
			}

			if (points.Count < 2) return; //Can't draw!!!

			Vector2[] pointsList = new Vector2[points.Count];
			points.CopyTo(pointsList, 0);
			overlay.DrawPolyline(pointsList, DEFAULT_DRAW_COLOR, 1, true);

			Vector3 startingPoint = obj.InterpolatePosition(obj.StartingOffset);
			if (!editorCam.IsPositionBehind(startingPoint))
				overlay.DrawCircle(editorCam.UnprojectPosition(startingPoint), 5f, SPECIAL_DRAW_COLOR);
		}

		private void DrawPerspectiveCircle(Control overlay, Vector3 center, Basis basis, float radius, Vector3 startingAxis, Vector3 rotationAxis, Color col)
		{
			Array<Vector3> points = new Array<Vector3>();
			for (int i = 0; i < PREVIEW_RESOLUTION; i++)
			{
				float angle = Mathf.Tau * ((float)i / PREVIEW_RESOLUTION);
				points.Add(center + basis * startingAxis.Rotated(rotationAxis, angle).Normalized() * radius);
			}

			Vector2 start = editorCam.UnprojectPosition(points[0]);
			Vector2 end = editorCam.UnprojectPosition(points[points.Count - 1]);
			overlay.DrawLine(start, end, col);
			for (int i = 1; i < points.Count; i++)
			{
				start = editorCam.UnprojectPosition(points[i - 1]);
				end = editorCam.UnprojectPosition(points[i]);
				overlay.DrawLine(start, end, col);
			}
		}

		private void UpdateMajinPath(Control overlay)
		{
			Majin t = target as Majin;
			if (t.spawnOffset == Vector3.Zero) return;

			Vector3 s = t.GlobalPosition;
			Vector3 e = s + t.Basis * t.spawnOffset;
			if (editorCam.IsPositionBehind(s) ||
			editorCam.IsPositionBehind(e))
				return;

			overlay.DrawLine(editorCam.UnprojectPosition(s), editorCam.UnprojectPosition(e), DEFAULT_DRAW_COLOR, 1, true);
		}
	}
#endif
}
