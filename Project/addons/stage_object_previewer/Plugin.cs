using Godot;
using Godot.Collections;
using Project.Gameplay;
using Project.Gameplay.Triggers;
using Project.Gameplay.Objects;
using Project.Gameplay.Hazards;

namespace Project.Editor.StageObjectPreviewer;

#if TOOLS
[Tool]
public partial class Plugin : EditorPlugin
{
	public Plugin plugin;

	private Node target;
	private Control viewportOverlay;
	private static Camera3D editorCam;
	private readonly Color DefaultDrawColor = Colors.Blue;
	private readonly Color SpecialDrawColor = Colors.Red;
	private const int PreviewResolution = 32;

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

	public override void _Forward3DDrawOverViewport(Control overlay)
	{
		if (!IsInstanceValid(target) || !target.IsInsideTree() || !IsInstanceValid(editorCam)) return;

		viewportOverlay = overlay;

		if (target is Launcher)
			DrawLaunchSettings((target as Launcher).GetLaunchSettings(), DefaultDrawColor.Lerp(SpecialDrawColor, (target as Launcher).GetLaunchRatio()));
		else if (target is JumpTrigger)
			DrawLaunchSettings((target as JumpTrigger).GetLaunchSettings(), DefaultDrawColor);
		else if (target is GasTank)
			DrawLaunchSettings((target as GasTank).GetLaunchSettings(), DefaultDrawColor);
		else if (target is BombMissile)
			DrawLaunchSettings((target as BombMissile).GetLaunchSettings(), DefaultDrawColor);
		else if (target is Sword)
			DrawLaunchSettings((target as Sword).GetLaunchSettings(), DefaultDrawColor);
		else if (target is ItemBox)
			UpdateItemBox();
		else if (target is FlyingPot)
			UpdatePot();
		else if (target is DriftTrigger)
			UpdateDriftCorner();
		else if (target is MovingObject)
			UpdateMovingObject();
		else if (target is Majin)
			DrawMajin();
		else if (target is SlimeMajin)
			DrawSlimeMajin();

		if (target is Enemy)
			DrawEnemy();
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

		viewportOverlay.DrawPolyline(pointsList, DefaultDrawColor, 1, true);
	}

	private void UpdateDriftCorner()
	{
		DriftTrigger trigger = target as DriftTrigger;
		DrawLine(trigger.GlobalPosition, trigger.MiddlePosition, DefaultDrawColor);
		DrawLine(trigger.MiddlePosition, trigger.EndPosition, SpecialDrawColor);
	}

	private void UpdateItemBox()
	{
		ItemBox box = target as ItemBox;

		if (box.spawnPearls) return;

		DrawLaunchSettings(box.GetLaunchSettings(), DefaultDrawColor);
	}

	private void UpdateMovingObject()
	{
		MovingObject obj = target as MovingObject;
		if (obj.IsMovementInvalid()) return; //Don't draw

		Array<Vector2> points = [];

		for (int i = 0; i < PreviewResolution; i++)
		{
			float simulationRatio = i / ((float)PreviewResolution - 1);
			Vector3 position = obj.InterpolatePosition(simulationRatio);
			if (!editorCam.IsPositionBehind(position))
				points.Add(editorCam.UnprojectPosition(position));
		}

		if (points.Count < 2) return; //Can't draw!!!

		Vector2[] pointsList = new Vector2[points.Count];
		points.CopyTo(pointsList, 0);
		viewportOverlay.DrawPolyline(pointsList, DefaultDrawColor, 1, true);

		obj.ApplyEditorPosition();
		Vector3 startingPoint = obj.InterpolatePosition(obj.StartingOffset);
		if (!editorCam.IsPositionBehind(startingPoint))
			viewportOverlay.DrawCircle(editorCam.UnprojectPosition(startingPoint), 5f, SpecialDrawColor);
	}

	private void DrawEnemy()
	{
		Enemy enemy = target as Enemy;
		if (enemy.rangeOverride == -1 || enemy.SpawnMode != Enemy.SpawnModes.Range)
			return;

		DrawPerspectiveCircle(enemy.GlobalPosition, Basis.Identity, enemy.rangeOverride, Vector3.Forward, Vector3.Up, DefaultDrawColor);
	}

