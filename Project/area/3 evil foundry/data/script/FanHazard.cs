using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class FanHazard : Hazard
{
	/// <summary> How long it takes to make a single rotation, in seconds. </summary>
	[Export] private float rotationTime;
	[Export] private bool playSFX;

	[ExportGroup("Components")]
	[Export] private NodePath root;
	private Node3D _root;
	[Export] private NodePath sfx;
	private AudioStreamPlayer3D _sfx;

	public override void _Ready()
	{
		_root = GetNode<Node3D>(root);
		_sfx = GetNode<AudioStreamPlayer3D>(sfx);

		if (playSFX)
			_sfx.Play();
	}

	public override void _PhysicsProcess(double _)
	{
		if (!Mathf.IsZeroApprox(rotationTime))
			_root.Rotation += Vector3.Back * Mathf.Tau * (1f / rotationTime) * PhysicsManager.physicsDelta;

		ProcessCollision();
	}
}
