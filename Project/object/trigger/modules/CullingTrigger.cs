using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Hides/Shows nodes.
	/// Use <see cref="modifyTree"/> to enable/disable objects completely. (i.e. one way collisions)
	/// </summary>
	public partial class CullingTrigger : StageTriggerModule
	{
		[Export]
		private Node3D targetNode;
		[Export]
		private bool modifyTree; //Can cause stuttering when used on denser objects
		private SpawnData spawnData; //Data for tree modification
		[Export]
		private bool startEnabled; //Generally things should start culled

		public override void _Ready()
		{
			if (modifyTree)
				spawnData = new SpawnData(targetNode.GetParent(), targetNode.Transform);

			Respawn();
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public override void _ExitTree()
		{
			if (modifyTree && !targetNode.IsQueuedForDeletion())
				targetNode.QueueFree();
		}

		private void Respawn()
		{
			//Disable the node on startup?
			if (!startEnabled)
				CallDeferred(nameof(DeactivateNode));
		}

		public void ActivateNode()
		{
			if (modifyTree)
			{
				if (targetNode.IsInsideTree()) return;

				spawnData.Respawn(targetNode);
				return;
			}

			targetNode.Visible = true;
			targetNode.SetProcess(true);
			targetNode.SetPhysicsProcess(true);
		}

		public void DeactivateNode()
		{
			if (modifyTree)
			{
				if (!targetNode.IsInsideTree()) return;

				spawnData.parentNode.CallDeferred("remove_child", targetNode);
				return;
			}

			targetNode.Visible = false;
			targetNode.SetProcess(false);
			targetNode.SetPhysicsProcess(false);
		}
	}
}
