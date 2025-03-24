using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers;

public partial class CullingTrigger : StageTriggerModule
{
	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler();

	[Export]
	private bool startEnabled; // Generally things should start culled
	[Export]
	private bool saveVisibilityOnCheckpoint;
	[Export]
	private bool isStageVisuals;
	private bool isActive;
	private StageSettings Stage => StageSettings.Instance;
	[Export]
	private bool respawnOnActivation;
	private Array<Node> respawnableNodes = [];

	public override void _EnterTree()
	{
		Visible = true;
		if (isStageVisuals)
			DebugManager.Instance.StageCullingToggled += UpdateCullingState;

		if (isStageVisuals && !TransitionManager.instance.IsReloadingScene)
			ShaderManager.Instance.RegisterCullingTrigger(this);
	}

	public override void _ExitTree()
	{
		if (isStageVisuals)
			DebugManager.Instance.StageCullingToggled -= UpdateCullingState;
	}

	public override void _Ready()
	{
		// Show everything for shader compilation
		Visible = true;

		// Cache all children with a respawn method
		if (respawnOnActivation)
		{
			foreach (Node child in GetChildren(true))
			{
				if (child.HasMethod(MethodName.Respawn))
					respawnableNodes.Add(child);
			}
		}

		if (saveVisibilityOnCheckpoint)
		{
			// Cache starting checkpoint state
			visibleOnCheckpoint = visibleOnDebugCheckpoint = startEnabled;

			// Listen for checkpoint signals
			DebugManager.Instance.TriggeredDebugCheckpoint += ProcessDebugCheckpoint;
			Stage.TriggeredCheckpoint += ProcessCheckpoint;
			Stage.Respawned += Respawn;
		}

		if (isStageVisuals)
			Stage.Connect(StageSettings.SignalName.LevelStarted, new Callable(this, MethodName.Respawn), (uint)ConnectFlags.Deferred);
		else
			CallDeferred(MethodName.Respawn);
	}

	private bool visibleOnCheckpoint;
	private bool visibleOnDebugCheckpoint;
	/// <summary> Saves the current visiblity. Called when the player passes a checkpoint. </summary>
	private void ProcessCheckpoint()
	{
		if (StageSettings.Instance.LevelState == StageSettings.LevelStateEnum.Probes ||
			StageSettings.Instance.LevelState == StageSettings.LevelStateEnum.Shaders)
		{
			visibleOnCheckpoint = startEnabled;
		}
		else
		{
			visibleOnCheckpoint = Visible;
		}
	}

	private void ProcessDebugCheckpoint() => visibleOnDebugCheckpoint = Visible;

	public override void Respawn()
	{
		if (saveVisibilityOnCheckpoint)
		{
			if (Player.IsDebugRespawn && visibleOnDebugCheckpoint)
				Activate();
			else if (!Player.IsDebugRespawn && visibleOnCheckpoint)
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
				node.Call(MethodName.Respawn);
		}

		EmitSignal(SignalName.Activated);
	}

	public override void Deactivate()
	{
		isActive = false;
		UpdateCullingState();
		EmitSignal(SignalName.Deactivated);
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