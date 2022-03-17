using Godot;

namespace Project.Gameplay
{
	public abstract class StageObject : Area
	{
		public CharacterController Character;

		protected struct SpawnData
		{
			public Node parentNode;
			public Transform spawnTransform;
			public void UpdateSpawnData(Spatial s)
			{
				if (!s.IsInsideTree()) return;

				this.parentNode = s.GetParent();
				this.spawnTransform = s.GlobalTransform;
			}
		}
		private SpawnData spawnData;

		[Signal]
		private delegate void OnSpawned(StageObject o);
		[Signal]
		private delegate void OnDespawned(StageObject o);

		public abstract bool IsRespawnable();

		public override void _Ready() => SetUp();

		public virtual void SetUp()
		{
			if (!IsRespawnable()) return;

			spawnData.UpdateSpawnData(this);

			Connect(nameof(OnSpawned), StageManager.instance, nameof(StageManager.instance.OnObjectSpawned));
			Connect(nameof(OnDespawned), StageManager.instance, nameof(StageManager.instance.OnObjectDespawned));
			Spawn();
		}

		public virtual void Spawn()
		{
			if (!IsInsideTree())
				spawnData.parentNode.AddChild(this);

			GlobalTransform = spawnData.spawnTransform;
			EmitSignal(nameof(OnSpawned), this);
		}

		public virtual void Despawn()
		{
			GetParent().CallDeferred("remove_child", this);
			EmitSignal(nameof(OnDespawned), this);
		}

		public virtual void OnEnter() { }
		public virtual void OnStay() { }
		public virtual void OnExit() { }
	}
}
