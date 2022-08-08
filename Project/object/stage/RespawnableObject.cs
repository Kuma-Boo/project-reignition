using Godot;

namespace Project.Gameplay
{
	//Parent class of all stage objects.
	public abstract class RespawnableObject : Spatial
	{
		public CharacterController Character => CharacterController.instance;

		protected struct SpawnData
		{
			public Node parentNode;
			public Transform spawnTransform;
			public void UpdateSpawnData(Spatial s)
			{
				if (!s.IsInsideTree()) return;

				parentNode = s.GetParent();
				spawnTransform = s.GlobalTransform;
			}
		}
		protected SpawnData spawnData;

		public virtual bool IsRespawnable() => false; //Set to True for enemies

		public override void _Ready() => SetUp();

		public virtual void SetUp()
		{
			if (!IsRespawnable()) return;

			spawnData.UpdateSpawnData(this);
			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public virtual void Respawn()
		{
			if (IsInsideTree()) return;

			spawnData.parentNode.AddChild(this);
			GlobalTransform = spawnData.spawnTransform;
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred("remove_child", this);
		}

		public virtual void OnEnter() { }
		public virtual void OnStay() { }
		public virtual void OnExit() { }

		public virtual void OnEntered(Area a) { }
		public virtual void OnExited(Area a) { }
	}
}
