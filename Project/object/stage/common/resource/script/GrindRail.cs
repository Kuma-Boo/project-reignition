using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public class GrindRail : Area
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
		public Path GrindPath { get; private set; }
		public Curve3D Curve => GrindPath.Curve;
		[Export]
		public Mesh capMeshData;
		private readonly Vector2[] railPolygon = {
			new Vector2(-0.07f, -0.145f),
			new Vector2(-0.07f, 0.145f),
			new Vector2(0.07f, 0.145f),
			new Vector2(0.07f, -0.145f),
		};

		private MeshInstance _startCap;
		private MeshInstance _endCap;
		private CSGPolygon _polygon;
		private CollisionShape _collider;

		private CharacterController Character => CharacterController.instance;
		private const float RAIL_HEIGHT = .15f;

		public override void _Ready()
		{
			if (Engine.EditorHint) return;

			GrindPath = GetNode<Path>("Components/RailPath");
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
			UpdateVariables();
			
			GrindPath.Translation = Vector3.Up * RAIL_HEIGHT;
			GrindPath.Curve = new Curve3D();
			Curve.AddPoint(Vector3.Zero);
			Curve.AddPoint(Vector3.Forward * length);


			_polygon.Depth = length;

			_collider.Shape = new BoxShape() { Extents = new Vector3(.4f, .2f, length * .5f + .42f) };
			_collider.Translation = Vector3.Forward * length * .5f;

			if (!isInvisibleRail)
			{
				_polygon.MaterialOverride = railMaterial;

				_startCap.MaterialOverride = _endCap.MaterialOverride = capMaterial;
				_endCap.Translation = Vector3.Forward * length;
			}
		}

		private void UpdateVariables()
		{
			Spatial componentParent = GetNodeOrNull<Spatial>("Components");
			if (componentParent == null)
			{
				componentParent = new Spatial() { Name = "Components" };
				AddChild(componentParent);
				componentParent.Owner = GetTree().EditedSceneRoot;
			}

			_polygon = GetNodeOrNull<CSGPolygon>("Components/RailMesh");
			if(_polygon == null)
			{
				_polygon = new CSGPolygon() { Name = "RailMesh" };
				componentParent.AddChild(_polygon);
				_polygon.Owner = GetTree().EditedSceneRoot;
			}

			_polygon.Polygon = railPolygon;
			_polygon.Mode = CSGPolygon.ModeEnum.Depth;

			GrindPath = GetNodeOrNull<Path>("Components/RailPath");
			if (GrindPath == null)
			{
				GrindPath = new Path() { Name = "RailPath" };
				componentParent.AddChild(GrindPath);
				GrindPath.Owner = GetTree().EditedSceneRoot;
			}

			_collider = GetNodeOrNull<CollisionShape>("RailCollision");
			if (_collider == null)
			{
				_collider = new CollisionShape() { Name = "RailCollision" };
				AddChild(_collider);
				_collider.Owner = GetTree().EditedSceneRoot;
			}

			_startCap = GetNodeOrNull<MeshInstance>("Components/StartCap");
			_endCap = GetNodeOrNull<MeshInstance>("Components/EndCap");
			if (isInvisibleRail) //Remove unnecessary objects 
			{
				if (_startCap != null)
					_startCap.QueueFree();

				if(_endCap != null)
					_endCap.QueueFree();
			}
			else //Create caps
			{
				if (_startCap == null)
				{
					_startCap = new MeshInstance() { Name = "StartCap" };
					componentParent.AddChild(_startCap);
					_startCap.Owner = GetTree().EditedSceneRoot;
				}

				if (_endCap == null)
				{
					_endCap = new MeshInstance() { Name = "EndCap" };
					componentParent.AddChild(_endCap);
					_endCap.Owner = GetTree().EditedSceneRoot;
				}

				_startCap.Mesh = _endCap.Mesh = capMeshData;
				_endCap.RotationDegrees = Vector3.Up * 180;
			}
		}

		public float GetClosestOffset(Vector3 globalPosition)
		{
			//Associate's position local to path position
			Vector3 localPosition = globalPosition - GrindPath.GlobalTranslation;
			return GrindPath.Curve.GetClosestOffset(localPosition);
		}

		public void OnEntered(Area _)
		{
			if (!CanGrind) return;

			//Calculate connection point
			Vector3 delta = Character.GlobalTranslation - GlobalTranslation;
			delta = GlobalTransform.basis.RotationQuat().Inverse().Xform(delta);
			float dst = Mathf.Abs(delta.z);
			Vector3 connectionPoint = GlobalTranslation + this.Up() * RAIL_HEIGHT - this.Forward() * dst;
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
