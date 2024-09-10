using System;
using Godot;
using Godot.Collections;
using Project.Gameplay;
using Project.Gameplay.Objects;

namespace Project.CustomNodes;

[Tool]
/// <summary>
/// Tool for generating groups of stage objects.
/// </summary>
public partial class ObjectGenerator : Node3D
{
	private PackedScene source;
	private int currentChildCount;
	[Export(PropertyHint.Range, "0,100,1,or_greater")]
	public int amount = 0;
	private float IntervalDivider => amount - 1;

	public SpawnShape shape;
	public enum SpawnShape
	{
		Line, // Spawn linearly
		Ring, // Spawns around a ring
		Path, // Spawn linearly along a path
		Launcher // Spawn along a launcher path
	}
	[Export]
	public SpawnOrientation orientation;
	public enum SpawnOrientation
	{
		Horizontal,
		Vertical,
	}

	[Export(PropertyHint.Range, "0,20,0.1")]
	public float spacing = 1f;
	public float ringRatio = 1f;

	public NodePath _path;
	public Path3D path;
	public Launcher.LaunchDirection launchDirection;
	public float launchDistance;
	public float launchEndHeight;
	public float launchMiddleHeight;
	/// <summary> Use this if the default sampling is inaccurate. </summary>
	[Export(PropertyHint.Range, "-64,64,0.1")]
	public float progressOffset;
	/// <summary> Disable following the path's Y position? </summary>
	[Export]
	public bool disablePathY;
	[Export]
	public Curve hOffsetCurve;
	[Export]
	public Curve vOffsetCurve;

	// public override Array<Dictionary> _GetPropertyList()
	// {
	// 	Array<Dictionary> properties =
	// 	[
	// 		// ExtensionMethods.CreateProperty("Generate", Variant.Type.Bool),
	// 			ExtensionMethods.CreateProperty("Source", Variant.Type.Object, PropertyHint.ResourceType, "PackedScene"),
	// 			// ExtensionMethods.CreateProperty("Amount", Variant.Type.Int, PropertyHint.Range, "0,64"),
	// 			ExtensionMethods.CreateProperty("Shape", Variant.Type.Int, PropertyHint.Enum, shape.EnumToString()),
	// 		];

	// 	if (shape == SpawnShape.Path)
	// 	{
	// 		properties.Add(ExtensionMethods.CreateProperty("Path", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Path3D"));
	// 		// properties.Add(ExtensionMethods.CreateProperty("Path Progress Offset", Variant.Type.Float, PropertyHint.Range, "-64,64,.1"));
	// 		// properties.Add(ExtensionMethods.CreateProperty("Disable Path Y", Variant.Type.Bool));
	// 	}
	// 	else if (shape == SpawnShape.Launcher)
	// 	{
	// 		properties.Add(ExtensionMethods.CreateProperty("Launcher Type", Variant.Type.Int, PropertyHint.Enum, launchDirection.EnumToString()));
	// 		properties.Add(ExtensionMethods.CreateProperty("Launch Distance", Variant.Type.Float));
	// 		properties.Add(ExtensionMethods.CreateProperty("Launch Middle Height", Variant.Type.Float));
	// 		properties.Add(ExtensionMethods.CreateProperty("Launch End Height", Variant.Type.Float));
	// 		properties.Add(ExtensionMethods.CreateProperty("Launch Offset", Variant.Type.Float, PropertyHint.Range, "0,1"));
	// 		properties.Add(ExtensionMethods.CreateProperty("Launch Ratio", Variant.Type.Float, PropertyHint.Range, "0,1"));
	// 	}
	// 	else
	// 	{
	// 		// properties.Add(ExtensionMethods.CreateProperty("Orientation", Variant.Type.Int, PropertyHint.Enum, orientation.EnumToString()));
	// 	}

