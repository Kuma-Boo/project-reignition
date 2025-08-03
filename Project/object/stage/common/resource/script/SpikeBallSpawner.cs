using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Hazards;

/// <summary>
/// Continuously spawns spike balls at a given rate.
/// </summary>
public partial class SpikeBallSpawner : Node3D
{
	[Export(PropertyHint.Range, "0,1")] private float initialTimerRatio;

	/// <summary> Timer to keep track of spawn interval. </summary>
	private float timer;

	[Export] private bool isPaused;
	/// <summary> The spikeball to make copies of. </summary>
	[Export] private PackedScene spikeBallScene;

	private readonly List<SpikeBallPhysics> spikeBallPool = [];

	/// <summary> How many spike balls can be pooled. </summary>
	[Export] private int maxSpawnAmount;
	/// <summary> How long should spike balls last? </summary>
	[Export] private float lifetime;
	/// <summary> How frequently to spawn spike balls. </summary>
	[Export] private float spawnInterval;
	/// <summary> Initial speed of spike balls. </summary>
	[Export] private float initialVelocity;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Hide the editor mesh
		GetChild<Node3D>(0).Visible = false;
		timer = initialTimerRatio * spawnInterval;

		// Pool spike balls
		for (int i = 0; i < maxSpawnAmount; i++)
		{
			SpikeBallPhysics spikeBall = spikeBallScene.Instantiate<SpikeBallPhysics>();
			spikeBall.MaxLifetime = lifetime;
			spikeBallPool.Add(spikeBall);

			spikeBallPool[i].Despawn();
			AddChild(spikeBall);
		}

		StageSettings.Instance.Unloaded += Unload;
	}

	public override void _PhysicsProcess(double _)
	{
		if (isPaused) return;

		timer += PhysicsManager.physicsDelta;

		if (timer >= spawnInterval)
		{
			timer -= spawnInterval;
			SpawnBall();
		}
	}

	/// <summary>
	/// Called when the timer emits. Attempts to spawn a spikeball.
	/// </summary>
	public void SpawnBall()
	{
		for (int i = 0; i < spikeBallPool.Count; i++)
		{
			if (!spikeBallPool[i].IsSpawned)
			{
				spikeBallPool[i].Spawn();
				spikeBallPool[i].Transform = Transform3D.Identity;
				spikeBallPool[i].LinearVelocity = this.Forward() * initialVelocity;
				break;
			}
		}
	}

	/// <summary> Prevent memory leaks. </summary>
	public void Unload() => spikeBallPool.Clear();
}