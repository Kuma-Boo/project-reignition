using Godot;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay.Hazards;

public partial class MissileGenerator : Node3D
{
	[Export(PropertyHint.Range, "0,10,0.1, or_greater")] private float missileLifetime = 5.0f;
	[Export(PropertyHint.Range, "0,30,0.1, or_greater")] private float missleSpeed = 20.0f;
	[Export(PropertyHint.Range, "0,1,0.1, or_greater")] private float spawnInterval = 3.0f;
	private float spawnTimer;

	[ExportGroup("Components")]
	[Export] private PackedScene missileScene;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath spawnTransform;
	private Node3D _spawnTransform;
	private Queue<Missile> missilePool = [];

	private readonly int DEFAULT_POOL_SIZE = 5;

	public override void _Ready()
	{
		spawnTimer = spawnInterval;
		_spawnTransform = GetNode<Node3D>(spawnTransform);

		for (int i = 0; i < DEFAULT_POOL_SIZE; i++)
			PoolMissile();
	}

	public override void _PhysicsProcess(double _)
	{
		spawnTimer -= PhysicsManager.physicsDelta;
		if (spawnTimer > 0)
			return;

		SpawnMissile();
		spawnTimer = spawnInterval;
	}

	private void PoolMissile()
	{
		Missile missile = missileScene.Instantiate<Missile>();
		missile.SetLifetime(missileLifetime);
		missile.SetSpeed(missleSpeed);
		missilePool.Enqueue(missile);

		missile.Visible = false;
		missile.ProcessMode = ProcessModeEnum.Disabled;

		AddChild(missile);
	}

	private void SpawnMissile()
	{
		if (missilePool.Count == 0)
			PoolMissile();

		Missile missile = missilePool.Dequeue();
		missile.UpdateSpawnTransform(_spawnTransform.Transform);
		missile.Respawn();
	}
}
