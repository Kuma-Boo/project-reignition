using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects
{
	//TODO Rework this object completely
	[Tool]
	public partial class GrindRail : Area3D
	{
		[Export]
		public bool generate;
		[Export]
		public bool generateAll; //Regenerates every single rail in this scene. (SLOW!)
		[Export]
		public int length;
		[Export]
		public bool isInvisibleRail;
		[Export]
		public Material railMaterial;
		[Export]
		public Material capMaterial;

		/*
		[Export]
		public NodePath overridePath; //For BK styled rails?
		*/
		public Path3D GrindPath { get; private set; }
		public Curve3D Curve => GrindPath.Curve;
		[Export]
		public Mesh capMeshData;
		private readonly Vector2[] railPolygon = {
			new Vector2(-0.07f, -0.145f),
			new Vector2(-0.07f, 0.145f),
			new Vector2(0.07f, 0.145f),
			new Vector2(0.07f, -0.145f),
		};

		private MeshInstance3D _startCap;
		private MeshInstance3D _endCap;
		private CSGPolygon3D _polygon;
		private CollisionShape3D _collider;

		private CharacterController Character => CharacterController.instance;
		private const float RAIL_HEIGHT = .15f;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			GrindPath = GetNode<Path3D>("Components/RailPath");
		}

		public override void _Process(double _)
		{
			if (!Engine.IsEditorHint()) return;

			if (generateAll)
			{
				Array<Node> nodes = GetTree().GetNodesInGroup("grindrail");
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
			UpdateVariables();

			GrindPath.Position = Vector3.Up * RAIL_HEIGHT;
			GrindPath.Curve = new Curve3D();
			Curve.AddPoint(Vector3.Zero);
			Curve.AddPoint(Vector3.Forward * length);


			_polygon.Depth = length;

			_collider.Shape = new BoxShape3D() { Size = new Vector3(.4f, .2f, length * .5f + .42f) };
			_collider.Position = Vector3.Forward * length * .5f;

			if (!isInvisibleRail)
			{
				_polygon.MaterialOverride = railMaterial;

				_startCap.MaterialOverride = _endCap.MaterialOverride = capMaterial;
				_endCap.Position = Vector3.Forward * length;
			}
		}

		private void UpdateVariables()
		{
			Node3D componentParent = GetNodeOrNull<Node3D>("Components");
			if (componentParent == null)
			{
				componentParent = new Node3D() { Name = "Components" };
				AddChild(componentParent);
				componentParent.Owner = GetTree().EditedSceneRoot;
			}

			_polygon = GetNodeOrNull<CSGPolygon3D>("Components/RailMesh");
			if (_polygon == null)
			{
				_polygon = new CSGPolygon3D() { Name = "RailMesh" };
				componentParent.AddChild(_polygon);
				_polygon.Owner = GetTree().EditedSceneRoot;
			}

			_polygon.Polygon = railPolygon;
			_polygon.Mode = CSGPolygon3D.ModeEnum.Depth;

			GrindPath = GetNodeOrNull<Path3D>("Components/RailPath");
			if (GrindPath == null)
			{
				GrindPath = new Path3D() { Name = "RailPath" };
				componentParent.AddChild(GrindPath);
				GrindPath.Owner = GetTree().EditedSceneRoot;
			}

			_collider = GetNodeOrNull<CollisionShape3D>("RailCollision");
			if (_collider == null)
			{
				_collider = new CollisionShape3D() { Name = "RailCollision" };
				AddChild(_collider);
				_collider.Owner = GetTree().EditedSceneRoot;
			}

			_startCap = GetNodeOrNull<MeshInstance3D>("Components/StartCap");
			_endCap = GetNodeOrNull<MeshInstance3D>("Components/EndCap");
			if (isInvisibleRail) //RemoveAt unnecessary objects 
			{
				if (_startCap != null)
					_startCap.QueueFree();

				if (_endCap != null)
					_endCap.QueueFree();
			}
			else //Create caps
			{
				if (_startCap == null)
				{
					_startCap = new MeshInstance3D() { Name = "StartCap" };
					componentParent.AddChild(_startCap);
					_startCap.Owner = GetTree().EditedSceneRoot;
				}

				if (_endCap == null)
				{
					_endCap = new MeshInstance3D() { Name = "EndCap" };
					componentParent.AddChild(_endCap);
					_endCap.Owner = GetTree().EditedSceneRoot;
				}

				_startCap.Mesh = _endCap.Mesh = capMeshData;
				_endCap.Rotation = Vector3.Up * Mathf.Pi;
			}
		}

		public float GetClosestOffset(Vector3 globalPosition)
		{
			//Associate's position local to path position
			Vector3 localPosition = globalPosition - GrindPath.GlobalPosition;
			return GrindPath.Curve.GetClosestOffset(localPosition);
		}

		public void OnEntered(Area3D _)
		{
			if (!CanGrind) return;

			//Calculate connection point
			Vector3 delta = Character.GlobalPosition - GlobalPosition;
			delta = GlobalTransform.basis.GetRotationQuaternion().Inverse() * delta;
			float dst = Mathf.Abs(delta.z);
			Vector3 connectionPoint = GlobalPosition + this.Up() * RAIL_HEIGHT - this.Forward() * dst;
			//Character.StartGrinding(this, connectionPoint);
		}

		public void OnExited(Area3D _)
		{
			//if (Character.MovementState != CharacterController.MovementStates.Grinding) return;
			//Character.StopGrinding();
		}

		private bool CanGrind => false;
		//Character.MovementState != CharacterController.MovementStates.Grinding && !Character.IsRising && (!Character.IsOnGround || Character.JustLandedOnGround);
	}
}
