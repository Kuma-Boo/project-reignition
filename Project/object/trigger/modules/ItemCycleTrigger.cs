using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
	public partial class ItemCycleTrigger : StageTriggerModule
	{
		[Export]
		private Area3D checkpointTrigger;

		private bool itemCycleRespawnEnabled = true; // Respawn items when cycling?
		private bool isCycleFlagSet; // Should we trigger an item switch?
		private int itemCycleIndex; // Active item set

		// Make sure itemCycle triggers are monitoring and collide with the player
		[Export]
		private Array<NodePath> itemCycles = new();
		private readonly Array<CullingTrigger> _itemCycles = new();
		public override void _Ready()
		{
			checkpointTrigger.Connect(Area3D.SignalName.AreaEntered, new Callable(this, MethodName.OnCheckpointEntered));

			for (int i = 0; i < itemCycles.Count; i++)
			{
				CullingTrigger trigger = GetNodeOrNull<CullingTrigger>(itemCycles[i]); // Empty item cycle
				if (trigger == null)
				{
					_itemCycles.Add(null);
					continue;
				}

				_itemCycles.Add(trigger);
			}
		}

		public override void Respawn()
		{
			// Revert to the first item cycle
			DespawnItemCycle();
			itemCycleIndex = 0;
			SpawnItemCycle();
			isCycleFlagSet = false;
		}


		public override void Activate()
		{
			if (itemCycles.Count == 0 || !isCycleFlagSet) return;

			// Cycle items
			DespawnItemCycle();

			// Increment counter
			itemCycleIndex++;
			if (itemCycleIndex > itemCycles.Count - 1)
				itemCycleIndex = 0;

			SpawnItemCycle();
			isCycleFlagSet = false;
		}

		private void DespawnItemCycle()
		{
			if (_itemCycles[itemCycleIndex] == null)
				return;

			_itemCycles[itemCycleIndex].Deactivate(); // Despawn current item cycle
		}

		private void SpawnItemCycle()
		{
			if (_itemCycles[itemCycleIndex] == null)
				return;

			_itemCycles[itemCycleIndex].Activate(); // Spawn current item cycle
		}


		private void OnCheckpointEntered(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			isCycleFlagSet = true;
		}
	}
}
