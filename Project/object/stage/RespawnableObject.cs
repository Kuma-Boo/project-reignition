using Godot;

namespace Project.Gameplay
{
	//Parent class of all stage objects.
	public abstract class RespawnableObject : Spatial
	{
		protected CharacterController Character => CharacterController.instance;

		private struct SpawnData
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
		private SpawnData spawnData;
		protected virtual bool IsRespawnable() => false; //Set to True for enemies

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			if (!IsRespawnable()) return;

			spawnData.UpdateSpawnData(this);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public virtual void Respawn()
		{
			if (!IsInsideTree())
				spawnData.parentNode.AddChild(this);

			GlobalTransform = spawnData.spawnTransform;
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred("remove_child", this);
		}
	}
}
