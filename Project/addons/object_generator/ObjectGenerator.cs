using Godot;

//Tool for generating groups of objects
namespace Project.Editor
{
	[Tool]
	public class ObjectGenerator : Spatial
	{
		[Export]
		public PackedScene targetScene;

		[Export]
		public bool generate;

		[Export(PropertyHint.Range, "0, 64")]
		public int amount;

		[Export]
		public GenerationType type;
		public enum GenerationType
		{
			RingVertical, //Spawns around a ring
			RingFlat, //Spawns around a ring
 			Line, //Spawn linearly
			LineUp, //Spawn Upwards
			Path //Spawn linearly along a path
		}

		[Export(PropertyHint.Range, "0, 12")]
		public float spacing;
		[Export(PropertyHint.Range, "0, 1")]
		public float ringRatio = 1f;
		[Export]
		public NodePath path;
		[Export]
		public Curve pathHInterpolationCurve;
		[Export]
		public Curve pathVInterpolationCurve;

		public override void _Process(float _)
		{
			if (!Engine.EditorHint) return;

			if (generate)
			{
				generate = false;

				for (int i = 0; i < GetChildCount(); i++)
					GetChild(i).QueueFree();

				switch (type)
				{
					case GenerationType.RingVertical:
						if (amount == 1)
						{
							Spawn(Vector3.Zero);
							break;
						}

						float interval = Mathf.Tau * ringRatio / (ringRatio == 1 ? amount : amount - 1);
						for (int i = 0; i < amount; i++)
							Spawn(Vector3.Left.Rotated(Vector3.Forward, (interval * i)).Normalized() * spacing);

						break;
					case GenerationType.RingFlat:
						if (amount == 1)
						{
							Spawn(Vector3.Zero);
							break;
						}

						interval = Mathf.Tau * ringRatio / (ringRatio == 1 ? amount : amount - 1);
						for (int i = 0; i < amount; i++)
							Spawn(Vector3.Forward.Rotated(Vector3.Up, (interval * i)).Normalized() * spacing);

						break;
					case GenerationType.Line:
						for (int i = 0; i < amount; i++)
						{
							Vector3 offset = new Vector3(pathHInterpolationCurve.Interpolate((float)i / (amount - 1)), pathVInterpolationCurve.Interpolate((float)i / (amount - 1)), 0);
							Spawn(Vector3.Forward * i * spacing + offset);
						}
						break;
					case GenerationType.LineUp:
						for (int i = 0; i < amount; i++)
						{
							Vector3 offset = new Vector3(pathHInterpolationCurve.Interpolate((float)i / (amount - 1)), pathVInterpolationCurve.Interpolate((float)i / (amount - 1)), 0);
							Spawn(Vector3.Forward * i * spacing + offset);
						}
						break;
					case GenerationType.Path:
						if (path.IsEmpty())
						{
							GD.PrintErr("No Path Provided.");
							break;
						}

						Path _path = GetNode<Path>(path);
						PathFollow _follow = new PathFollow
						{
							RotationMode = PathFollow.RotationModeEnum.Oriented
						};
						_path.AddChild(_follow);
						_follow.Offset = _path.Curve.GetClosestOffset(GlobalTranslation - _path.GlobalTranslation);

						for (int i = 0; i < amount; i++)
						{
							_follow.HOffset = pathHInterpolationCurve.Interpolate((float)i / (amount - 1));
							_follow.VOffset = pathVInterpolationCurve.Interpolate((float)i / (amount - 1));
							Spawn(_follow.GlobalTranslation, true);
							_follow.Offset += spacing;
						}

						_follow.QueueFree();
						break;
				}
			}
		}

		private void Spawn(Vector3 pos, bool globalPosition = default)
		{
			Spatial obj = targetScene.Instance() as Spatial;
			AddChild(obj);
			obj.Owner = GetTree().EditedSceneRoot;

			if (globalPosition)
			{
				Transform t = obj.GlobalTransform;
				t.origin = pos;
				obj.GlobalTransform = t;
			}
			else
			{
				Transform t = obj.Transform;
				t.origin = pos;
				obj.Transform = t;
			}
		}
	}
}
