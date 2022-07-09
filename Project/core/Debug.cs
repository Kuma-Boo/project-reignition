using Godot;
using System.Collections.Generic;

namespace Project.Core
{
	public class Debug : Node2D
	{
		public static Debug Instance;
		private bool isAdvancingFrame;
		private bool IsPaused => GetTree().Paused;

		private readonly bool drawRaycasts = true;

		private enum Properties
		{
			HSpeed,
			VSpeed,
			Grounded,
			Charge,
			PropertyCount
		}

		public override void _Ready()
		{
			Instance = this;
			PauseMode = PauseModeEnum.Process;
		}

		public override void _PhysicsProcess(float _)
		{
			if (!OS.IsDebugBuild()) //Don't do anything in real build
				return;

			if (isAdvancingFrame)
			{
				GetTree().Paused = true;
				isAdvancingFrame = false;
			}

			if (InputManager.debugTurbo.wasPressed)
				Engine.TimeScale = 2.5f;
			else if (InputManager.debugTurbo.wasReleased)
				Engine.TimeScale = 1f;

			if (InputManager.debugPause.wasPressed)
				GetTree().Paused = !IsPaused;

			if (InputManager.debugAdvance.wasPressed)
			{
				GetTree().Paused = false;
				isAdvancingFrame = true;
			}

			if (InputManager.debugRestart.wasPressed)
			{
				/*
				if (IsInstanceValid(Adventure.Objects.Level.instance))
					Adventure.Objects.Level.instance.RespawnLevel();
				else
				*/
				GetTree().ReloadCurrentScene();
			}

			if (lines.Count != 0 && !IsPaused)
			{
				Update();
				lines.Clear();
			}
		}

		public override void _Draw()
		{
			if (!drawRaycasts)
				return;

			Camera cam = GetViewport().GetCamera();

			if (cam == null) return; //NO CAMERA

			for (int i = lines.Count - 1; i >= 0; i--)
			{
				if (cam.IsPositionBehind(lines[i].start) || cam.IsPositionBehind(lines[i].end))
					continue;

				Vector2 startPos = cam.UnprojectPosition(lines[i].start);
				Vector2 endPos = cam.UnprojectPosition(lines[i].end);

				DrawLine(startPos, endPos, lines[i].color);
			}
		}

		#region Line Drawer
		public struct Line
		{
			public Vector3 start;
			public Vector3 end;
			public Color color;

			public Line(Vector3 s, Vector3 e, Color c)
			{
				start = s;
				end = e;
				color = c;
			}
		}

		private static readonly List<Line> lines = new List<Line>();

		public static void DrawLn(Vector3 s, Vector3 e, Color c) => lines.Add(new Line(s, e, c));
		public static void DrawRay(Vector3 s, Vector3 r, Color c) => lines.Add(new Line(s, s + r, c));
		#endregion
	}
}
