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

		/// <summary> Used for runtime items (Enemy Pearls, Item Box Contents, etc) to allow manual respawning. </summary>
		public bool DisableAutoRespawning { get; set; }
		public SpawnData SpawnData { get; set; }

		protected StageSettings Stage => StageSettings.Instance;
		protected PlayerController Player => StageSettings.Player;

		public override void _Ready() => SetUp();

		protected virtual void SetUp()
		{
			SpawnData = new(GetParent(), Transform);

			if (!DisableAutoRespawning) // Connect respawn triggers
			{
				Stage.ConnectRespawnSignal(this);
				Stage.ConnectUnloadSignal(this);
			}
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			if (Player.IsTeleporting) return; // Don't allow collections during teleport/respawn

			CallDeferred(MethodName.Collect);
		}

		public virtual void Unload() => QueueFree();
		public virtual void Respawn()
		{
			SpawnData.Respawn(this);
			EmitSignal(SignalName.Respawned);
		}

		public virtual void Despawn()
		{
			if (!IsInsideTree()) return;

			GetParent().CallDeferred(MethodName.RemoveChild, this);
			EmitSignal(SignalName.Despawned);
		}

		protected virtual void Collect() => EmitSignal(SignalName.Collected);
	}
}
