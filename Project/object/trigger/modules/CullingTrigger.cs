using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers;

public partial class CullingTrigger : StageTriggerModule
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void DeactivatedEventHandler();

	[Export] private bool startEnabled; // Generally things should start culled
	[Export] private bool saveVisibilityOnCheckpoint;
	[Export] private bool isStageVisuals;
	private bool isActive;
	private StageSettings Stage => StageSettings.Instance;
	/// <summary> Determines whether children respawn methods should be called when activating culling trigger. </summary>
	[Export] private bool respawnOnActivation;
	/// <summary> Determines whether children respawn methods should be cached on startup. </summary>
	[Export] private bool cacheRespawnMethods;
	private readonly Array<Node> respawnableNodes = [];

	public override void _EnterTree()
	{
		Visible = true;
		if (isStageVisuals)
			DebugManager.Instance.StageCullingToggled += UpdateCullingState;
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
		if (respawnOnActivation || cacheRespawnMethods)
			SetUpRespawning(this);

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

	/// <summary> An expensive operation that automatically connects respawning methods for child nodes. </summary>
	private void SetUpRespawning(Node parentNode)
	{
		foreach (Node child in parentNode.GetChildren())
		{
			if (child.Name.ToString().Contains("Group")) // Cache recursively if node name contains the word "Group"
			{
				SetUpRespawning(child);
				continue;
			}

			if (child.HasMethod(MethodName.SetUpRespawning))
				child.Call(MethodName.SetUpRespawning);

			if (child.HasMethod(MethodName.Respawn))
				respawnableNodes.Add(child);
		}
	}

	private bool visibleOnCheckpoint;
	private bool visibleOnDebugCheckpoint;
	/// <summary> Saves the current visiblity. Called when the player passes a checkpoint. </summary>
	private void ProcessCheckpoint()
	{
		if (StageSettings.Instance.IsLevelLoading)
			visibleOnCheckpoint = startEnabled;
		else
			visibleOnCheckpoint = Visible;
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

	/// <summary>
	/// Respawns all child nodes (non-recursive). Call this from a signal to force respawn objects in a looping stage.
	/// </summary>
	private void RespawnChildren()
	{
		foreach (Node node in respawnableNodes)
			node.CallDeferred(MethodName.Respawn);
	}

	public override void Activate()
	{
		isActive = true;
		UpdateCullingState();

		// Respawn everything
		if (respawnOnActivation)
			RespawnChildren();

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
