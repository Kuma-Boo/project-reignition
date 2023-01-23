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
		private bool isStageVisuals; //Take CheatManager.DisableStageCulling into account?
		private bool DebugDisableCulling => isStageVisuals && CheatManager.DisableStageCulling;
		private LevelSettings Level => LevelSettings.instance;

		public override void _Ready()
		{
			if (DebugDisableCulling) return;

			if (saveVisibilityOnCheckpoint)
			{
				//Cache starting checkpoint state
				visibleOnCheckpoint = startEnabled;

				//Listen for checkpoint signals
				Level.Connect(LevelSettings.SignalName.OnTriggeredCheckpoint, new Callable(this, MethodName.ProcessCheckpoint));
				Level.ConnectRespawnSignal(this);
			}

			Respawn();
		}

		private bool visibleOnCheckpoint;
		/// <summary> Saves the current visiblity. Called when the player passes a checkpoint. </summary>
		private void ProcessCheckpoint() => visibleOnCheckpoint = Visible;

		public override void Respawn()
		{
			if (saveVisibilityOnCheckpoint)
			{
				if (visibleOnCheckpoint)
					Activate();
				else
					Deactivate();

				return;
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
