using Godot;
using Godot.Collections;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Ivy : Launcher
{
	[Export(PropertyHint.Range, "2, 10")]
	public int Length
	{
		get => length;
		set => SetLength(value);
	}
	private int length;

	[Export(PropertyHint.Range, "0,45,1")]
	private float maxRotation;
	[Export(PropertyHint.Range, "-1,1")]
	private float SwingRatio
	{
		get => swingRatio;
		set => SetRotation(value);
	}
	private float swingRatio;

	[Export]
	private NodePath root;
	private Node3D _root;
	[Export]
	private PackedScene ivyScene;
	private Array<Node3D> ivyLinks = [];

	public override void _Ready()
	{
		Initialize();
	}

	public void SetLength(int newLength)
	{
		length = newLength;
		Initialize();
	}

	public void SetRotation(float ratio)
	{
		swingRatio = ratio;
		float rotation = maxRotation * swingRatio;

		for (int i = 0; i < ivyLinks.Count; i++)
			ivyLinks[i].RotationDegrees = Vector3.Left * rotation;
	}

	private void Initialize()
	{
		_root = GetNodeOrNull<Node3D>(root);

		if (_root == null || launchPoint == null)
		{
			GD.PushError("Ivy references not found. (Check Launch Point).");
			return;
		}

		if (ivyLinks.Count == 0)
			ivyLinks.Add(_root);

		launchPoint.GetParent().RemoveChild(launchPoint);

		// Resize ivy to the proper length
		UpdateIvyLength();

		// Parent trigger to the last link
		ivyLinks[ivyLinks.Count - 1].AddChild(launchPoint);
		launchPoint.Transform = new()
		{
			Origin = Vector3.Down * .5f,
			Basis = Basis.Identity,
		};
	}

	private void UpdateIvyLength()
	{
		if (ivyLinks.Count > length)
		{
			// Since every ivy link is parented, we only need to delete one.
			ivyLinks[length].QueueFree();
			ivyLinks.Resize(length);
			return;
		}

		if (ivyScene == null)
			return;

		// Add ivy individually as needed
		while (ivyLinks.Count < length)
		{
			Node3D linkNode = ivyScene.Instantiate<Node3D>();
			ivyLinks[ivyLinks.Count - 1].AddChild(linkNode); // Add as a child so rotations carry over
			linkNode.Position = Vector3.Down;
			ivyLinks.Add(linkNode);
		}
	}
}