	// 	if (shape == SpawnShape.Ring)
	// 	{
	// 		properties.Add(ExtensionMethods.CreateProperty("Ring Size", Variant.Type.Float, PropertyHint.Range, "0,12,.1"));
	// 		properties.Add(ExtensionMethods.CreateProperty("Ring Ratio", Variant.Type.Float, PropertyHint.Range, "0,1,.1"));
	// 	}
	// 	else if (shape != SpawnShape.Launcher)
	// 	{
	// 		// properties.Add(ExtensionMethods.CreateProperty("Spacing", Variant.Type.Float, PropertyHint.Range, "0,12,.1"));
	// 		properties.Add(ExtensionMethods.CreateProperty("Horizontal Offset", Variant.Type.Object, PropertyHint.ResourceType, "Curve"));
	// 		properties.Add(ExtensionMethods.CreateProperty("Vertical Offset", Variant.Type.Object, PropertyHint.ResourceType, "Curve"));
	// 	}

	// 	return properties;
	// }
	public override void _ValidateProperty(Dictionary property)
	{
		var propName = property["name"].AsString();
		// hide spacing if type is launcher
		var _hide = false;
		GD.Print(propName, shape);
		if ((propName == "spacing" && shape == SpawnShape.Launcher) || (propName == "orientation" && shape <= SpawnShape.Ring))
		{
			_hide = true;
		}
		if (_hide)
		{
			GD.Print("hide ", propName);
			property["usage"] = Variant.From((int)PropertyUsageFlags.NoEditor);
		}
	}
	// public override bool _Set(StringName property, Variant value)
	// {
	// 	switch ((string)property)
	// 	{
	// 		case "Source":
	// 			source = (PackedScene)value;
	// 			break;
	// 		case "Amount":
	// 			amount = (int)value;
	// 			break;
	// 		case "Shape":
	// 			shape = (SpawnShape)(int)value;
	// 			NotifyPropertyListChanged();
	// 			break;
	// 		case "Launcher Type":
	// 			launchDirection = (Launcher.LaunchDirection)(int)value;
	// 			break;
	// 		case "Launch Distance":
	// 			launchDistance = (float)value;
	// 			break;
	// 		case "Launch Middle Height":
	// 			launchMiddleHeight = (float)value;
	// 			break;
	// 		case "Launch End Height":
	// 			launchEndHeight = (float)value;
	// 			break;
	// 		case "Launch Ratio":
	// 		case "Ring Ratio":
	// 			ringRatio = (float)value;
	// 			break;
	// 		case "Launch Offset":
	// 		case "Ring Size":
	// 		case "Spacing":
	// 			spacing = (float)value;
	// 			break;
	// 		case "Path":
	// 			_path = (NodePath)value;
	// 			path = GetNodeOrNull<Path3D>(_path);
	// 			break;
	// 		default:
	// 			return false;
	// 	}

	// 	return true;
	// }

	// public override Variant _Get(StringName property)
	// {
	// 	switch ((string)property)
	// 	{
	// 		case "Source":
	// 			return source;
	// 		case "Amount":
	// 			return amount;
	// 		case "Shape":
	// 			return (int)shape;
	// 		case "Launcher Type":
	// 			return (int)launchDirection;
	// 		case "Launch Distance":
	// 			return launchDistance;
	// 		case "Launch Middle Height":
	// 			return launchMiddleHeight;
	// 		case "Launch End Height":
	// 			return launchEndHeight;
	// 		case "Launch Ratio":
	// 		case "Ring Ratio":
	// 			return ringRatio;
	// 		case "Launch Offset":
	// 		case "Ring Size":
	// 		case "Spacing":
	// 			return spacing;
	// 		case "Path":
	// 			return _path;
	// 		default:
	// 			break;
	// 	}

	// 	return base._Get(property);
	// }

	private void DeleteChildren()
	{
		// Delete old children
		for (int i = 0; i < GetChildCount(); i++)
		{
			var child = GetChild(i);
			child.Name = "Deletion" + i;
			child.QueueFree();
		}
	}
	private void CreateChildren()
	{
		var root = GetTree().EditedSceneRoot;
		for (var i = 0; i < amount; i++)
		{
			Node3D obj = source.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
			obj.Name = "Child" + i.ToString("00"); //Set name
			currentChildCount++;
			AddChild(obj);
			obj.Owner = root;
			obj.SetMeta("_edit_lock_", true);
		}
	}

