using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Pickup : Area3D
	{
		[Signal]
		public delegate void CollectedEventHandler();
		[Signal]
		public delegate void RespawnedEventHandler();
		[Signal]
		public delegate void DespawnedEventHandler();

		private StageSettings.SpawnData spawnData;
		protected CharacterController Character => CharacterController.instance;

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			spawnData = new StageSettings.SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public void OnEnter() => CallDeferred(nameof(Collect));

		public virtual void Respawn()
		{
			if (!IsInsideTree() && GetParent() != spawnData.parentNode)
				spawnData.parentNode.AddChild(this);

			Transform = spawnData.spawnTransform;
			EmitSignal(SignalName.Respawned);
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred("remove_child", this);
			EmitSignal(SignalName.Despawned);
		}

		protected virtual void Collect()
		{
			EmitSignal(SignalName.Collected);
		}
	}
}
