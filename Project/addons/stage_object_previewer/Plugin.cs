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
		private Control viewportOverlay;
		public static Camera3D editorCam;

		public override bool _Handles(GodotObject var) => true;
		public override void _Edit(GodotObject var)
		{
			if (var is Node node)
				target = node;
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

			viewportOverlay = overlay;

			if (target is Launcher)
				DrawLaunchSettings((target as Launcher).GetLaunchSettings(), DEFAULT_DRAW_COLOR);
			else if (target is JumpTrigger)
				DrawLaunchSettings((target as JumpTrigger).GetLaunchSettings(), DEFAULT_DRAW_COLOR);
			else if (target is Catapult)
				DrawLaunchSettings((target as Catapult).GetLaunchSettings(), DEFAULT_DRAW_COLOR.Lerp(SPECIAL_DRAW_COLOR, (target as Catapult).launchPower));
			else if (target is LaunchRing)
				DrawLaunchSettings((target as LaunchRing).GetLaunchSettings(), DEFAULT_DRAW_COLOR.Lerp(SPECIAL_DRAW_COLOR, (target as LaunchRing).LaunchRatio));
			else if (target is GasTank)
				DrawLaunchSettings((target as GasTank).GetLaunchSettings(), DEFAULT_DRAW_COLOR);
			else if (target is ItemBox)
				UpdateItemBox();
			else if (target is FlyingPot)
				UpdatePot();
			else if (target is DriftTrigger)
				UpdateDriftCorner();
			else if (target is MovingObject)
				UpdateMovingObject();
			else if (target is Majin)
				UpdateMajinPath();
		}

		private void UpdatePot()
		{
			Array<Vector3> points = new();
			FlyingPot pot = target as FlyingPot;

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

			viewportOverlay.DrawPolyline(pointsList, DEFAULT_DRAW_COLOR, 1, true);
		}

		private void UpdateDriftCorner()
		{
			DriftTrigger trigger = target as DriftTrigger;
			DrawLine(trigger.GlobalPosition, trigger.MiddlePosition, DEFAULT_DRAW_COLOR);
			DrawLine(trigger.MiddlePosition, trigger.EndPosition, SPECIAL_DRAW_COLOR);
		}

		private void UpdateItemBox()
		{
			ItemBox box = target as ItemBox;

			if (box.spawnPearls) return;

			DrawLaunchSettings(box.GetLaunchSettings(), DEFAULT_DRAW_COLOR);
		}

		private void UpdateMovingObject()
		{
			MovingObject obj = target as MovingObject;
			if (obj.IsMovementInvalid()) return; //Don't draw

			Array<Vector2> points = new();

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
			viewportOverlay.DrawPolyline(pointsList, DEFAULT_DRAW_COLOR, 1, true);

			Vector3 startingPoint = obj.InterpolatePosition(obj.StartingOffset);
			if (!editorCam.IsPositionBehind(startingPoint))
				viewportOverlay.DrawCircle(editorCam.UnprojectPosition(startingPoint), 5f, SPECIAL_DRAW_COLOR);
		}


		private void UpdateMajinPath()
		{
			Majin t = target as Majin;
			if (t.SpawnOffset.IsZeroApprox()) return;

			Vector3 s = t.GlobalPosition;
			DrawLine(s, s + t.SpawnOffset, DEFAULT_DRAW_COLOR);
		}

		private readonly Color DEFAULT_DRAW_COLOR = Colors.Blue;
		private readonly Color SPECIAL_DRAW_COLOR = Colors.Red;
		private void DrawLaunchSettings(LaunchSettings LaunchSettings, Color overrideColor)
		{
			Array<Vector2> points = new();

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
			viewportOverlay.DrawPolyline(pointsList, overrideColor, 1, true);
		}


		private void DrawPerspectiveCircle(Vector3 center, Basis basis, float radius, Vector3 startingAxis, Vector3 rotationAxis, Color col)
		{
			Array<Vector3> points = new();
			for (int i = 0; i < PREVIEW_RESOLUTION; i++)
			{
				float angle = Mathf.Tau * ((float)i / PREVIEW_RESOLUTION);
				points.Add(center + basis * startingAxis.Rotated(rotationAxis, angle).Normalized() * radius);
			}

			Vector2 start = editorCam.UnprojectPosition(points[0]);
			Vector2 end = editorCam.UnprojectPosition(points[points.Count - 1]);
			viewportOverlay.DrawLine(start, end, col);
			for (int i = 1; i < points.Count; i++)
			{
				start = editorCam.UnprojectPosition(points[i - 1]);
				end = editorCam.UnprojectPosition(points[i]);
				viewportOverlay.DrawLine(start, end, col);
			}
		}


		private void DrawLine(Vector3 s, Vector3 e, Color c)
		{
			if (editorCam.IsPositionBehind(s) || editorCam.IsPositionBehind(e)) return;
			viewportOverlay.DrawLine(editorCam.UnprojectPosition(s), editorCam.UnprojectPosition(e), c, -1, true);
		}
	}
#endif
}
