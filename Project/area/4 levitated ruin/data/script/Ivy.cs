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
	private float LaunchRatio
	{
		get => launchRatio;
		set => SetRotation(value);
	}
	[Export]
	private bool isSwingingForward;

	[Export]
	private NodePath root;
	private Node3D _root;
	[Export]
	private PackedScene ivyScene;
	private Array<Node3D> ivyLinks = [];

	public override float GetLaunchRatio()
	{
		if (isSwingingForward)
			return Mathf.Clamp(LaunchRatio + 1, 0f, 1f);

		if (LaunchRatio <= 0)
			return 0;

		return LaunchRatio;
	}

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
		launchRatio = ratio;
		float rotation = maxRotation * launchRatio;

		for (int i = 0; i < ivyLinks.Count; i++)
			ivyLinks[i].RotationDegrees = Vector3.Left * rotation;

		UpdateAreaPosition();
	}

	/// <summary> Moves the area trigger to the last link's position. </summary>
	private void UpdateAreaPosition()
	{
		launchPoint.GlobalPosition = ivyLinks[ivyLinks.Count - 1].GlobalPosition;
		launchPoint.GlobalPosition -= ivyLinks[ivyLinks.Count - 1].Up() * .5f;
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

		// Resize ivy to the proper length
		UpdateIvyLength();
		UpdateAreaPosition();
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
