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

		private SpawnData spawnData;
		protected CharacterController Character => CharacterController.instance;

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			spawnData = new SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public void OnEnter(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			CallDeferred(nameof(Collect));
		}

		public virtual void Respawn()
		{
			spawnData.Respawn(this);
			EmitSignal(SignalName.Respawned);
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred("remove_child", this);
			EmitSignal(SignalName.Despawned);
		}

		protected virtual void Collect() => EmitSignal(SignalName.Collected);
	}
}
