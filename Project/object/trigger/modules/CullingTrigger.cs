using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public partial class CullingTrigger : StageTriggerModule
	{
		[Export]
		private bool startEnabled; //Generally things should start culled
		[Export]
		private bool saveVisibilityOnCheckpoint;
		[Export]
		private bool isStageVisuals; //Take cheat into account?
		private Callable ProcessCheckpointCallable => new Callable(this, MethodName.ProcessCheckpoint);
		private LevelSettings Level => LevelSettings.instance;

		private bool DebugDisableCulling => isStageVisuals && CheatManager.DisableStageCulling;

		public override void _Ready()
		{
			if (DebugDisableCulling) return;

			Respawn();
			Level.ConnectRespawnSignal(this);
			Level.ConnectUnloadSignal(this);

			if (saveVisibilityOnCheckpoint)
			{
				if (!Level.IsConnected(LevelSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable))
					Level.Connect(LevelSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable);

				//Cache starting checkpoint state
				ProcessCheckpoint();
			}
		}

		public void Unload()
		{
			if (Level.IsConnected(LevelSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable))
				Level.Disconnect(LevelSettings.SignalName.OnTriggeredCheckpoint, ProcessCheckpointCallable);
		}

		private bool useCheckpointVisibility;
		private bool visibleOnCheckpoint; //Saves the current visiblity when player passes a checkpoint
		private void ProcessCheckpoint() => visibleOnCheckpoint = Visible;

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

			Visible = true;
			ProcessMode = ProcessModeEnum.Inherit;
		}

		public override void Deactivate()
		{
			if (DebugDisableCulling) return;

			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
