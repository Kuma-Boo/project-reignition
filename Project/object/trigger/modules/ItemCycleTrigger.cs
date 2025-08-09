using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers;

public partial class ItemCycleTrigger : StageTriggerModule
{
	private bool ignoreCheckpoint; // Is the player moving backward through the level (ignore checkpoints)?
	private bool isCycleFlagSet; // Should we trigger an item switch?
	private int itemCycleIndex; // Active item set

	// Make sure itemCycle triggers are monitoring and collide with the player
	[Export]
	private Array<NodePath> itemCycles = [];
	[Export]
	private Array<NodePath> transitionNodes = [];
	private readonly Array<CullingTrigger> ItemCycles = [];
	private readonly Array<CullingTrigger> Transitions = [];
	public override void _Ready()
	{
		for (int i = 0; i < itemCycles.Count; i++)
		{
			CullingTrigger trigger = GetNodeOrNull<CullingTrigger>(itemCycles[i]); // Empty item cycle
			if (trigger == null)
			{
				ItemCycles.Add(null);
				continue;
			}

			ItemCycles.Add(trigger);
		}

		for (int i = 0; i < transitionNodes.Count; i++)
		{
			CullingTrigger trigger = GetNodeOrNull<CullingTrigger>(transitionNodes[i]); // Empty item cycle
			if (trigger == null)
			{
				Transitions.Add(null);
				continue;
			}

			Transitions.Add(trigger);
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
		ignoreCheckpoint = false;

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

	public override void Deactivate() => ignoreCheckpoint = true;

	private void DespawnItemCycle()
	{
		if (ItemCycles[itemCycleIndex] == null)
			return;

		ItemCycles[itemCycleIndex].Deactivate(); // Despawn current item cycle
	}

	private void SpawnItemCycle()
	{
		if (ItemCycles[itemCycleIndex] == null)
			return;

		ItemCycles[itemCycleIndex].Activate(); // Spawn current item cycle
	}

	private void OnCheckpointActivated()
	{
		if (ignoreCheckpoint) return;

		// Change the transition if needed
		if (Transitions.Count > itemCycleIndex)
			Transitions[itemCycleIndex]?.Deactivate();
		if (Transitions.Count > itemCycleIndex + 1)
			Transitions[itemCycleIndex + 1]?.Activate();

		isCycleFlagSet = true;
	}

	private void OnCheckpointDeactivated()
	{
		// Revert to the current lap's transition if needed
		if (Transitions.Count > itemCycleIndex)
			Transitions[itemCycleIndex]?.Activate();
		if (Transitions.Count > itemCycleIndex + 1)
			Transitions[itemCycleIndex + 1]?.Deactivate();

		if (isCycleFlagSet)
			isCycleFlagSet = false;
		else
			ignoreCheckpoint = true; // Player must be moving backwards
	}
}