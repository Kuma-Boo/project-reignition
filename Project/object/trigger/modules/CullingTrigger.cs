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
		private bool modifyTree; //Doesn't work with isStageVisuals, and can cause stuttering when used on denser objects
		private SpawnData spawnData; //Data for tree modification
		[Export]
		private bool startEnabled; //Generally things should start culled
		[Export]
		private bool isStageVisuals;
		private Callable ProcessCheckpointCallable => new Callable(this, MethodName.ProcessCheckpoint);
		private StageSettings Stage => StageSettings.instance;

		public override void _Ready()
		{
			if (isStageVisuals)
			{
				if (CheatManager.DisableStageCulling) //Culling disabled
					return;

				if (!Stage.IsConnected(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable))
					Stage.Connect(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable);
			}
			else if (modifyTree)
				spawnData = new SpawnData(targetNode.GetParent(), targetNode.Transform);

			Respawn();
			Stage.RegisterRespawnableObject(this);
		}

		public override void _ExitTree()
		{
			if (Stage.IsConnected(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable))
				Stage.Disconnect(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable);

			if (modifyTree && !targetNode.IsQueuedForDeletion())
				targetNode.QueueFree();
		}

		private bool useCheckpointVisibility;
		private bool visibleOnCheckpoint; //Saves the current visiblity when player passes a checkpoint
		private void ProcessCheckpoint() => visibleOnCheckpoint = targetNode.Visible;

		private void Respawn()
		{
			if (isStageVisuals)
			{
				if (useCheckpointVisibility)
				{
					if (visibleOnCheckpoint)
						Activate();
					else
						Deactivate();

					return;
				}

				useCheckpointVisibility = true;
			}

			//Disable the node on startup?
			if (!startEnabled)
				Deactivate();
		}

		public override void Activate()
		{
			if (isStageVisuals && CheatManager.DisableStageCulling) return;

			if (!isStageVisuals && modifyTree)
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

			if (!isStageVisuals && modifyTree)
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
