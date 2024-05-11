using Godot;
using Godot.Collections;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Stage settings.
	/// Must be the first thing loaded in a level.
	/// </summary>
	[Tool]
	public partial class StageSettings : Node3D
	{
		public static StageSettings instance;

		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Level Data", Variant.Type.Object),
				ExtensionMethods.CreateProperty("Camera Settings", Variant.Type.Object),
				ExtensionMethods.CreateProperty("Dialog Library", Variant.Type.Object),
				ExtensionMethods.CreateProperty("Ranking/Skip Score", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Ranking/Gold Time", Variant.Type.Int),
				ExtensionMethods.CreateProperty("Ranking/Silver Time", Variant.Type.Int),
				ExtensionMethods.CreateProperty("Ranking/Bronze Time", Variant.Type.Int)
			};

			if (!skipScore)
			{
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Gold Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Silver Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Bronze Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,100"));
			}

			properties.Add(ExtensionMethods.CreateProperty("Completion/Delay", Variant.Type.Float, PropertyHint.Range, "0,2.5,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Completion/Lockout", Variant.Type.Object));
			properties.Add(ExtensionMethods.CreateProperty("Completion/Demo Animator", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "AnimationPlayer"));

			properties.Add(ExtensionMethods.CreateProperty("Static/Path Parent", Variant.Type.NodePath));
			properties.Add(ExtensionMethods.CreateProperty("Static/Environment", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "WorldEnvironment"));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Level Data":
					return Data;

				case "Camera Settings":
					return InitialCameraSettings;
				case "Dialog Library":
					return dialogLibrary;

				case "Ranking/Skip Score":
					return skipScore;
				case "Ranking/Gold Time":
					return goldTime;
				case "Ranking/Silver Time":
					return silverTime;
				case "Ranking/Bronze Time":
					return bronzeTime;
				case "Ranking/Gold Score":
					return goldScore;
				case "Ranking/Silver Score":
					return silverScore;
				case "Ranking/Bronze Score":
					return bronzeScore;

				case "Completion/Delay":
					return completionDelay;
				case "Completion/Lockout":
					return CompletionLockout;
				case "Completion/Demo Animator":
					return completionDemoAnimator;

				case "Static/Path Parent":
					return pathParent;
				case "Static/Environment":
					return environment;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Level Data":
					Data = (LevelDataResource)value;
					break;

				case "Camera Settings":
					InitialCameraSettings = (CameraSettingsResource)value;
					break;
				case "Dialog Library":
					dialogLibrary = (SFXLibraryResource)value;
					break;

				case "Ranking/Skip Score":
					skipScore = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Ranking/Gold Time":
					goldTime = (int)value;
					break;
				case "Ranking/Silver Time":
					silverTime = (int)value;
					break;
				case "Ranking/Bronze Time":
					bronzeTime = (int)value;
					break;
				case "Ranking/Gold Score":
					goldScore = (int)value;
					break;
				case "Ranking/Silver Score":
					silverScore = (int)value;
					break;
				case "Ranking/Bronze Score":
					bronzeScore = (int)value;
					break;

				case "Completion/Delay":
					completionDelay = (float)value;
					break;
				case "Completion/Lockout":
					CompletionLockout = (LockoutResource)value;
					break;
				case "Completion/Demo Animator":
					completionDemoAnimator = (NodePath)value;
					break;


				case "Static/Path Parent":
					pathParent = (NodePath)value;
					break;
				case "Static/Environment":
					environment = (NodePath)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		#region Path Settings
		private NodePath pathParent;
		/// <summary> List of all level paths contained for this level. </summary>
		private readonly Array<Path3D> pathList = new();

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

		/// <summary> Reference to active area's WorldEnvironment node. </summary>
		public WorldEnvironment Environment { get; private set; }
		private NodePath environment;

		[Export]
		public bool isControlTest;

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			instance = this; //Always override previous instance

			Environment = GetNodeOrNull<WorldEnvironment>(environment);

			Node pathParentNode = GetNode<Node>(pathParent);
			for (int i = 0; i < pathParentNode.GetChildCount(); i++)
			{
				Path3D path = pathParentNode.GetChildOrNull<Path3D>(i);
				if (path != null)
					pathList.Add(path);
			}

			UpdateScore(0, MathModeEnum.Replace);
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

			UpdateTime();
		}


		#region Level Settings
		/// <summary> Reference to the level's data. </summary>
		public LevelDataResource Data { get; private set; }
		public CameraSettingsResource InitialCameraSettings { get; private set; }
		public SFXLibraryResource dialogLibrary;

		// Rank
		private bool skipScore; // Don't use score when ranking (i.e. for bosses)

		// Requirements for time rank. Format is in seconds.
		private int goldTime;
		private int silverTime;
		private int bronzeTime;
		// Requirement for score rank
		private int goldScore;
		private int silverScore;
		private int bronzeScore;

		/// <summary>
		/// Calculates the rank [Fail = -1, None = 0, Bronze = 1, Silver = 2, Gold = 3]
		/// </summary>
		public int CalculateRank()
		{
			if (LevelState == LevelStateEnum.Failed)
				return -1;

			int rank = 0; // DEFAULT - No rank

			if (skipScore)
			{
				if (CurrentTime <= goldTime)
					rank = 3;
				else if (CurrentTime <= silverTime)
					rank = 2;
				else if (CurrentTime <= bronzeTime)
					rank = 1;
			}
			else if (CurrentTime <= bronzeTime && CurrentScore >= bronzeScore)
			{
				if (CurrentTime <= goldTime && CurrentScore >= silverScore)
					rank = 3;
				else if (CurrentTime >= silverTime || CurrentScore <= silverScore)
					rank = 1;
				else
					rank = 2;
			}

			if (rank >= 3 && RespawnCount != 0) // Limit to silver if a respawn occured
				rank = 2;

			return rank;
		}
		#endregion

		#region Level Data
		public enum MathModeEnum // List of ways the score can be modified
		{
			Add,
			Subtract,
			Multiply,
			Replace
		}
		/// <summary>
		/// Calculates value based on provided MathMode.
		/// </summary>
		private int CalculateMath(int value, int amount, MathModeEnum mode)
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
		public void UpdateScore(int amount, MathModeEnum mode)
		{
			CurrentScore = CalculateMath(CurrentScore, amount, mode);
			DisplayScore = ExtensionMethods.FormatScore(CurrentScore);
			EmitSignal(SignalName.ScoreChanged);
		}

		public int RespawnCount { get; private set; } // How high many times did the player have to respawn?
		public void IncrementRespawnCount() => RespawnCount++;

		//Objectives
		public int CurrentObjectiveCount { get; private set; } // How much has the player currently completed?
		[Signal]
		public delegate void ObjectiveChangedEventHandler(); // Progress towards the objective has changed
		public void IncrementObjective()
		{
			CurrentObjectiveCount++;
			EmitSignal(SignalName.ObjectiveChanged);

			if (Data.MissionObjectiveCount == 0) // i.e. Sand Oasis's "Don't break the jars!" mission.
				FinishLevel(false);
			else if (CurrentObjectiveCount >= Data.MissionObjectiveCount)
				FinishLevel(true);
		}

		// Rings
		public int CurrentRingCount { get; private set; } // How many rings is the player currently holding?
		[Signal]
		public delegate void RingChangedEventHandler(int change); // Ring count has changed
		public void UpdateRingCount(int amount, MathModeEnum mode, bool disableAnimations = false)
		{
			int previousAmount = CurrentRingCount;
			CurrentRingCount = CalculateMath(CurrentRingCount, amount, mode);
			if (Data.MissionType == LevelDataResource.MissionTypes.Ring && CurrentRingCount >= Data.MissionObjectiveCount) // For ring based missions
			{
				CurrentRingCount = Data.MissionObjectiveCount; // Clamp
				FinishLevel(true);
			}

			if (DebugManager.Instance.InfiniteRings) // Infinite ring cheat
				CurrentRingCount = 999;

			EmitSignal(SignalName.RingChanged, CurrentRingCount - previousAmount, disableAnimations);
		}




		public int CurrentEXP { get; private set; } // How much exp is the player earning from this stage?

		// Time
		[Signal]
		public delegate void TimeChangedEventHandler(); // Time has changed.

		public float CurrentTime { get; private set; } // How long has the player been on this level? (In Seconds)
		public string DisplayTime { get; private set; } // Current time formatted in mm:ss.ff
		private void UpdateTime()
		{
			if (IsLevelFinished || !Interface.PauseMenu.AllowPausing) return;

			CurrentTime += PhysicsManager.physicsDelta; // Add current time
			if (Data.MissionTimeLimit == 0) // No time limit
				DisplayTime = ExtensionMethods.FormatTime(CurrentTime);
			else
			{
				DisplayTime = ExtensionMethods.FormatTime(Mathf.Clamp(Data.MissionTimeLimit - CurrentTime, 0, Data.MissionTimeLimit));
				if (CurrentTime >= Data.MissionTimeLimit) // Time's up!
					FinishLevel(false);
			}

			EmitSignal(SignalName.TimeChanged);
		}
		#endregion

		#region Level Completion
		[Signal]
		public delegate void LevelCompletedEventHandler(); // Called when the level is completed
		[Signal]
		public delegate void LevelDemoStartedEventHandler(); // Called when the level demo starts

		private float completionDelay;
		public enum LevelStateEnum
		{
			Incomplete,
			Failed,
			Success,
		}
		public LevelStateEnum LevelState { get; private set; }
		/// <summary> Control lockout to apply when the level is completed. Leave null to use Runtime.Instance.StopLockout. </summary>
		public LockoutResource CompletionLockout { get; private set; }
		private bool IsLevelFinished => LevelState != LevelStateEnum.Incomplete;
		private const float FAIL_COMPLETION_DELAY = 1.5f; // Mission fails always have a delay of 1.5 seconds
		public void FinishLevel(bool wasSuccessful)
		{
			// Attempt to start the completion demo
			GetTree().CreateTimer(wasSuccessful ? completionDelay : FAIL_COMPLETION_DELAY).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.StartCompletionDemo));

			BGMPlayer.StageMusicPaused = true;
			LevelState = wasSuccessful ? LevelStateEnum.Success : LevelStateEnum.Failed;

			EmitSignal(SignalName.LevelCompleted);
		}

		/// <summary> Camera demo that gets enabled after the level is cleared. </summary>
		public NodePath completionDemoAnimator;
		private AnimationPlayer completionAnimator;
		private void StartCompletionDemo()
		{
			EmitSignal(SignalName.LevelDemoStarted);

			completionAnimator = GetNodeOrNull<AnimationPlayer>(completionDemoAnimator);
			if (completionAnimator == null) return;

			OnCameraDemoAdvance();
			completionAnimator.Play("demo1");
		}

		/// <summary> Completion demo advanced, play a crossfade. </summary>
		public void OnCameraDemoAdvance()
		{
			StringName nextAnimation = completionAnimator.AnimationGetNext(completionAnimator.CurrentAnimation);
			if (completionAnimator.HasAnimation(nextAnimation))
				completionAnimator.Play(nextAnimation);
			CharacterController.instance.Camera.StartCrossfade();
		}

		#endregion

		#region Object Spawning
		// Checkpoint data
		[Signal]
		public delegate void OnTriggeredCheckpointEventHandler();
		public Triggers.CheckpointTrigger CurrentCheckpoint { get; private set; }
		public Path3D CheckpointPlayerPath { get; private set; }
		public Path3D CheckpointCameraPath { get; private set; }
		public CameraSettingsResource CheckpointCameraSettings;
		public void SetCheckpoint(Triggers.CheckpointTrigger newCheckpoint)
		{
			if (newCheckpoint == CurrentCheckpoint) return; // Already at this checkpoint

			CurrentCheckpoint = newCheckpoint; // Position transform
			CheckpointPlayerPath = CharacterController.instance.PathFollower.ActivePath; // Store current player path
			CheckpointCameraPath = CharacterController.instance.Camera.PathFollower.ActivePath; // Store current camera path
			CheckpointCameraSettings = CharacterController.instance.Camera.ActiveSettings;

			EmitSignal(SignalName.OnTriggeredCheckpoint);
		}


		[Signal]
		public delegate void OnUnloadedEventHandler();
		private const string UNLOAD_FUNCTION = "Unload"; // Clean up any memory leaks in this function
		public override void _ExitTree() => EmitSignal(SignalName.OnUnloaded);
		public void ConnectUnloadSignal(Node node)
		{
			if (!node.HasMethod(UNLOAD_FUNCTION))
			{
				GD.PrintErr($"Node {node.Name} doesn't have a function '{UNLOAD_FUNCTION}!'");
				return;
			}

			if (!IsConnected(SignalName.OnUnloaded, new Callable(node, UNLOAD_FUNCTION)))
				Connect(SignalName.OnUnloaded, new Callable(node, UNLOAD_FUNCTION));
		}


		[Signal]
		public delegate void OnRespawnedEventHandler();
		public readonly static StringName RESPAWN_FUNCTION = "Respawn"; // Default name of respawn functions
		public void ConnectRespawnSignal(Node node)
		{
			if (!node.HasMethod(RESPAWN_FUNCTION))
			{
				GD.PrintErr($"Node {node.Name} doesn't have a function '{RESPAWN_FUNCTION}!'");
				return;
			}

			if (!IsConnected(SignalName.OnRespawned, new Callable(node, RESPAWN_FUNCTION)))
				Connect(SignalName.OnRespawned, new Callable(node, RESPAWN_FUNCTION), (uint)ConnectFlags.Deferred);
		}


		public void RespawnObjects()
		{
			SoundManager.instance.CancelDialog(); // Cancel any active dialog
			EmitSignal(SignalName.OnRespawned);
		}

		#endregion
	}


	public struct SpawnData
	{
		/// <summary> Original parent node. </summary>
		public Node parentNode;
		/// <summary> Local transform to spawn with. </summary>
		public Transform3D spawnTransform;
		public SpawnData(Node parent, Transform3D transform)
		{
			parentNode = parent;
			spawnTransform = transform;
		}

		public void Respawn(Node n)
		{
			if (parentNode != null && n.GetParent() != parentNode)
			{
				if (n.IsInsideTree()) // Object needs to be reparented first.
					n.GetParent().RemoveChild(n);

				parentNode.CallDeferred(Node.MethodName.AddChild, n);
			}

			n.SetDeferred("transform", spawnTransform);
		}
	}
}