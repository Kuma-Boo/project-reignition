using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Gameplay.Hazards
{
	/// <summary>
	/// Continuously spawns spike balls at a given rate.
	/// </summary>
	public partial class SpikeBallSpawner : Node3D
	{
		public partial class SpikeBallData : GodotObject
		{
			/// <summary> Spikeball's rigidbody. </summary>
			public RigidBody3D rigidbody;
			/// <summary> Spikeball's animator. </summary>
			public AnimationPlayer animator;

			/// <summary> Is this spikeball currently spawned? </summary>
			public bool isSpawned;
			/// <summary> Spikeball's current lifetime. </summary>
			public float lifetime;
			/// <summary> How long should the spikeball last? </summary>
			public float maxLifetime;

			public void Spawn()
			{
				rigidbody.Visible = true;
				rigidbody.ProcessMode = ProcessModeEnum.Inherit;

				rigidbody.LinearVelocity = Vector3.Zero;
				rigidbody.AngularVelocity = Vector3.Zero;
				rigidbody.Transform = Transform3D.Identity;
				animator.Play("spawn");

				lifetime = 0;
				isSpawned = true;
			}

			public void Despawn()
			{
				isSpawned = false;
				rigidbody.Visible = false;
				rigidbody.ProcessMode = ProcessModeEnum.Disabled;
			}

			public void Process()
			{
				if (!isSpawned) return;

				if (lifetime < maxLifetime)
				{
					lifetime += PhysicsManager.physicsDelta;
					if (lifetime >= maxLifetime)
						animator.Play("despawn");
				}
				else if (!animator.IsPlaying()) //Wait until despawn animation finishes
					Despawn();
			}
		}

		/// <summary> Timer to keep track of spawn interval. </summary>
		private float timer;

		[Export]
		public bool isPaused;

		[Export]
		/// <summary> The spikeball to make copies of. </summary>
		public RigidBody3D spikeBall;
		private readonly List<SpikeBallData> spikeBallPool = new List<SpikeBallData>();

		[Export]
		/// <summary> How many spike balls can be pooled. </summary>
		public int maxSpawnAmount;

		[Export]
		/// <summary> How long should spike balls last? </summary>
		public float lifetime;

		/// <summary> How frequently to spawn spike balls. </summary>
		[Export]
		public float spawnInterval;

		// Called when the node enters the scene tree for the first time.
		public override void _Ready()
		{
			spikeBall.Visible = false;
			spikeBall.ProcessMode = ProcessModeEnum.Disabled;

			for (int i = 0; i < maxSpawnAmount; i++)
			{
				RigidBody3D rb = spikeBall.Duplicate() as RigidBody3D;
				spikeBallPool.Add(new SpikeBallData()
				{
					rigidbody = rb,
					animator = rb.GetNode<AnimationPlayer>("AnimationPlayer"),
					maxLifetime = lifetime
				});
				spikeBallPool[i].Despawn();
				AddChild(rb);
			}

			LevelSettings.instance.ConnectUnloadSignal(this);
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

			for (int i = 0; i < spikeBallPool.Count; i++)
				spikeBallPool[i].Process();
		}

		/// <summary>
		/// Called when the timer emits. Attempts to spawn a spikeball.
		/// </summary>
		public void SpawnBall()
		{
			for (int i = 0; i < spikeBallPool.Count; i++)
			{
				if (!spikeBallPool[i].isSpawned)
				{
					spikeBallPool[i].Spawn();
					break;
				}
			}
		}


		/// <summary>
		/// Prevent memory leaks
		/// </summary>
		public void Unload()
		{
			for (int i = 0; i < spikeBallPool.Count; i++)
			{
				spikeBallPool[i].rigidbody.QueueFree();
				spikeBallPool[i].Free();
			}

			spikeBallPool.Clear();
		}
	}
}
