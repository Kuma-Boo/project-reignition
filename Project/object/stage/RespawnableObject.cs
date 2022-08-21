using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Parent class of all respawnable objects
	/// </summary>
	public abstract class RespawnableObject : Spatial
	{
		private struct SpawnData
		{
			public Node parentNode;
			public Transform spawnTransform;
			public void UpdateSpawnData(Spatial s)
			{
				if (!s.IsInsideTree()) return;

				parentNode = s.GetParent();
				spawnTransform = s.Transform;
			}
		}
		private SpawnData spawnData;
		protected virtual bool IsRespawnable() => false; //Set to True for enemies

		[Signal]
		public delegate void Respawned();
		[Signal]
		public delegate void Despawned();
		protected CharacterController Character => CharacterController.instance;

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			StageSettings.instance.Connect(nameof(StageSettings.StageUnload), this, nameof(Unload));

			if (!IsRespawnable()) return;

			spawnData.UpdateSpawnData(this);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public virtual void Unload() { }
		public virtual void Respawn()
		{
			if (!IsInsideTree() && GetParent() != spawnData.parentNode)
				spawnData.parentNode.AddChild(this);

			Transform = spawnData.spawnTransform;
			EmitSignal(nameof(Respawned));
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred("remove_child", this);
			EmitSignal(nameof(Despawned));
		}
	}
}
