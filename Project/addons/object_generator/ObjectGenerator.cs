using Godot;

//Tool for generating groups of objects
namespace Project.Editor
{
	[Tool]
	public partial class ObjectGenerator : Node3D
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
			Path3D //Spawn linearly along a path
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

		public override void _Process(double _)
		{
			if (!Engine.IsEditorHint()) return;

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
							Vector3 offset = new Vector3(pathHInterpolationCurve.Sample((float)i / (amount - 1)), pathVInterpolationCurve.Sample((float)i / (amount - 1)), 0);
							Spawn(Vector3.Forward * i * spacing + offset);
						}
						break;
					case GenerationType.LineUp:
						for (int i = 0; i < amount; i++)
						{
							Vector3 offset = new Vector3(pathHInterpolationCurve.Sample((float)i / (amount - 1)), pathVInterpolationCurve.Sample((float)i / (amount - 1)), 0);
							Spawn(Vector3.Forward * i * spacing + offset);
						}
						break;
					case GenerationType.Path3D:
						if (path.IsEmpty)
						{
							GD.PrintErr("No Path3D Provided.");
							break;
						}

						Path3D _path = GetNode<Path3D>(path);
						PathFollow3D _follow = new PathFollow3D
						{
							RotationMode = PathFollow3D.RotationModeEnum.Oriented
						};
						_path.AddChild(_follow);
						_follow.Progress = _path.Curve.GetClosestOffset(GlobalPosition - _path.GlobalPosition);

						for (int i = 0; i < amount; i++)
						{
							if (pathHInterpolationCurve != null)
								_follow.HOffset = pathHInterpolationCurve.Sample((float)i / (amount - 1));
							else
								_follow.HOffset = (_follow.GlobalTransform.basis * (_follow.GlobalPosition - GlobalPosition)).x; //XFormInv

							if (pathVInterpolationCurve != null)
								_follow.VOffset = pathVInterpolationCurve.Sample((float)i / (amount - 1));
							else
								_follow.VOffset = (_follow.GlobalTransform.basis * (_follow.GlobalPosition - GlobalPosition)).y; //XFormInv

							Spawn(_follow.GlobalPosition, true);
							_follow.Progress += spacing;
							_follow.HOffset = _follow.VOffset = 0f;
						}

						_follow.QueueFree();
						break;
				}
			}
		}

		private void Spawn(Vector3 pos, bool globalPosition = default)
		{
			Node3D obj = targetScene.Instantiate<Node3D>();
			AddChild(obj);
			obj.Owner = GetTree().EditedSceneRoot;

			if (globalPosition)
				GlobalPosition = pos;
			else
				obj.Position = pos;
		}
	}
}
