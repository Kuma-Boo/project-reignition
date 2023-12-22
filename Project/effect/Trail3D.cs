using Godot;
using System.Collections.Generic;

namespace Project.Core
{
	[Tool]
	public partial class Trail3D : MeshInstance3D
	{
		[Export]
		public bool IsEmitting { get; set; }
		private ImmediateMesh trailMesh;
		private Vector3 previousPosition;

		[Export]
		private float radius = .3f; // Radius of the trail
		[Export]
		private int resolution = 16; // Resolution of the homing attack trail length-wise
		[Export]
		private float distance_deadzone = .1f; // Resolution of the homing attack trail distance-wise
		[Export]
		private float lifetime = .5f; // How long each point should live
		private const float ATTACK_TRAIL_UV_STEP = .01f; // How much of the uv each segment should take up



		private readonly List<Point> points = new(); // Data of each point
		private readonly List<float> pointLifetimes = new(); // Lifetime of each point

		public override void _Ready()
		{
			trailMesh = new ImmediateMesh();
			Mesh = trailMesh;
			previousPosition = GlobalPosition;
		}


		public override void _PhysicsProcess(double delta)
		{
			UpdateTrail(delta);
			RenderTrail();
		}


		private void UpdateTrail(double delta)
		{
			if (IsEmitting && GlobalPosition.DistanceSquaredTo(previousPosition) >= Mathf.Pow(distance_deadzone, 2.0f)) // Check for new points
				AddPoint();

			for (int i = points.Count - 1; i >= 0; i--) // Update each point in reverse order
			{
				pointLifetimes[i] += (float)delta;
				if (pointLifetimes[i] >= lifetime)
					RemovePoint(i);
			}
		}


		private void RenderTrail()
		{
			trailMesh.ClearSurfaces();

			if (points.Count < 2) // No points to render
				return;

			float angleIncrement = Mathf.Tau / resolution;

			for (int y = 0; y < resolution; y++)
			{
				trailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
				float yFactor01 = y / (float)resolution;
				float yFactor02 = (y + 1) / (float)resolution;

				for (int x = 0; x < points.Count; x++)
				{
					float xFactor = x / (points.Count - 1.0f);

					Vector3 normal01 = points[x].normal.Rotated(points[x].tangent, angleIncrement * y);
					Vector3 normal02 = normal01.Rotated(points[x].tangent, angleIncrement);
					Vector3 surfaceNormal = (normal01 + normal02) * .5f;
					trailMesh.SurfaceSetUV(new Vector2(x * ATTACK_TRAIL_UV_STEP, yFactor01));
					trailMesh.SurfaceSetUV2(new Vector2(xFactor, yFactor01));
					trailMesh.SurfaceSetNormal(surfaceNormal);
					trailMesh.SurfaceAddVertex(ToLocal(points[x].position + normal01 * radius));

					trailMesh.SurfaceSetUV(new Vector2(x * ATTACK_TRAIL_UV_STEP, yFactor02));
					trailMesh.SurfaceSetUV2(new Vector2(xFactor, yFactor01));
					trailMesh.SurfaceSetNormal(surfaceNormal);
					trailMesh.SurfaceAddVertex(ToLocal(points[x].position + normal02 * radius));
				}

				trailMesh.SurfaceEnd();
			}
		}


		private void AddPoint()
		{
			Vector3 tangentDirection = (GlobalPosition - previousPosition).Normalized();
			Vector3 upDirection = tangentDirection.Rotated(this.Right(), Mathf.Pi * .5f);
			points.Add(new Point(GlobalPosition, upDirection, tangentDirection));
			pointLifetimes.Add(0);
			previousPosition = GlobalPosition;
		}


		private void RemovePoint(int index)
		{
			points.RemoveAt(index);
			pointLifetimes.RemoveAt(index);
		}

		private struct Point
		{
			public Vector3 position; // Origin of the point
			public Vector3 normal; // "Up" direction of the point
			public Vector3 tangent; // "Forward" direction of the point


			public Point(Vector3 p, Vector3 n, Vector3 t)
			{
				position = p;
				normal = n;
				tangent = t;
			}
		}
	}
}