	private void DrawMajin()
	{
		Majin majin = target as Majin;
		if (majin.SpawnTravelEnabled)
			DrawMajinPath(majin);

		if (majin.IsDefeatLaunchEnabled &&
			editorCam.IsPositionInFrustum(majin.OriginalPosition) &&
			editorCam.IsPositionInFrustum(majin.CalculateLaunchPosition()))
		{
			viewportOverlay.DrawLine(editorCam.UnprojectPosition(majin.OriginalPosition), editorCam.UnprojectPosition(majin.CalculateLaunchPosition()), SpecialDrawColor, 1, true);
		}

		if (majin.IsRedMajin && majin.FlameAggressionRadius != 0)
			DrawPerspectiveCircle(majin.GlobalPosition, Basis.Identity, majin.FlameAggressionRadius, Vector3.Forward, Vector3.Up, SpecialDrawColor);
	}

	private void DrawSlimeMajin()
	{
		SlimeMajin slime = target as SlimeMajin;
		if (slime.IsSpawnLaunchEnabled)
			DrawLaunchSettings(slime.SpawnLaunchSettings, Colors.Blue);

		if (slime.IsMovementEnabled)
		{
			DrawLine(slime.MovementStartPosition, slime.MovementEndPosition, Colors.Blue);
		}
	}

	private void DrawMajinPath(Majin majin)
	{
		// Draw each of the handles
		if (editorCam.IsPositionInFrustum(majin.SpawnPosition))
			viewportOverlay.DrawCircle(editorCam.UnprojectPosition(majin.SpawnPosition), 5f, SpecialDrawColor);
		if (editorCam.IsPositionInFrustum(majin.InHandle))
			viewportOverlay.DrawCircle(editorCam.UnprojectPosition(majin.InHandle), 5f, SpecialDrawColor);
		if (editorCam.IsPositionInFrustum(majin.OutHandle))
			viewportOverlay.DrawCircle(editorCam.UnprojectPosition(majin.OutHandle), 5f, SpecialDrawColor);

		// Draw the spawn curve
		Array<Vector2> points = [];

		for (int i = 0; i < PreviewResolution; i++)
		{
			float simulationRatio = i / ((float)PreviewResolution - 1);
			Vector3 position = majin.CalculateTravelPosition(simulationRatio);
			if (editorCam.IsPositionInFrustum(position))
				points.Add(editorCam.UnprojectPosition(position));
		}

		if (points.Count < 2) return; // Can't draw!!!

		Vector2[] pointsList = new Vector2[points.Count];
		points.CopyTo(pointsList, 0);
		viewportOverlay.DrawPolyline(pointsList, DefaultDrawColor, 1, true);
	}

	private void DrawLaunchSettings(LaunchSettings LaunchSettings, Color col)
	{
		Array<Vector2> points = [];

		for (int i = 0; i < PreviewResolution; i++)
		{
			float simulationRatio = i / ((float)PreviewResolution - 1);
			Vector3 position = LaunchSettings.InterpolatePositionRatio(simulationRatio);
			if (!editorCam.IsPositionBehind(position))
				points.Add(editorCam.UnprojectPosition(position));
		}

		if (points.Count < 2) return; //Can't draw!!!

		Vector2[] pointsList = new Vector2[points.Count];
		points.CopyTo(pointsList, 0);
		viewportOverlay.DrawPolyline(pointsList, col, 1, true);
	}

	private void DrawPerspectiveCircle(Vector3 center, Basis basis, float radius, Vector3 startingAxis, Vector3 rotationAxis, Color col)
	{
		Array<Vector2> points = [];

		for (int i = 0; i < PreviewResolution + 1; i++)
		{
			float angle = Mathf.Tau * ((float)i / PreviewResolution);
			Vector3 position = center + (basis * startingAxis.Rotated(rotationAxis, angle).Normalized() * radius);
			if (!editorCam.IsPositionBehind(position))
				points.Add(editorCam.UnprojectPosition(position));
		}

		Vector2[] pointsList = new Vector2[points.Count];
		points.CopyTo(pointsList, 0);
		viewportOverlay.DrawPolyline(pointsList, col, 1, true);
	}

	private void DrawLine(Vector3 s, Vector3 e, Color c)
	{
		if (editorCam.IsPositionBehind(s) || editorCam.IsPositionBehind(e)) return;
		viewportOverlay.DrawLine(editorCam.UnprojectPosition(s), editorCam.UnprojectPosition(e), c, -1, true);
	}
}
#endif