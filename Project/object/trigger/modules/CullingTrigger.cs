using Godot;
using Godot.Collections;
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
		[Export]
		private bool respawnOnActivation;
		private Array<Node> respawnableNodes = new();

		public override void _EnterTree()
		{
			Visible = true;
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
			// Cache all children with a respawn method
			if (respawnOnActivation)
			{
				Array<Node> children = GetChildren(true);
				foreach (Node child in children)
				{
					if (child.HasMethod(StageSettings.RESPAWN_FUNCTION))
						respawnableNodes.Add(child);
				}
			}

			if (saveVisibilityOnCheckpoint)
			{
				//Cache starting checkpoint state
				visibleOnCheckpoint = startEnabled;

				//Listen for checkpoint signals
				Level.Connect(StageSettings.SignalName.TriggeredCheckpoint, new Callable(this, MethodName.ProcessCheckpoint));
				Level.ConnectRespawnSignal(this);
			}

			CallDeferred(MethodName.Respawn);
		}

		private bool visibleOnCheckpoint;
		/// <summary> Saves the current visiblity. Called when the player passes a checkpoint. </summary>
		private void ProcessCheckpoint()
		{
			if (StageSettings.instance.LevelState == StageSettings.LevelStateEnum.Loading)
				visibleOnCheckpoint = startEnabled;
			else
				visibleOnCheckpoint = Visible;
		}

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

			// Respawn everything
			if (respawnOnActivation)
			{
				foreach (Node node in respawnableNodes)
					node.Call(StageSettings.RESPAWN_FUNCTION);
			}
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
				SetDeferred("visible", true);
				SetDeferred("process_mode", (long)ProcessModeEnum.Inherit);
				return;
			}

			SetDeferred("visible", isActive);
			SetDeferred("process_mode", (long)(isActive ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled));
		}
	}
}
