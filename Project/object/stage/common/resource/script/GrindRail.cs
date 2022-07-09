using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class GrindRail : Area
	{
		[Export]
		public NodePath path;
		public Path GrindPath { get; private set; }
		public Curve3D Curve => GrindPath.Curve;
		private Spatial _endCap;
		private CSGPolygon _rail;
		private CollisionShape _collider;

		[Export]
		public bool generate;
		[Export]
		public bool generateAll; //Regenerates every single rail in this scene. (SLOW!)
		[Export]
		public int length;
		[Export]
		public bool isInvisibleRail;
		[Export]
		public Material railMaterial; //Only applied when isInvisibleRail is set to FALSE.

		private CharacterController Character => CharacterController.instance;
		private const float RAIL_HEIGHT = .15f;

		public override void _Ready()
		{
			if (Engine.EditorHint) return;
			GrindPath = GetNode<Path>(path);
		}

		public override void _Process(float _)
		{
			if (!Engine.EditorHint) return;

			if (generateAll)
			{
				Array nodes = GetTree().GetNodesInGroup("grindrail");
				for (int i = 0; i < nodes.Count; i++)
				{
					if (!(nodes[i] is GrindRail)) continue;
					(nodes[i] as GrindRail).GenerateRail();
				}

				generateAll = false;
			}

			if (!generate) return;
			generate = false;

			GenerateRail();
		}

		//Reset
		private void GenerateRail()
		{
			GrindPath = null;
			_rail = null;
			_collider = null;
			_endCap = null;

			for (int i = 0; i < GetChildCount(); i++)
			{
				Node child = GetChild<Node>(i);

				if (GrindPath == null && child is Path)
					GrindPath = child as Path;

				if (_collider == null && child is CollisionShape)
					_collider = child as CollisionShape;

				if (_rail == null && child is CSGPolygon)
					_rail = child as CSGPolygon;

				if (!isInvisibleRail && _endCap == null && child.Name.Equals("End"))
					_endCap = child as Spatial;

				if (GrindPath != null && _collider != null && (isInvisibleRail || _endCap != null) && _rail != null) break;
			}

			GrindPath.Transform = Transform.Identity;
			GrindPath.Translate(Vector3.Up * RAIL_HEIGHT);
			GrindPath.Curve = new Curve3D();
			Curve.AddPoint(Vector3.Zero);
			Curve.AddPoint(Vector3.Forward * length);

			_collider.Shape = new BoxShape()
			{
				Extents = new Vector3(.4f, .2f, length * .5f + .42f)
			};
			Transform t = _collider.Transform;
			t.origin = Vector3.Forward * length * .5f;
			_collider.Transform = t;

			if (!isInvisibleRail)
			{
				_rail.MaterialOverride = railMaterial;
				if (_endCap != null)
				{
					t = _endCap.Transform;
					t.origin = Vector3.Forward * length;
					_endCap.Transform = t;
				}
			}
		}

		public float GetClosestOffset(Vector3 globalPosition)
		{
			//Associate's position local to path position
			Vector3 localPosition = globalPosition - GrindPath.GlobalTransform.origin;
			return GrindPath.Curve.GetClosestOffset(localPosition);
		}

		public void OnEntered(Area _)
		{
			if (!CanGrind) return;

			//Calculate connection point
			Vector3 delta = Character.GlobalTransform.origin - GlobalTransform.origin;
			delta = GlobalTransform.basis.RotationQuat().Inverse().Xform(delta);
			float dst = Mathf.Abs(delta.z);
			Vector3 connectionPoint = GlobalTransform.origin + this.Up() * RAIL_HEIGHT - this.Forward() * dst;
			Character.StartGrinding(this, connectionPoint);
		}

		public void OnExited(Area _)
		{
			if (Character.MovementState != CharacterController.MovementStates.Grinding) return;
			Character.StopGrinding();
		}

		private bool CanGrind => Character.MovementState != CharacterController.MovementStates.Grinding && !Character.IsRising && (!Character.IsOnGround || Character.JustLandedOnGround);
	}
}
