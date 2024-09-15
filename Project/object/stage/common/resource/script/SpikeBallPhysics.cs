using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards
{
	public partial class SpikeBallPhysics : RigidBody3D
	{
		/// <summary> Spikeball's current lifetime. </summary>
		private float Lifetime { get; set; }
		[Export]
		/// <summary> How long should the spikeball last? </summary>
		public float MaxLifetime { get; set; }
		/// <summary> Is this spikeball currently spawned? </summary>
		public bool IsSpawned { get; private set; }
		/// <summary> Spikeball's animator. </summary>
		[Export]
		private Vector3 startingVelocity;
		[Export]
		private AnimationPlayer animator;

		public override void _Ready() => StageSettings.Instance.ConnectUnloadSignal(this);

		public override void _PhysicsProcess(double _)
		{
			if (!IsSpawned) return;

			if (Lifetime < MaxLifetime)
			{
				Lifetime += PhysicsManager.physicsDelta;
				if (Lifetime >= MaxLifetime)
					animator.Play("despawn");
			}
			else if (!animator.IsPlaying()) //Wait until despawn animation finishes
				Despawn();
		}


		public void Spawn()
		{
			Visible = true;
			ProcessMode = ProcessModeEnum.Inherit;

			LinearVelocity = GlobalBasis * startingVelocity;
			AngularVelocity = Vector3.Zero;
			animator.Play("spawn");

			Lifetime = 0;
			IsSpawned = true;
		}


		public void Despawn()
		{
			IsSpawned = false;
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}

		private void Unload() => QueueFree();
	}
}
