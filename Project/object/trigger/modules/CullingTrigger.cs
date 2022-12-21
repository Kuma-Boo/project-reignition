using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public partial class CullingTrigger : StageTriggerModule
	{
		/// <summary>
		/// Target node to affect. Can't be the CullingTrigger object when set to CullMode.ModifyTree.
		/// </summary>
		[Export]
		private Node3D targetNode;
		[Export]
		private bool startEnabled; //Generally things should start culled
		[Export]
		private bool modifyTree; //Should the SceneTree be modified when target object is culled?
		[Export]
		private bool saveVisibilityOnCheckpoint;
		[Export]
		private bool isStageVisuals; //Take cheat into account?
		private SpawnData spawnData; //Data for tree modification
		private Callable ProcessCheckpointCallable => new Callable(this, MethodName.ProcessCheckpoint);
		private StageSettings Stage => StageSettings.instance;

		private bool DebugDisableCulling => isStageVisuals && CheatManager.DisableStageCulling;

		public override void _Ready()
		{
			if (DebugDisableCulling) return;

			if (saveVisibilityOnCheckpoint)
			{
				if (!Stage.IsConnected(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable))
					Stage.Connect(StageSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable);
			}

			if (modifyTree) //Save spawn data
			{
				if (targetNode == this) //The results of this configuration is that once the culling trigger gets despawned, it'll never be respawned again
					GD.PrintErr($"{Name}: Target Node cannot be the same object as CullingTrigger.cs when using CullMode.ModifyTree!");

				spawnData = new SpawnData(targetNode.GetParent(), targetNode.Transform);
			}

			Respawn();
			Stage.ConnectRespawnSignal(this);
			Stage.ConnectUnloadSignal(this);
		}

		public void Unload()
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
			if (saveVisibilityOnCheckpoint)
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
			if (startEnabled)
				Activate();
			else
				Deactivate();
		}

		public override void Activate()
		{
			if (DebugDisableCulling) return;

			if (modifyTree)
			{
				GD.Print($"Respawned " + Name);
				spawnData.Respawn(targetNode);
			}

			targetNode.Visible = true;
			targetNode.SetProcess(true);
			targetNode.SetPhysicsProcess(true);
		}

		public override void Deactivate()
		{
			if (DebugDisableCulling) return;

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
