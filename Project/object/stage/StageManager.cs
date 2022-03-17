using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay
{
	//Manager responsible for spawning/despawning objects
	public class StageManager : Spatial
	{
		public static StageManager instance;

		[Export]
		public int maxRingCount = 100; //Maximum amount of collectable rings for this stage

		[Export]
		public SphereShape pearlCollisionShape;
		[Export]
		public SphereShape richPearlCollisionShape;

		public RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		private const float PEARL_ATTRACTOR_MULTIPLIER = .5f;

		private const int ENEMY_PEARL_AMOUNT = 16; //How many pearls are obtained when defeating an enemy

		public override void _Ready()
		{
			instance = this; //Always override previous instance

			//TODO Expand hitbox if skill is equipped
			pearlCollisionShape.Radius = PEARL_NORMAL_COLLISION;
			richPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION;

			if (SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.PearlAttractor))
			{
				pearlCollisionShape.Radius *= PEARL_ATTRACTOR_MULTIPLIER;
				richPearlCollisionShape.Radius *= PEARL_ATTRACTOR_MULTIPLIER;
			}
		}

		private readonly List<StageObject> spawnedObjects = new List<StageObject>();
		private readonly List<StageObject> despawnedObjects = new List<StageObject>();

		public void OnObjectSpawned(StageObject o)
		{
			if (!spawnedObjects.Contains(o))
				spawnedObjects.Add(o);
		}

		public void OnObjectDespawned(StageObject o)
		{
			if (!despawnedObjects.Contains(o))
				despawnedObjects.Add(o);
		}

		public override void _ExitTree()
		{
			//Prevent memory leaking
			for (int i = 0; i < spawnedObjects.Count; i++)
				spawnedObjects[i].QueueFree();

			for (int i = 0; i < despawnedObjects.Count; i++)
				despawnedObjects[i].QueueFree();
		}
	}
}
