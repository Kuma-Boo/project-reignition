using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Manager responsible for level setup.
	/// This is the first thing that gets loaded in a level.
	/// </summary>
	[Tool]
	public partial class LevelSettings : Node3D
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Mission Type", Variant.Type.Int, PropertyHint.Enum, MissionType.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty("Time Limit", Variant.Type.Int, PropertyHint.Range, "0,640"));

			if (MissionType != MissionTypes.None && MissionType != MissionTypes.Race)
				properties.Add(ExtensionMethods.CreateProperty("Objective Count", Variant.Type.Int, PropertyHint.Range, "0,256"));

			properties.Add(ExtensionMethods.CreateProperty("Item Cycling/Activation Trigger", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Area3D"));
			if (itemCycleActivationTrigger != null && !itemCycleActivationTrigger.IsEmpty)
			{
				properties.Add(ExtensionMethods.CreateProperty("Item Cycling/Halfway Trigger", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Area3D"));
				properties.Add(ExtensionMethods.CreateProperty("Item Cycling/Enable Respawning", Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Item Cycling/Item Cycles", Variant.Type.Array, PropertyHint.TypeString, "22/0:"));
			}

			properties.Add(ExtensionMethods.CreateProperty("Ranking/Skip Score", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Gold Time", Variant.Type.Float, PropertyHint.Range, "0,10,.05"));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Silver Time", Variant.Type.Float, PropertyHint.Range, "0,10,.05"));
			properties.Add(ExtensionMethods.CreateProperty("Ranking/Bronze Time", Variant.Type.Float, PropertyHint.Range, "0,10,.05"));

			if (!skipScore)
			{
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Gold Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,10"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Silver Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,10"));
				properties.Add(ExtensionMethods.CreateProperty("Ranking/Bronze Score", Variant.Type.Int, PropertyHint.Range, "0,99999999,10"));
			}

			properties.Add(ExtensionMethods.CreateProperty("Completion/Delay", Variant.Type.Float, PropertyHint.Range, "0,4,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Completion/Lockout", Variant.Type.Object));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Mission Type":
					return (int)MissionType;
				case "Time Limit":
					return TimeLimit;
				case "Objective Count":
					return ObjectiveCount;

				case "Item Cycling/Activation Trigger":
					return itemCycleActivationTrigger;
				case "Item Cycling/Halfway Trigger":
					return itemCycleHalfwayTrigger;
				case "Item Cycling/Enable Respawning":
					return itemCycleRespawnEnabled;
				case "Item Cycling/Item Cycles":
					return itemCycles;

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
					return completionLockout;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Mission Type":
					MissionType = (MissionTypes)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Time Limit":
					TimeLimit = (int)value;
					break;
				case "Objective Count":
					ObjectiveCount = (int)value;
					break;

				case "Item Cycling/Activation Trigger":
					itemCycleActivationTrigger = (NodePath)value;
					NotifyPropertyListChanged();
					break;
				case "Item Cycling/Halfway Trigger":
					itemCycleHalfwayTrigger = (NodePath)value;
					break;
				case "Item Cycling/Enable Respawning":
					itemCycleRespawnEnabled = (bool)value;
					break;
				case "Item Cycling/Item Cycles":
					itemCycles = (Array<NodePath>)value;
					break;

				case "Ranking/Skip Score":
					skipScore = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Ranking/Gold Time":
					goldTime = (float)value;
					break;
				case "Ranking/Silver Time":
					silverTime = (float)value;
					break;
				case "Ranking/Bronze Time":
					bronzeTime = (float)value;
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
					completionLockout = (LockoutResource)value;
					break;
				default:
					return false;
			}

			return true;
		}
		#endregion

		public static LevelSettings instance;

		public override void _EnterTree()
		{
			if (Engine.IsEditorHint()) return;

			instance = this; //Always override previous instance
			SetUpItemCycles();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

			UpdateTime();
		}

		#region Level Settings
		private bool skipScore; //Don't use score when ranking (i.e. for bosses)

		//Requirements for time rank. Format is in minutes. (.5 is 30 seconds)
		private float goldTime;
		private float silverTime;
		private float bronzeTime;
		//Requirement for score rank
		private int goldScore;
		private int silverScore;
		private int bronzeScore;

		private float TimeLimit { get; set; } //Level time limit, in seconds
		public int ObjectiveCount { get; private set; } //What's the target amount for the current objective?
		public MissionTypes MissionType { get; private set; } //Type of mission
		public enum MissionTypes
		{
			None, //Add a goal node or a boss so the player doesn't get stuck!
			Objective, //Add custom nodes that call IncrementObjective()
			Ring, //Collect a certain amount of rings
			Pearl, //Collect a certain amount of pearls (normally zero)
			Enemy, //Destroy a certain amount of enemies
			Race, //Race against an enemy
		}
		#endregion

		#region Level Data
		public int CurrentScore { get; private set; } //How high is the current score?
		public string DisplayScore { get; private set; } //Current score formatted to eight zeros
		[Signal]
		public delegate void ScoreChangedEventHandler(); //Score has changed, normally occours from a bonus
		public enum ScoreFunction //List of ways the score can be modified
		{
			Add,
			Subtract,
			Multiply,
			Replace
		}
		private const string SCORE_FORMATTING = "00000000";
		public void ChangeScore(int amount, ScoreFunction func)
		{
			switch (func)
			{
				case ScoreFunction.Add:
					CurrentScore += amount;
					break;
				case ScoreFunction.Subtract:
					CurrentScore -= amount;
					if (CurrentScore < 0)
						CurrentScore = 0;
					break;
				case ScoreFunction.Multiply:
					CurrentScore *= amount;
					break;
				case ScoreFunction.Replace:
					CurrentScore = amount;
					break;
			}

			DisplayScore = CurrentScore.ToString(SCORE_FORMATTING);
			EmitSignal(SignalName.ScoreChanged);
		}

		//Bonuses
		public enum BonusType
		{
			PerfectHomingAttack, //Obtained by attacking an enemy using a perfect homing attack
			DriftBonus, //Obtained by performing a Drift
			GrindShuffle, //Obtained by performing a grind shuffle
			PerfectGrindShuffle, //Obtained by performing a perfect grind shuffle
			GrindStep, //Obtained by using the Grind Step
		}
		[Signal]
		public delegate void BonusAddedEventHandler(BonusType type);
		public void AddBonus(BonusType type)
		{
			switch (type)
			{
				default:
					break;
			}

			EmitSignal(SignalName.BonusAdded, (int)type);
		}

		//Objectives
		public int CurrentObjectiveCount { get; private set; } //How much has the player currently completed?
		[Signal]
		public delegate void ObjectiveChangedEventHandler(); //Progress towards the objective has changed
		public void IncrementObjective()
		{
			CurrentObjectiveCount++;
			EmitSignal(SignalName.ObjectiveChanged);

			if (ObjectiveCount == 0) //i.e. Sand Oasis's "Don't break the jars!" mission.
				FinishLevel(false);
			else if (CurrentObjectiveCount >= ObjectiveCount)
				FinishLevel(true);
		}

		//Rings
		public int CurrentRingCount { get; private set; } //How many rings is the player currently holding?
		[Signal]
		public delegate void RingChangedEventHandler(int change); //Ring count has changed
		public void UpdateRingCount(int amount)
		{
			CurrentRingCount += amount;
			if (CurrentRingCount < 0) //Clamp to zero
				CurrentRingCount = 0;
			else if (MissionType == MissionTypes.Ring && CurrentRingCount >= ObjectiveCount) //For ring based missions
			{
				CurrentRingCount = ObjectiveCount; //Clamp
				FinishLevel(true);
			}

			EmitSignal(SignalName.RingChanged, amount);
		}

		//Time
		[Signal]
		public delegate void TimeChangedEventHandler(); //Time has changed.

		public float CurrentTime { get; private set; } //How long has the player been on this level?
		public string DisplayTime { get; private set; } //Current time formatted in mm:ss.ff

		private const string TIME_LABEL_FORMAT = "mm':'ss'.'ff";
		private void UpdateTime()
		{
			if (isLevelFinished || Interface.Countdown.IsCountdownActive) return;

			CurrentTime += PhysicsManager.physicsDelta; //Add current time
			if (TimeLimit == 0) //No time limit
			{
				TimeSpan time = TimeSpan.FromSeconds(CurrentTime);
				DisplayTime = time.ToString(TIME_LABEL_FORMAT);
			}
			else
			{
				TimeSpan time = TimeSpan.FromSeconds(Mathf.Clamp(TimeLimit - CurrentTime, 0, TimeLimit));
				DisplayTime = time.ToString(TIME_LABEL_FORMAT);
				if (CurrentTime > TimeLimit)
					FinishLevel(false); //Time's up!
			}

			EmitSignal(SignalName.TimeChanged);
		}
		#endregion

		#region Level Completion
		private bool isLevelFinished;
		private float completionDelay;
		private LockoutResource completionLockout;

		[Signal]
		public delegate void LevelCompletedEventHandler(bool isSuccess); //Called when level is completed
		public void FinishLevel(bool isSuccess)
		{
			CharacterController.instance.AddLockoutData(completionLockout); //Lockout player

			//TODO Calculate rank
			//GameplayInterface.instance.Score;
			if (StageSettings.instance.StartCompletionDemo()) //Attempt to start the completion demo
			{
				//Hide everything so shadows don't render
				Visible = false;

				//Disable all object nodes that aren't parented to this node
				Array<Node> nodes = GetTree().GetNodesInGroup("cull on complete");
				for (int i = 0; i < nodes.Count; i++)
				{
					if (nodes[i] is Node3D node)
						node.Visible = false;
				}
			}

			isLevelFinished = true;
			EmitSignal(SignalName.LevelCompleted, isSuccess);
		}

		#endregion

		#region Object Spawning
		//Checkpoint data
		public Node3D CurrentCheckpoint { get; private set; }
		public Path3D CheckpointPath { get; private set; }
		public CameraSettingsResource CheckpointCamera;
		public void SetCheckpoint(Node3D newCheckpoint)
		{
			if (newCheckpoint == CurrentCheckpoint) return; //Already at this checkpoint

			CurrentCheckpoint = newCheckpoint; //Position transform
			CheckpointPath = CharacterController.instance.PathFollower.ActivePath; //Store current path
			CheckpointCamera = CameraController.instance.ActiveSettings;

			EmitSignal(SignalName.OnTriggeredCheckpoint);
		}
		[Signal]
		public delegate void OnTriggeredCheckpointEventHandler();

		[Signal]
		public delegate void OnUnloadedEventHandler();
		private const string UNLOAD_FUNCTION = "Unload"; //Clean up any memory leaks in this function
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
		private const string RESPAWN_FUNCTION = "Respawn"; //Default name of respawn functions
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
			SoundManager.instance.CancelDialog(); //Cancel any active dialog
			EmitSignal(SignalName.OnRespawned);
		}

		private bool itemCycleRespawnEnabled = true; //Respawn items when cycling?
		private bool itemCycleFlagSet; //Should we trigger an item switch?
		private int itemCycleIndex; //Active item set

		//Make sure itemCycle triggers are monitoring and collide with the player
		private NodePath itemCycleActivationTrigger; //When to apply the item cycle
		private NodePath itemCycleHalfwayTrigger; //So the player can't just move in and out of the activation trigger
		private Array<NodePath> itemCycles = new Array<NodePath>();
		private readonly List<Node3D> _itemCycles = new List<Node3D>();
		private readonly List<SpawnData> _itemCyclesSpawnData = new List<SpawnData>();

		private void SetUpItemCycles()
		{
			if (itemCycleActivationTrigger == null || itemCycleActivationTrigger.IsEmpty) return; //Item cycling disabled

			GetNode<Area3D>(itemCycleActivationTrigger).Connect(Area3D.SignalName.AreaEntered, new Callable(this, MethodName.OnItemCycleActivate));
			if (itemCycleHalfwayTrigger != null)
				GetNode<Area3D>(itemCycleHalfwayTrigger).Connect(Area3D.SignalName.AreaEntered, new Callable(this, MethodName.OnItemCycleHalfwayEntered));

			for (int i = 0; i < itemCycles.Count; i++)
			{
				if (itemCycles[i] == null || itemCycles[i].IsEmpty) //Nothing on this item cycle!
				{
					_itemCycles.Add(null);
					_itemCyclesSpawnData.Add(new SpawnData());
					continue;
				}

				Node3D node = GetNode<Node3D>(itemCycles[i]);
				SpawnData spawnData = new SpawnData(node.GetParent(), node.Transform);
				node.Visible = true;

				_itemCycles.Add(node);
				_itemCyclesSpawnData.Add(spawnData);

				if (i != itemCycleIndex) //Disable inactive nodes
					spawnData.parentNode.CallDeferred(MethodName.RemoveChild, node);
			}
		}

		public void OnItemCycleActivate(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			if (itemCycles.Count == 0 || !itemCycleFlagSet) return;

			//Cycle items
			if (itemCycleRespawnEnabled)
				RespawnObjects();

			if (_itemCycles[itemCycleIndex] != null) //Despawn current item cycle
				_itemCyclesSpawnData[itemCycleIndex].parentNode.CallDeferred(MethodName.RemoveChild, _itemCycles[itemCycleIndex]);

			//Increment counter
			itemCycleIndex++;
			if (itemCycleIndex > itemCycles.Count - 1)
				itemCycleIndex = 0;

			if (_itemCycles[itemCycleIndex] != null) //Spawn current item cycle
			{
				_itemCyclesSpawnData[itemCycleIndex].parentNode.AddChild(_itemCycles[itemCycleIndex]);
				_itemCycles[itemCycleIndex].Transform = _itemCyclesSpawnData[itemCycleIndex].spawnTransform;
			}

			itemCycleFlagSet = false;
		}

		public void OnItemCycleHalfwayEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			itemCycleFlagSet = true;
		}
		#endregion
	}

	public struct SpawnData
	{
		public Node parentNode; //Original parent node
		public Transform3D spawnTransform; //Local transform to spawn with
		public SpawnData(Node parent, Transform3D transform)
		{
			parentNode = parent;
			spawnTransform = transform;
		}

		public void Respawn(Node n)
		{
			if (n.GetParent() != parentNode)
			{
				if (n.IsInsideTree()) //Object needs to be unparented first.
					n.GetParent().CallDeferred("remove_child", n);

				parentNode.CallDeferred("add_child", n);
			}

			n.SetDeferred("transform", spawnTransform);
		}
	}
}
