using Godot;
using System.Collections.Generic;

namespace Project.Core
{
	public partial class Debug : Node2D
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
			ProcessMode = ProcessModeEnum.Always;
		}

		public override void _PhysicsProcess(double _)
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
				if (!Input.IsKeyPressed(Key.Shift) && IsInstanceValid(Gameplay.CharacterController.instance))
					Gameplay.CharacterController.instance.StartRespawn();
				else
					TransitionManager.QueueSceneChange(string.Empty, true);
			}

			if (line3d.Count + line2d.Count != 0 && !IsPaused)
				QueueRedraw();
		}

		public override void _Draw()
		{
			if (drawRaycasts)
			{
				for (int i = line2d.Count - 1; i >= 0; i--)
				{
					DrawLine(line2d[i].start, line2d[i].end, line3d[i].color, 1.0f, true);
					line2d.RemoveAt(i);
				}

				Camera3D cam = GetViewport().GetCamera3d();
				if (cam == null) return; //NO CAMERA

				for (int i = line3d.Count - 1; i >= 0; i--)
				{
					if (cam.IsPositionBehind(line3d[i].start) || cam.IsPositionBehind(line3d[i].end))
						continue;

					Vector2 startPos = cam.UnprojectPosition(line3d[i].start);
					Vector2 endPos = cam.UnprojectPosition(line3d[i].end);

					DrawLine(startPos, endPos, line3d[i].color, 1.0f, true);
					line3d.RemoveAt(i);
				}
			}
		}

		#region Line Drawer
		public struct Line3D
		{
			public Vector3 start;
			public Vector3 end;
			public Color color;

			public Line3D(Vector3 s, Vector3 e, Color c)
			{
				start = s;
				end = e;
				color = c;
			}
		}

		public struct Line2D
		{
			public Vector2 start;
			public Vector2 end;
			public Color color;

			public Line2D(Vector2 s, Vector2 e, Color c)
			{
				start = s;
				end = e;
				color = c;
			}
		}

		private static readonly List<Line3D> line3d = new List<Line3D>();
		private static readonly List<Line2D> line2d = new List<Line2D>();

		public static void DrawLn(Vector3 s, Vector3 e, Color c) => line3d.Add(new Line3D(s, e, c));
		public static void DrawRay(Vector3 s, Vector3 r, Color c) => line3d.Add(new Line3D(s, s + r, c));

		public static void DrawLn(Vector2 s, Vector2 e, Color c) => line2d.Add(new Line2D(s, e, c));
		public static void DrawRay(Vector2 s, Vector2 r, Color c) => line2d.Add(new Line2D(s, s + r, c));
		#endregion
	}
}
