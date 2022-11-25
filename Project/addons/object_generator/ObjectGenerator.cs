using Godot;
using Godot.Collections;

//Tool for generating groups of objects
namespace Project.Editor
{
	[Tool]
	public partial class ObjectGenerator : Node3D
	{
		private PackedScene source;
		private int amount;

		public SpawnShape shape;
		public enum SpawnShape
		{
			Line, //Spawn linearly
			Ring, //Spawns around a ring
			Path //Spawn linearly along a path
		}

		public SpawnOrientation orientation;
		public enum SpawnOrientation
		{
			Horizontal,
			Vertical,
		}

		public float spacing;
		public float ringRatio = 1f;

		public NodePath _path;
		public Path3D path;
		public float progressOffset; //Used whenever path isn't getting the proper point
		public bool followPathY; //Enable this to use path's Y position
		public Curve hOffsetCurve;
		public Curve vOffsetCurve;

		private int currentChildCount;

		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Generate", Variant.Type.Bool));

			properties.Add(ExtensionMethods.CreateProperty("Source", Variant.Type.Object, PropertyHint.ResourceType, "PackedScene"));
			properties.Add(ExtensionMethods.CreateProperty("Amount", Variant.Type.Int, PropertyHint.Range, "0,64"));

			properties.Add(ExtensionMethods.CreateProperty("Shape", Variant.Type.Int, PropertyHint.Enum, "Line,Ring,Path"));

			if (shape == SpawnShape.Path)
			{
				properties.Add(ExtensionMethods.CreateProperty("Path", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Path3D"));
				properties.Add(ExtensionMethods.CreateProperty("Path Progress Offset", Variant.Type.Float, PropertyHint.Range, "-64,64,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Follow Path Y", Variant.Type.Bool));
			}
			else
				properties.Add(ExtensionMethods.CreateProperty("Orientation", Variant.Type.Int, PropertyHint.Enum, "Horizontal,Vertical"));

			if (shape == SpawnShape.Ring)
			{
				properties.Add(ExtensionMethods.CreateProperty("Ring Size", Variant.Type.Float, PropertyHint.Range, "0,12"));
				properties.Add(ExtensionMethods.CreateProperty("Ring Ratio", Variant.Type.Float, PropertyHint.Range, "0,1,.1"));
			}
			else
			{
				properties.Add(ExtensionMethods.CreateProperty("Spacing", Variant.Type.Float, PropertyHint.Range, "0,12"));
				properties.Add(ExtensionMethods.CreateProperty("Horizontal Offset", Variant.Type.Object, PropertyHint.ResourceType, "Curve"));
				properties.Add(ExtensionMethods.CreateProperty("Vertical Offset", Variant.Type.Object, PropertyHint.ResourceType, "Curve"));
			}

			return properties;
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Generate":
					GenerateChildren();
					break;
				case "Source":
					source = (PackedScene)value;
					break;
				case "Amount":
					amount = (int)value;
					break;
				case "Shape":
					shape = (SpawnShape)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Orientation":
					orientation = (SpawnOrientation)(int)value;
					break;
				case "Ring Ratio":
					ringRatio = (float)value;
					break;
				case "Ring Size":
					spacing = (float)value;
					break;
				case "Spacing":
					spacing = (float)value;
					break;
				case "Path":
					_path = (NodePath)value;
					break;
				case "Path Progress Offset":
					progressOffset = (float)value;
					break;
				case "Follow Path Y":
					followPathY = (bool)value;
					break;

				case "Horizontal Offset":
					hOffsetCurve = (Curve)value;
					break;
				case "Vertical Offset":
					vOffsetCurve = (Curve)value;
					break;

				default:
					return false;
			}

			return true;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Generate":
					return false;
				case "Source":
					return source;
				case "Amount":
					return amount;
				case "Shape":
					return (int)shape;
				case "Orientation":
					return (int)orientation;
				case "Ring Ratio":
					return ringRatio;
				case "Ring Size":
					return spacing;
				case "Spacing":
					return spacing;
				case "Path":
					return _path;
				case "Path Progress Offset":
					return progressOffset;
				case "Follow Path Y":
					return followPathY;

				case "Horizontal Offset":
					return hOffsetCurve;
				case "Vertical Offset":
					return vOffsetCurve;
			}

			return base._Get(property);
		}

		private void GenerateChildren()
		{
			//Delete old children
			for (int i = 0; i < GetChildCount(); i++)
			{
				GetChild(i).Name = "Deletion" + i;
				GetChild(i).QueueFree();
			}
			currentChildCount = 1; //Reset child counter
			float divider = amount - 1;

			switch (shape)
			{
				case SpawnShape.Ring:
					if (amount == 1)
					{
						Spawn(Vector3.Zero);
						break;
					}

					float interval = Mathf.Tau * ringRatio / (ringRatio == 1 ? amount : amount - 1);
					Vector3 rotationBase = orientation == SpawnOrientation.Horizontal ? Vector3.Forward : Vector3.Left;
					Vector3 rotationAxis = orientation == SpawnOrientation.Horizontal ? Vector3.Up : Vector3.Forward;
					for (int i = 0; i < amount; i++)
						Spawn(Vector3.Left.Rotated(rotationAxis, (interval * i)).Normalized() * spacing);
					break;
				case SpawnShape.Line:
					Vector3 forwardDirection = orientation == SpawnOrientation.Horizontal ? Vector3.Forward : Vector3.Up;
					Vector3 upDirection = orientation == SpawnOrientation.Horizontal ? Vector3.Up : Vector3.Back;
					float hOffset = 0;
					float vOffset = 0;

					for (int i = 0; i < amount; i++)
					{
						if (hOffsetCurve != null)
							hOffset = hOffsetCurve.Sample(i / divider);

						if (vOffsetCurve != null)
							vOffset = vOffsetCurve.Sample(i / divider);

						Spawn(forwardDirection * i * spacing + Vector3.Right * hOffset + upDirection * vOffset);
					}
					break;
				case SpawnShape.Path:
					path = GetNodeOrNull<Path3D>(_path);

					if (path == null)
					{
						GD.PrintErr("No Path Provided.");
						break;
					}

					PathFollow3D follow = new PathFollow3D
					{
						RotationMode = PathFollow3D.RotationModeEnum.Oriented
					};
					path.AddChild(follow);
					follow.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition) + progressOffset;

					Vector3 offset = follow.GlobalTransform.Inverse().basis * (GlobalPosition - follow.GlobalPosition);
					follow.HOffset = offset.x;
					follow.VOffset = offset.y;

					for (int i = 0; i < amount; i++)
					{
						if (hOffsetCurve != null)
							follow.HOffset = hOffsetCurve.Sample((float)i / divider);

						Vector3 spawnPosition = follow.GlobalPosition;
						if (vOffsetCurve != null)
						{
							if (followPathY)
								spawnPosition.y += vOffsetCurve.Sample(i / divider);
							else
								spawnPosition.y = GlobalPosition.y + vOffsetCurve.Sample(i / divider);
						}

						Spawn(spawnPosition, true);
						follow.Progress += spacing;
					}

					follow.QueueFree();
					break;
			}
		}

		private void Spawn(Vector3 pos, bool globalPosition = default)
		{
			Node3D obj = source.Instantiate<Node3D>();
			obj.Name = "Child" + currentChildCount.ToString("00"); //Set name
			currentChildCount++;
			AddChild(obj);
			obj.Owner = GetTree().EditedSceneRoot;

			if (globalPosition)
				obj.GlobalPosition = pos;
			else
				obj.Position = pos;
		}
	}
}
