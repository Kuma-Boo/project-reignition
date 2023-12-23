using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	public partial class CullingTrigger : StageTriggerModule
	{
		[Export]
		private bool startEnabled; // Generally things should start culled
		[Export]
		private bool saveVisibilityOnCheckpoint;
		[Export]
		private bool isStageVisuals;
		private bool isActive;
		private StageSettings Level => StageSettings.instance;

		public override void _EnterTree()
		{
			if (isStageVisuals)
				DebugManager.Instance.Connect(DebugManager.SignalName.StageCullingToggled, new Callable(this, MethodName.UpdateCullingState));
		}

		public override void _ExitTree()
		{
			if (isStageVisuals)
				DebugManager.Instance.Disconnect(DebugManager.SignalName.StageCullingToggled, new Callable(this, MethodName.UpdateCullingState));
		}

		public override void _Ready()
		{
			if (saveVisibilityOnCheckpoint)
			{
				//Cache starting checkpoint state
				visibleOnCheckpoint = startEnabled;

				//Listen for checkpoint signals
				Level.Connect(StageSettings.SignalName.OnTriggeredCheckpoint, new Callable(this, MethodName.ProcessCheckpoint));
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

			// Disable the node on startup?
			if (startEnabled)
				Activate();
			else
				Deactivate();
		}

		public override void Activate()
		{
			isActive = true;
			UpdateCullingState();
		}

		public override void Deactivate()
		{
			isActive = false;
			UpdateCullingState();
		}

		private void UpdateCullingState()
		{
			if (isStageVisuals && !DebugManager.IsStageCullingEnabled) // Treat as active
			{
				Visible = true;
				ProcessMode = ProcessModeEnum.Inherit;
				return;
			}

			Visible = isActive;
			ProcessMode = isActive ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		}
	}
}
