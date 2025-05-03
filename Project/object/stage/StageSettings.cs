using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Stage settings.
/// Must be the first thing loaded in a level.
/// </summary>
public partial class StageSettings : Node3D
{
	public static StageSettings Instance;
	public static PlayerController Player { get; private set; }
	public static void RegisterPlayer(PlayerController player) => Player = player;

	public bool IsControlTest => Data.LevelID == OptionsLevelId;
	private readonly StringName OptionsLevelId = "options";

	public override void _EnterTree()
	{
		Instance = this; // Always override previous instance

		for (int i = 0; i < pathParent.GetChildCount(); i++)
		{
			Path3D path = pathParent.GetChildOrNull<Path3D>(i);
			if (path != null)
				pathList.Add(path);
		}

		CalculateTechnicalBonus();
		UpdateScore(0, MathModeEnum.Replace);
		UpdatePostProcessingStatus();

		// Update gameplay sfx audio channel
		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, IsControlTest ? 100 : 0);

		if (IsControlTest)
		{
			LevelState = LevelStateEnum.Ingame;
		}
		else
		{
			LevelState = LevelStateEnum.Probes;
			if (!TransitionManager.instance.IsReloadingScene)
				TransitionManager.instance.UpdateLoadingText("load_probes");
		}
	}

	public override void _Ready()
	{
		if (IsControlTest)
			return;

		// Fixes obnoxious flickering when testing from the editor
		if (OS.IsDebugBuild() && !TransitionManager.IsTransitionActive)
		{
			TransitionManager.StartTransition(new()
			{
				outSpeed = .5f,
				color = Colors.Black,
				disableAutoTransition = true,
			});
		}

		SetEnvironmentFxFactor(environmentFxFactor, 0);
	}

	public override void _ExitTree()
	{
		EmitSignal(SignalName.Unloaded);
	}

	public void UpdatePostProcessingStatus()
	{
		bool postProcessingEnabled = SaveManager.Config.postProcessingQuality != SaveManager.QualitySetting.Disabled;
		Environment.Environment.SsaoEnabled = postProcessingEnabled;
		Environment.Environment.SsilEnabled = postProcessingEnabled;
		Environment.Environment.GlowEnabled = SaveManager.Config.bloomMode != SaveManager.QualitySetting.Disabled;
	}

	#region Shader Compilation
	[Signal]
	public delegate void LevelStartedEventHandler();
	private float probeTimer;
	private const float ProbeWaitLength = 2f;
	public override void _Process(double _)
	{
		/*
		TODO 
		Temporary workaround because reflection probes are slow.
		Reduce frame count when Godot adds a way to do quick "UPDATE_ONCE" reflection probes
		*/

		if (LevelState == LevelStateEnum.Probes)
		{
			probeTimer += PhysicsManager.normalDelta;
			if (probeTimer >= ProbeWaitLength)
				StartLevel();

			return;
		}

		UpdateTime();
		UpdateEnvironmentFXFactor();
	}

	private void StartLevel()
	{
		LevelState = LevelStateEnum.Ingame;
		SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, 100); // Unmute gameplay sound effects
		TransitionManager.FinishTransition();
		EmitSignal(SignalName.LevelStarted);
	}
	#endregion

	#region Level Settings
	/// <summary> Reference to the level's data. </summary>
	[Export]
	public LevelDataResource Data { get; private set; }
	[Export]
	public CameraSettingsResource InitialCameraSettings { get; private set; }
	[Export]
	public SFXLibraryResource dialogLibrary;

	/// <summary>
	/// Calculates the rank [Fail = -1, None = 0, Bronze = 1, Silver = 2, Gold = 3]
	/// </summary>
	public int CalculateRank(bool preCountBonuses = false)
	{
		if (LevelState == LevelStateEnum.Failed)
			return -1;

		int rank = 0; // DEFAULT - No rank
		float completionTime = Mathf.RoundToInt(CurrentTime * 100f) * 0.01f; // Round to nearest millisecond

		if (Data.SkipScore)
		{
			if (completionTime <= Data.GoldTime)
				rank = 3;
			else if (completionTime <= Data.SilverTime)
				rank = 2;
			else if (completionTime <= Data.BronzeTime)
				rank = 1;
		}
		else
		{
			int score = TotalScore;
			if (preCountBonuses)
				score += BonusManager.instance.QueuedScore;

			if (completionTime <= Data.GoldTime && score >= Data.Score) // Perfect run
				rank = 3;
			else if (completionTime <= Data.SilverTime && score >= 3 * (Data.Score / 4)) // Silver score reqs are always 3/4 of gold
				rank = 2;
			else if (completionTime <= Data.BronzeTime) // Bronze is easy to get
				rank = 1;
		}

		if (rank >= 3 && RespawnCount != 0) // Limit to silver if a respawn occured
			rank = 2;

		return rank;
	}
	#endregion

	public string GetRequiredTime(int rank)
	{
		switch (rank)
		{
			case 0:
				return ExtensionMethods.FormatTime(Data.BronzeTime);
			case 1:
				return ExtensionMethods.FormatTime(Data.SilverTime);
			case 2:
				return ExtensionMethods.FormatTime(Data.GoldTime);
			default:
				return "00:00.00";
		}
	}
	public int GetRequiredScore() => Data.Score;

	#region Level Data
	public enum MathModeEnum // List of ways the score can be modified
	{
		Add,
		Subtract,
		Multiply,
		Replace
	}
	/// <summary> Calculates value based on provided MathMode. </summary>
	private static int CalculateMath(int value, int amount, MathModeEnum mode)
	{
		switch (mode)
		{
			case MathModeEnum.Add:
				value += amount;
				break;
			case MathModeEnum.Subtract:
				value -= amount;
				if (value < 0) // Clamp to zero
					value = 0;
				break;
			case MathModeEnum.Multiply:
				value *= amount;
				break;
			case MathModeEnum.Replace:
				value = amount;
				break;
		}
		return value;
	}

	[Signal]
	public delegate void ScoreChangedEventHandler(); // Score has changed, normally occours from a bonus
	public int CurrentScore { get; private set; } // How high is the current score?
	public string DisplayScore { get; private set; } // Current score formatted to eight zeros
	/// <summary> Total score, including ring and technical bonus. </summary>
	public int TotalScore => CurrentScore + Mathf.CeilToInt(RingBonus * TechnicalBonus);
	public void UpdateScore(int amount, MathModeEnum mode)
	{
		CurrentScore = CalculateMath(CurrentScore, amount, mode);
		DisplayScore = ExtensionMethods.FormatMenuNumber(CurrentScore);
		EmitSignal(SignalName.ScoreChanged);
	}

	/// <summary> How many times has the player taken damage? </summary>
	public int DamageCount { get; private set; }
	/// <summary> The number of times the player has respawned. </summary>
	public int RespawnCount { get; private set; } // How high many times did the player have to respawn?
	public void IncrementDamageCount()
	{
		DamageCount++;
		CalculateTechnicalBonus();
	}
	public void IncrementRespawnCount()
	{
		RespawnCount++;
		CalculateTechnicalBonus();
	}
	public float TechnicalBonus { get; private set; }

	private void CalculateTechnicalBonus()
	{
		if (LevelState == LevelStateEnum.Failed)
		{
			// Failing the level gives a technical bonus of .5
			TechnicalBonus = .5f;
			return;
		}

		if (RespawnCount != 0 || DamageCount >= 6)
		{
			// Respawning automatically means 1.0
			TechnicalBonus = 1.0f;
			return;
		}

		// Damage values
		if (DamageCount >= 4)
		{
			// 4-5
			TechnicalBonus = 1.1f;
			return;
		}

		if (DamageCount >= 2)
		{
			// 2-3
			TechnicalBonus = 1.2f;
			return;
		}

		if (DamageCount == 1)
		{
			// 1
			TechnicalBonus = 1.5f;
			return;
		}

		TechnicalBonus = 2.0f; // Perfect run
	}

	//Objectives
	public int CurrentObjectiveCount { get; private set; } // How much has the player currently completed?
	[Signal]
	public delegate void ObjectiveChangedEventHandler(); // Progress towards the objective has changed
	[Signal]
	public delegate void ObjectiveResetEventHandler(); // Progress towards the objective has changed
	public void IncrementObjective()
	{
		CurrentObjectiveCount++;
		CurrentObjectiveCount = Mathf.Clamp(CurrentObjectiveCount, 0, Data.MissionObjectiveCount);
		EmitSignal(SignalName.ObjectiveChanged);

		if (Data.MissionObjectiveCount == 0) // i.e. Sand Oasis's "Don't break the jars!" mission.
		{
			FinishLevel(false);
		}
		else if (CurrentObjectiveCount >= Data.MissionObjectiveCount &&
				Data.MissionType != LevelDataResource.MissionTypes.Chain)
		{
			FinishLevel(true);
		}
	}

	public void ResetObjective(int progress = 0)
	{
		CurrentObjectiveCount = progress;
		EmitSignal(SignalName.ObjectiveReset);
	}

	// Rings
	public int CurrentRingCount { get; private set; } // How many rings is the player currently holding?
	public int RingBonus { get; private set; }
	[Signal]
	public delegate void RingChangedEventHandler(int change); // Ring count has changed
	public void UpdateRingCount(int amount, MathModeEnum mode, bool disableAnimations = false)
	{
		int previousAmount = CurrentRingCount;
		CurrentRingCount = CalculateMath(CurrentRingCount, amount, mode);
		RingBonus = CurrentRingCount * 10;
		if (Data.MissionType == LevelDataResource.MissionTypes.Ring &&
			CurrentRingCount >= Data.MissionObjectiveCount &&
			Data.MissionObjectiveCount != 0) // For ring based missions
		{
			CurrentRingCount = Data.MissionObjectiveCount; // Clamp
			FinishLevel(true);
		}

		// Soul barrier
		if (Player == null)
		{
			GD.PushError("PlayerController is missing!");
			if (mode == MathModeEnum.Subtract && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RingLossConvert))
				Player.Skills.ModifySoulGauge((previousAmount - CurrentRingCount) * 2);
		}

		if (DebugManager.Instance.InfiniteRings) // Infinite ring cheat
			CurrentRingCount = 999;

		EmitSignal(SignalName.RingChanged, CurrentRingCount - previousAmount, disableAnimations);
	}

	public int CurrentEXP { get; set; } // How much exp is the player earning from this stage?

	// Time
	[Signal]
	public delegate void TimeChangedEventHandler(); // Time has changed.

	public float CurrentTime { get; private set; } // How long has the player been on this level? (In Seconds)
	public string DisplayTime { get; private set; } // Current time formatted in mm:ss.ff
	private void UpdateTime(bool skipPhysicsTick = false)
	{
		if (!IsLevelIngame || !Interface.PauseMenu.AllowPausing) return;

		if (!skipPhysicsTick)
			CurrentTime += PhysicsManager.normalDelta; // Add current time
		DisplayTime = ExtensionMethods.FormatTime(CurrentTime);
		if (Data.MissionTimeLimit != 0 && CurrentTime >= Data.MissionTimeLimit) // Time's up!
			FinishLevel(false);

		EmitSignal(SignalName.TimeChanged);
	}

	/// <summary> Artifically add time. Used when skipping cutscenes. </summary>
	public void AddTime(float amount)
	{
		CurrentTime += amount;
		UpdateTime(true);
	}

	public bool[] fireSoulCheckpoints = new bool[3];
	public bool IsFireSoulCheckpointFlagSet(int index) => fireSoulCheckpoints[index];
	public bool SetFireSoulCheckpointFlag(int index, bool value) => fireSoulCheckpoints[index] = value;
	#endregion

	#region Path Settings
	[Export(PropertyHint.NodeType, "Node3D")]
	private Node3D pathParent;
	/// <summary> List of all level paths contained for this level. </summary>
	private readonly Array<Path3D> pathList = [];

	/// <summary>
	/// Returns the path the player is currently the closest to.
	/// Allows placing the player anywhere in the editor without needing to manually assign paths.
	/// </summary>
	public Path3D CalculateStartingPath(Vector3 globalPosition)
	{
		int closestPathIndex = -1;
		float closestDistanceSquared = Mathf.Inf;

		for (int i = 0; i < pathList.Count; i++)
		{
			Vector3 closestPoint = pathList[i].Curve.GetClosestPoint(globalPosition - pathList[i].GlobalPosition);
			closestPoint += pathList[i].GlobalPosition;
			float dstSquared = globalPosition.DistanceSquaredTo(closestPoint);

			if (dstSquared < closestDistanceSquared)
			{
				closestPathIndex = i;
				closestDistanceSquared = dstSquared;
			}
		}

		if (closestPathIndex == -1)
			return null;

		return pathList[closestPathIndex];
	}
	#endregion

	#region Object Spawning
	// Checkpoint data
	[Signal]
	public delegate void TriggeredCheckpointEventHandler();
	public Triggers.CheckpointTrigger CurrentCheckpoint { get; private set; }
	private int CheckpointScore { get; set; }
	private int CheckpointObjectiveCount { get; set; }
	private float CheckpointEnvironmentFxFactor { get; set; }
	public void SetCheckpoint(Triggers.CheckpointTrigger checkpoint)
	{
		if (checkpoint == CurrentCheckpoint) return; // Already at this checkpoint

		CurrentCheckpoint = checkpoint;
		CheckpointScore = CurrentScore;
		CheckpointObjectiveCount = CurrentObjectiveCount;
		CheckpointEnvironmentFxFactor = targetEnvironmentFxFactor;
		EmitSignal(SignalName.TriggeredCheckpoint);
	}

	public void RevertToCheckpointData()
	{
		ResetObjective(CheckpointObjectiveCount);
		UpdateScore(CheckpointScore, MathModeEnum.Replace);
		SetEnvironmentFxFactor(CheckpointEnvironmentFxFactor, 0);
	}

	[Signal]
	public delegate void RespawnedEventHandler();
	[Signal]
	public delegate void RespawnedEnemiesEventHandler();
	[Signal]
	public delegate void UnloadedEventHandler();

	public void StartRespawn()
	{
		SoundManager.instance.CancelDialog(); // Cancel any active dialog
		GetTree().CreateTimer(PhysicsManager.physicsDelta, false, true).Timeout += RespawnEnemies;
	}

	private void RespawnEnemies()
	{
		EmitSignal(SignalName.Respawned);
		EmitSignal(SignalName.RespawnedEnemies);
	}
	#endregion

	#region Level Completion
	[Signal]
	public delegate void LevelCompletedEventHandler(); // Called when the level is completed
	[Signal]
	public delegate void LevelFailedEventHandler(); // Called when the level is failed
	[Signal]
	public delegate void LevelSuccessEventHandler(); // Called when the level is successfully finished
	[Signal]
	public delegate void LevelDemoStartedEventHandler(); // Called when the level demo starts

	public enum LevelStateEnum
	{
		Probes,
		Ingame,
		Failed,
		Success,
	}
	public LevelStateEnum LevelState { get; private set; }
	public bool IsLevelLoading => LevelState == LevelStateEnum.Probes;
	public bool IsLevelIngame => LevelState == LevelStateEnum.Ingame;
	/// <summary> Flag for keeping track of Uhu's race status. </summary>
	public bool IsRaceActive { get; set; }
	private const float FAIL_COMPLETION_DELAY = 1.5f; // Mission fails always have a delay of 1.5 seconds
	public void FinishLevel(bool wasSuccessful)
	{
		if (!IsLevelIngame)
			return;

		// Attempt to start the completion demo
		GetTree().CreateTimer(wasSuccessful ? Data.CompletionDelay : FAIL_COMPLETION_DELAY).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.StartCompletionDemo));

		BGMPlayer.StageMusicPaused = true;
		SoundManager.instance.CancelDialog();
		Interface.PauseMenu.AllowPausing = false;
		LevelState = wasSuccessful ? LevelStateEnum.Success : LevelStateEnum.Failed;

		// Recalculate technical bonus
		CalculateTechnicalBonus();

		EmitSignal(SignalName.LevelCompleted);
		EmitSignal(wasSuccessful ? SignalName.LevelSuccess : SignalName.LevelFailed);
	}

	/// <summary> Camera demo that gets enabled after the level is cleared. </summary>
	[Export]
	private AnimationPlayer completionAnimator;
	private int completionAnimationIndex;
	private bool isCompletionDemoActive;
	public void StartCompletionDemo()
	{
		if (isCompletionDemoActive)
			return;

		isCompletionDemoActive = true;
		EmitSignal(SignalName.LevelDemoStarted);

		if (completionAnimator == null) return;
		OnCameraDemoAdvance();
	}

	/// <summary> Completion demo advanced, play a crossfade. </summary>
	public void OnCameraDemoAdvance()
	{
		completionAnimationIndex++;
		if (completionAnimationIndex > 3)
			completionAnimationIndex = 1;
		completionAnimator.Play($"demo{completionAnimationIndex}");
		Player.Camera.StartCrossfade();
	}

	#endregion

	/// <summary> Reference to active area's WorldEnvironment node. </summary>
	[Export] public WorldEnvironment Environment { get; private set; }
	[Export(PropertyHint.Range, "0,1,.1")] private float environmentFxFactor;
	private float targetEnvironmentFxFactor;
	private float environmentFxVelocity;
	private float environmentFxSmoothing;
	private readonly StringName ShaderEnvironmentFXParameter = "environment_fx_intensity";
	public void SetEnvironmentFxFactor(float value, float smoothing)
	{
		targetEnvironmentFxFactor = Mathf.Clamp(value, 0f, 1f);
		environmentFxSmoothing = smoothing;
	}

	private void UpdateEnvironmentFXFactor()
	{
		if (Mathf.IsZeroApprox(environmentFxSmoothing))
		{
			environmentFxFactor = targetEnvironmentFxFactor;
			environmentFxVelocity = 0;
		}
		else
		{
			environmentFxFactor = ExtensionMethods.SmoothDamp(environmentFxFactor, targetEnvironmentFxFactor, ref environmentFxVelocity, environmentFxSmoothing);
		}

		RenderingServer.GlobalShaderParameterSet(ShaderEnvironmentFXParameter, environmentFxFactor);
	}
}

public struct SpawnData(Node parent, Transform3D transform)
{
	/// <summary> Original parent node. </summary>
	public Node parentNode = parent;
	/// <summary> Local transform to spawn with. </summary>
	public Transform3D spawnTransform = transform;

	public readonly void Respawn(Node3D n)
	{
		if (parentNode != null && n.GetParent() != parentNode)
		{
			if (n.IsInsideTree()) // Object needs to be reparented first.
				n.GetParent().RemoveChild(n);

			parentNode.AddChild(n);
		}

		n.Visible = true;
		n.ProcessMode = Node.ProcessModeEnum.Inherit;
		n.SetDeferred("transform", spawnTransform);
	}
}
