using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Manager responsible for stage setup.
	/// This is the first thing that gets loaded in a stage.
	/// </summary>
	public partial class StageSettings : Node3D
	{
		public static StageSettings instance;

		public override void _EnterTree()
		{
			instance = this; //Always override previous instance
			if (cameraDemo != null)
			{
				_cameraDemo = GetNode<AnimationPlayer>(cameraDemo);
				_cameraDemo.Connect("animation_changed", new Callable(this, nameof(CameraDemoAdvanced)));
			}

			SetUpMission();
		}

		public override void _PhysicsProcess(double _) => UpdateTime();

		#region Stage Settings
		[Export]
		public int scoreRequirement; //Requirement for score rank
		[Export]
		public float timeRequirement; //Requirement for time rank. Format is [minutes.seconds (.5 is 30 seconds)]
		[Export]
		public int targetObjectiveCount; //What's the target amount for the current objective?
		[Export]
		public MissionType missionType; //Type of mission
		public enum MissionType
		{
			Objective, //For Get to the Goal stages, add a goal node and set the objective count to 0.
			Ring, //Collect a certain amount of rings
			Enemy, //Destroy a certain amount of enemies
		}
		private void SetUpMission()
		{
			/*
			switch (missionType)
			{
				case MissionType.Ring:
					break;
				case MissionType.Enemy:
					Array rings = GetTree().GetNodesInGroup("enemy");
					break;
			}
			*/
		}
		#endregion

		#region Stage Data
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
			Grind, //Obtained by continuously grinding
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
			GD.Print("Objective is now " + CurrentObjectiveCount);

			if (CurrentObjectiveCount >= targetObjectiveCount)
				FinishStage(true);
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
			else if (missionType == MissionType.Ring && CurrentRingCount >= targetObjectiveCount) //For ring based missions
			{
				CurrentRingCount = targetObjectiveCount; //Clamp
				FinishStage(true);
			}
			EmitSignal(SignalName.RingChanged, amount);
		}

		//Time
		private bool isUpdatingTime = true;

		[Signal]
		public delegate void TimeChangedEventHandler(); //Time has changed.

		public float CurrentTime { get; private set; } //How long has the player been on this stage?
		public string DisplayTime { get; private set; } //Current time formatted in mm:ss.ff

		private const string TIME_LABEL_FORMAT = "mm':'ss'.'ff";
		private void UpdateTime()
		{
			if (!isUpdatingTime) return;

			CurrentTime += PhysicsManager.physicsDelta; //Add current time
			System.TimeSpan time = System.TimeSpan.FromSeconds(CurrentTime);
			DisplayTime = time.ToString(TIME_LABEL_FORMAT);
			EmitSignal(SignalName.TimeChanged);
		}

		//Completion
		[Export]
		public StageCompletionType completionType;
		public enum StageCompletionType
		{
			Crossfade, //Instantly crossfade to the camera demo
			Run, //Keep running forward, then crossfade
		}
		[Export]
		public LockoutResource CompletionControlLockout;
		[Export]
		public NodePath cameraDemo; //Camera3D demo that gets enabled after the stage clears
		private AnimationPlayer _cameraDemo;

		[Signal]
		public delegate void StageCompletedEventHandler(bool isSuccess); //Stage was completed
		public void FinishStage(bool isSuccess)
		{
			//Asssign rank
			//GameplayInterface.instance.Score;
			if (_cameraDemo != null)
			{
				CameraDemoAdvanced(string.Empty, string.Empty);
				_cameraDemo.Play("demo1");

				//Hide everything so shadows don't render
				Visible = false;

				//Disable all object nodes that aren't parented to this node
				Array<Node> nodes = GetTree().GetNodesInGroup("cull on complete");
				for (int i = 0; i < nodes.Count; i++)
				{
					if (nodes[i] is Node3D spatial)
						spatial.Visible = false;
				}
			}

			isUpdatingTime = false;
			EmitSignal(SignalName.StageCompleted, isSuccess);
		}

		//Camera3D demo was advanced, play a crossfade
		private void CameraDemoAdvanced(string _, string _newAnim) => CharacterController.instance.Camera.StartCrossfade();
		#endregion

		#region Object Spawning
		[Signal]
		public delegate void RespawnedEventHandler();
		[Signal]
		public delegate void StageUnloadEventHandler();
		private const string RESPAWN_FUNCTION = "Respawn";

		public void RegisterRespawnableObject(Node node)
		{
			if (!IsConnected(SignalName.StageUnload, new Callable(node, "queue_free"))) //Prevent memory leaks
				Connect(SignalName.StageUnload, new Callable(node, "queue_free"));

			if (!node.HasMethod(RESPAWN_FUNCTION))
			{
				GD.PrintErr($"Node {node.Name} doesn't have a function 'Respawn!'");
				return;
			}

			if (!IsConnected(SignalName.Respawned, new Callable(node, RESPAWN_FUNCTION)))
				Connect(SignalName.Respawned, new Callable(node, RESPAWN_FUNCTION));
		}

		public void RespawnObjects()
		{
			SoundManager.instance.CancelDialog(); //Cancel any active dialog
			EmitSignal(SignalName.Respawned);
		}

		public override void _ExitTree()
		{
			EmitSignal(SignalName.StageUnload);
		}
		#endregion

	}
}
