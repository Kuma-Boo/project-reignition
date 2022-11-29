using Godot;
using Project.Core;

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
		[Export]
		private bool isStageVisuals;

		public override void _Ready()
		{
			if (isStageVisuals && CheatManager.DisableStageCulling)
			{
				Visible = true;
				return;
			}

			if (modifyTree)
				spawnData = new SpawnData(targetNode.GetParent(), targetNode.Transform);

			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
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
				Deactivate();
		}

		public override void Activate()
		{
			if (isStageVisuals && CheatManager.DisableStageCulling) return;

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

		public override void Deactivate()
		{
			if (isStageVisuals && CheatManager.DisableStageCulling) return;

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