	public void GenerateChildren()
	{
		if (amount != GetChildCount())
		{
			DeleteChildren();
			CreateChildren();
		}
		AlignChildren();
	}

	public void AlignChildren()
	{
		switch (shape)
		{
			case SpawnShape.Line:
				AlignLine();
				break;
			case SpawnShape.Ring:
				AlignRing();
				break;
			case SpawnShape.Path:
				AlignPath();
				break;
			case SpawnShape.Launcher:
				AlignLauncher();
				break;
		}
	}

	private void AlignRing()
	{
		float interval = Mathf.Tau * ringRatio / (ringRatio == 1 ? amount : amount - 1);
		Vector3 rotationAxis = orientation == SpawnOrientation.Horizontal ? Vector3.Up : Vector3.Forward;
		for (int i = 0; i < amount; i++)
			GetChild<Node3D>(i).Position = Vector3.Left.Rotated(rotationAxis, interval * i).Normalized() * spacing;
	}

	private void AlignLine()
	{
		Vector3 forwardDirection = orientation == SpawnOrientation.Horizontal ? Vector3.Forward : Vector3.Up;
		Vector3 upDirection = orientation == SpawnOrientation.Horizontal ? Vector3.Up : Vector3.Back;
		float hOffset = 0;
		float vOffset = 0;

		for (int i = 0; i < GetChildCount(); i++)
		{
			if (hOffsetCurve != null)
				hOffset = hOffsetCurve.Sample(i / IntervalDivider);

			if (vOffsetCurve != null)
				vOffset = vOffsetCurve.Sample(i / IntervalDivider);
			GetChild<Node3D>(i).Position = forwardDirection * i * spacing + Vector3.Right * hOffset + upDirection * vOffset;
		}
	}

	private void AlignPath()
	{
		if (_path == null)
		{
			GD.PrintErr("No Path Provided.");
			return;
		}
		path = GetNodeOrNull<Path3D>(_path);

		PathFollow3D follow = new()
		{
			RotationMode = PathFollow3D.RotationModeEnum.Xyz,
			UseModelFront = true
		};

		path.AddChild(follow);
		follow.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition) + progressOffset;

		Vector3 offset = follow.GlobalTransform.Inverse().Basis * (GlobalPosition - follow.GlobalPosition);
		for (int i = 0; i < amount; i++)
		{
			follow.HOffset = offset.X;
			follow.VOffset = offset.Y;

			if (hOffsetCurve != null)
				follow.HOffset += hOffsetCurve.Sample((float)i / IntervalDivider);

			if (vOffsetCurve != null)
				follow.VOffset += vOffsetCurve.Sample(i / IntervalDivider);

			Vector3 spawnPosition = follow.GlobalPosition;
			if (disablePathY)
				spawnPosition.Y = GlobalPosition.Y;

			GetChild<Node3D>(i).GlobalPosition = spawnPosition;
			follow.Progress += spacing;
		}

		follow.QueueFree();
	}

	public virtual Vector3 GetLaunchDirection()
	{
		if (launchDirection == Launcher.LaunchDirection.Forward)
			return this.Forward();
		else if (launchDirection == Launcher.LaunchDirection.Flatten)
			return this.Up().RemoveVertical().Normalized();

		return this.Up();
	}

	private void AlignLauncher()
	{
		Vector3 startPosition = GlobalPosition;
		Vector3 endPosition = startPosition + (GetLaunchDirection() * launchDistance) + (Vector3.Up * launchEndHeight);

		LaunchSettings settings = LaunchSettings.Create(startPosition, endPosition, launchMiddleHeight);

		for (int i = 0; i < amount; i++)
		{
			float t = i / (float)amount;
			t *= ringRatio;
			t += spacing;
			GetChild<Node3D>(i).Position = settings.InterpolatePositionRatio(t);
		}
	}
}
