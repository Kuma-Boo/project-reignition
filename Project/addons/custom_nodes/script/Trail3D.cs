using Godot;
using Project.Core;
using Project.Gameplay;
using System.Collections.Generic;

namespace Project.CustomNodes
{
	public partial class Trail3D : Node3D
	{
		public PlayerController Player { get; set; }
		[Export]
		public bool IsEmitting { get; set; }
		private MeshInstance3D trailMeshInstance;
		private ImmediateMesh trailMesh;
		private Vector3 previousPosition;

		[Export]
		private float radius = .3f; // Radius of the trail
		[Export]
		private int resolution = 16; // Resolution of the homing attack trail length-wise
		[Export(PropertyHint.Range, "0.01, 1, 0.01")]
		private float distanceDeadzone = .1f; // Resolution of the homing attack trail distance-wise
		[Export]
		private float lifetime = .5f; // How long each point should live
		[Export]
		private Curve lifetimeCurve;
		[Export]
		private Curve positionCurve;
		private const float TrailUVStep = .01f; // How much of the uv each segment should take up

		[Export(PropertyHint.Layers3DRender)]
		private uint layer;
		[Export]
		public Material material;

		private readonly List<Point> points = []; // Data of each point
		private readonly List<float> pointLifetimes = []; // Lifetime of each point

		public override void _Ready()
		{
			trailMesh = new();

			// Actual mesh instance is parented at the bottom of the tree so trails render AFTER everything else has moved.
			trailMeshInstance = new()
			{
				Layers = layer,
				MaterialOverride = material,
				Mesh = trailMesh,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};

			GetTree().CurrentScene.CallDeferred(MethodName.AddChild, trailMeshInstance);
			previousPosition = GlobalPosition;
		}

		public override void _PhysicsProcess(double delta) => CallDeferred(MethodName.UpdateTrail, delta);

		private void UpdateTrail(double delta)
		{
			trailMeshInstance.GlobalTransform = GlobalTransform;

			if (IsEmitting && trailMeshInstance.GlobalPosition.DistanceSquaredTo(previousPosition) >= Mathf.Pow(distanceDeadzone, 2.0f)) // Check for new points
				AddPoint();

			for (int i = points.Count - 1; i >= 0; i--) // Update each point in reverse order
			{
				pointLifetimes[i] += (float)delta;
				if (pointLifetimes[i] >= lifetime)
					RemovePoint(i);
			}

			RenderTrail();
		}

		private void RenderTrail()
		{
			trailMesh.ClearSurfaces();

			if (points.Count < 2) // No points to render
				return;

			float angleIncrement = Mathf.Tau / resolution;

			if (Player != null)
			{
				for (int i = 0; i < points.Count; i++)
					points[i].position += Player.Animator.Back() * Player.MoveSpeed * PhysicsManager.physicsDelta;
			}

			previousPosition = points[^1].position;

			for (int y = 0; y < resolution; y++)
			{
				trailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
				float yFactor01 = y / (float)resolution;
				float yFactor02 = (y + 1) / (float)resolution;

				for (int x = 0; x < points.Count; x++)
				{
					float xFactor = x / (points.Count - 1.0f);
					float transparency = Mathf.Clamp(lifetimeCurve.Sample(pointLifetimes[x] / lifetime), 0f, 1f);
					transparency *= Mathf.Clamp(positionCurve.Sample(xFactor), 0f, 1f);

					Vector3 normal01 = points[x].normal.Rotated(points[x].tangent, angleIncrement * y);
					Vector3 normal02 = normal01.Rotated(points[x].tangent, angleIncrement);
					Vector3 surfaceNormal = (normal01 + normal02) * .5f;
					trailMesh.SurfaceSetUV(new Vector2(x * TrailUVStep, yFactor01));
					trailMesh.SurfaceSetUV2(new Vector2(xFactor, yFactor01));
					trailMesh.SurfaceSetColor(new(Colors.White, transparency));
					trailMesh.SurfaceSetNormal(surfaceNormal);
					trailMesh.SurfaceAddVertex(ToLocal(points[x].position + (normal01 * radius)));

					trailMesh.SurfaceSetUV(new Vector2(x * TrailUVStep, yFactor02));
					trailMesh.SurfaceSetUV2(new Vector2(xFactor, yFactor01));
					trailMesh.SurfaceSetColor(new(Colors.White, transparency));
					trailMesh.SurfaceSetNormal(surfaceNormal);
					trailMesh.SurfaceAddVertex(ToLocal(points[x].position + (normal02 * radius)));
				}

				trailMesh.SurfaceEnd();
			}
		}

		private void AddPoint()
		{
			if (points.Count == 0)
				previousPosition = trailMeshInstance.GlobalPosition + this.Back();

			Vector3 tangentDirection = (previousPosition - trailMeshInstance.GlobalPosition).Normalized();
			Vector3 upDirection = tangentDirection.Rotated(this.Right(), Mathf.Pi * .5f);
			points.Add(new(GlobalPosition, upDirection, tangentDirection));
			pointLifetimes.Add(0);
			previousPosition = trailMeshInstance.GlobalPosition;
		}

		private void RemovePoint(int index)
		{
			points.RemoveAt(index);
			pointLifetimes.RemoveAt(index);
		}

		private class Point
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
