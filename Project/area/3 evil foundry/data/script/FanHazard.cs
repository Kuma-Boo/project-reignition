using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class FanHazard : Hazard
{
	/// <summary> How long it takes to make a single rotation, in seconds. </summary>
	[Export] private float rotationTime;
	[Export] private float timebreakMultiplier = 0.5f;
	[Export] private bool playSFX;

	[ExportGroup("Components")]
	[Export] private Node3D root;
	[Export] private AudioStreamPlayer3D sfx;

	public override void _Ready()
	{
		if (playSFX)
			sfx.Play();
	}

	public override void _PhysicsProcess(double _)
	{
		if (!Mathf.IsZeroApprox(rotationTime))
			ProcessRotation();

		ProcessCollision();
	}

	private void ProcessRotation()
	{
		float rotationSpeed = 1f / rotationTime;
		if (Player.Skills.IsTimeBreakActive)
			rotationSpeed *= timebreakMultiplier;
		root.Rotation += Vector3.Back * Mathf.Tau * rotationSpeed * PhysicsManager.physicsDelta;
	}
}
