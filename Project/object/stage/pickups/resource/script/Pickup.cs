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

		public bool DisableAutoRespawning { get; set; } //Used for ItemBoxes to allow manual respawning
		private SpawnData spawnData;
		protected CharacterController Character => CharacterController.instance;

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			if (DisableAutoRespawning) return; //Don't respawn automatically

			spawnData = new SpawnData(GetParent(), Transform);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			CallDeferred(MethodName.Collect);
		}

		public virtual void Respawn()
		{
			if (DisableAutoRespawning) return;

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
