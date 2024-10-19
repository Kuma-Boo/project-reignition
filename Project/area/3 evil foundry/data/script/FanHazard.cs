using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

public partial class FanHazard : Hazard
{
	[Export]
	private float rotationsPerSecond;
	[Export]
	private bool playSFX;

	[ExportGroup("Components")]
	[Export]
	private NodePath root;
	private Node3D _root;
	[Export]
	private NodePath sfx;
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
		_root.Rotation += Vector3.Forward * rotationsPerSecond * Mathf.Tau * PhysicsManager.physicsDelta;
		ProcessCollision();
	}
}
