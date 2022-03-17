using Godot;

namespace Project.Gameplay
{
	[Tool]
	public class GrindRail : StageObject
	{
		public Path Path { get; private set; }
		public Curve3D Curve => Path.Curve;
		private Spatial _endCap;
		private CSGPolygon _rail;
		private CollisionShape _collider;

		[Export]
		public float length;
		[Export]
		public bool isInvisibleRail;
		[Export]
		public Material railMaterial; //Only applied when isInvisibleRail is set to FALSE.

		public override bool IsRespawnable() => false;

		public void Rebuild()
		{
			//Reset
			Path = null;
			_rail = null;
			_collider = null;
			_endCap = null;

			for (int i = 0; i < GetChildCount(); i++)
			{
				Node child = GetChild<Node>(i);

				if (Path == null && child is Path)
					Path = child as Path;

				if (_collider == null && child is CollisionShape)
					_collider = child as CollisionShape;

				if (_rail == null && child is CSGPolygon)
					_rail = child as CSGPolygon;

				if (!isInvisibleRail && _endCap == null && child.Name.Equals("End"))
					_endCap = child as Spatial;

				if (Path != null && _collider != null && (isInvisibleRail || _endCap != null) && _rail != null) break;
			}

			Path.Curve = new Curve3D();
			Curve.AddPoint(Vector3.Zero);
			Curve.AddPoint(Vector3.Forward * length);

			_collider.Shape = new BoxShape()
			{
				Extents = new Vector3(.2f, .2f, length * .5f)
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

		/*
		public override void OnStay()
		{
			if (ActiveCharacter.ActiveGrindRail == null)
			{
				if (GrindRailValid(ActiveCharacter.GlobalTransform.origin))
					ActiveCharacter.StartGrinding(this);
			}
		}
		*/

		public float GetClosestOffset(Vector3 globalPosition)
		{
			//Associate's position local to path position
			Vector3 localPosition = Character.GlobalTransform.origin - Path.GlobalTransform.origin;
			return Path.Curve.GetClosestOffset(localPosition);
		}
	}
}
