using Godot;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay.Hazards;

public partial class MissileGenerator : Node3D
{
	/// <summary> Should the spawner itself be hidden? </summary>
	[Export] private bool isInvisibleSpawner;
	/// <summary> Should missiles be purely visual? </summary>
	[Export] private bool disableHitboxes;
	[Export(PropertyHint.Range, "0,10,0.1, or_greater")] private float missileLifetime = 5.0f;
	[Export(PropertyHint.Range, "0,30,0.1, or_greater")] private float missleSpeed = 20.0f;
	[Export(PropertyHint.Range, "0,1,0.1, or_greater")] private float spawnInterval = 3.0f;
	[Export] private float spawnTimer = 2.0f;

	[ExportGroup("Components")]
	[Export] private PackedScene missileScene;
	[Export(PropertyHint.NodePathValidTypes, "Node3D")] private NodePath spawnTransform;
	private Node3D _spawnTransform;

	private readonly int DefaultPoolSize = 3;
	private readonly Queue<Missile> missilePool = [];

	public override void _Ready()
	{
		if (isInvisibleSpawner)
			GetChild<Node3D>(0).Visible = false;
		_spawnTransform = GetNode<Node3D>(spawnTransform);

		for (int i = 0; i < DefaultPoolSize; i++)
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
		missile.DisableAutoRespawn = true;
		missile.Disabled += () => QueueMissile(missile);

		missile.SetLifetime(missileLifetime);
		missile.SetSpeed(missleSpeed);
		missile.SetHitboxState(disableHitboxes);
		missile.Disable();

		AddChild(missile);
	}

	private void QueueMissile(Missile missile) => missilePool.Enqueue(missile);

	private void SpawnMissile()
	{
		if (missilePool.Count == 0)
			PoolMissile();

		Missile missile = missilePool.Dequeue();
		missile.UpdateSpawnTransform(_spawnTransform.Transform);
		missile.Respawn();
	}
}
