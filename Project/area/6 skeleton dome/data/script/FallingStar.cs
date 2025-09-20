using Godot;
using System;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay.Objects;

/// <summary>
/// For the falling stars in Skeleton Dome's spiral section.
/// </summary>
public partial class FallingStar : Node3D
{
	[Signal] private delegate void RestartedEventHandler();

	[Export] private float lifetime;
	[Export] private float travelDistance;
	[Export] private float delay;
	[Export] private GroupGpuParticles3D particles;

	private Vector3 spawnPosition;
	private float currentLifetime;
	private readonly float FadeoutLength = 1f;

	public override void _Ready()
	{
		spawnPosition = GlobalPosition;
		currentLifetime = -delay;
	}

	public override void _PhysicsProcess(double _)
	{
		if (currentLifetime < 0f)
		{
			currentLifetime += PhysicsManager.physicsDelta;
			if (currentLifetime >= 0f)
				Respawn();
			return;
		}

		currentLifetime += PhysicsManager.physicsDelta;
		GlobalPosition = CalculateCurrentPosition();

		if (currentLifetime > lifetime)
		{
			if (!particles.Emitting && currentLifetime > lifetime + FadeoutLength)
			{
				Respawn();
				return;
			}

			particles.SetEmitting(false);
		}
	}

	private Vector3 CalculateCurrentPosition()
	{
		Vector3 returnPosition = spawnPosition;
		returnPosition += Vector3.Down * travelDistance * (currentLifetime / lifetime);
		returnPosition += (this.Right() + this.Forward()) * Mathf.Sin(currentLifetime);
		return returnPosition;
	}

	/// <summary> Return to original position and start falling again. </summary>
	private void Respawn()
	{
		currentLifetime = 0f;
		GlobalPosition = spawnPosition;
		ResetPhysicsInterpolation();
		particles.SetEmitting(true);
		EmitSignal(SignalName.Restarted);
	}
}
