using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Manager responsible for stage setup.
	/// This is the first thing that gets loaded in a stage.
	/// </summary>
	public class StageSettings : Spatial
	{
		public static StageSettings instance;

		public override void _EnterTree()
		{
			instance = this; //Always override previous instance
			if (cameraDemo != null)
			{
				_cameraDemo = GetNode<AnimationPlayer>(cameraDemo);
				_cameraDemo.Connect("animation_changed", this, nameof(CameraDemoAdvanced));
			}

			SetUpMission();
			SetUpSkills();
		}

		public override void _Process(float _) => UpdateTime();

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
		public delegate void ScoreChanged(); //Score has changed, normally occours from a bonus
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
			EmitSignal(nameof(ScoreChanged));
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
		public delegate void BonusAdded(BonusType type);
		public void AddBonus(BonusType type)
		{
			switch (type)
			{
				default:
					break;
			}


			EmitSignal(nameof(BonusAdded), type);
		}

		//Objectives
		public int CurrentObjectiveCount { get; private set; } //How much has the player currently completed?
		[Signal]
		public delegate void ObjectiveChanged(); //Progress towards the objective has changed
		public void IncrementObjective()
		{
			CurrentObjectiveCount++;
			EmitSignal(nameof(ObjectiveChanged));
			GD.Print("Objective is now " + CurrentObjectiveCount);

			if (CurrentObjectiveCount >= targetObjectiveCount)
				FinishStage(true);
		}

		//Rings
		public int CurrentRingCount { get; private set; } //How many rings is the player currently holding?
		[Signal]
		public delegate void RingChanged(int change); //Ring count has changed
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

			EmitSignal(nameof(RingChanged), amount);
		}


		//Time
		private bool isUpdatingTime = true;

		[Signal]
		public delegate void TimeChanged(); //Time has changed.

		public float CurrentTime { get; private set; } //How long has the player been on this stage?
		public string DisplayTime { get; private set; } //Current time formatted in mm:ss.ff

		private const string TIME_LABEL_FORMAT = "mm':'ss'.'ff";
		private void UpdateTime()
		{
			if (!isUpdatingTime) return;

			CurrentTime += PhysicsManager.normalDelta; //Add current time
			System.TimeSpan time = System.TimeSpan.FromSeconds(CurrentTime);
			DisplayTime = time.ToString(TIME_LABEL_FORMAT);
			EmitSignal(nameof(TimeChanged));
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
		public ControlLockoutResource CompletionControlLockout;
		[Export]
		public NodePath cameraDemo; //Camera demo that gets enabled after the stage clears
		private AnimationPlayer _cameraDemo;

		[Signal]
		public delegate void StageCompleted(bool isSuccess); //Stage was completed
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
				Array nodes = GetTree().GetNodesInGroup("cull on complete");
				for (int i = 0; i < nodes.Count; i++)
				{
					if (nodes[i] is Spatial spatial)
						spatial.Visible = false;
				}
			}

			isUpdatingTime = false;
			EmitSignal(nameof(StageCompleted), isSuccess);
		}

		private void CameraDemoAdvanced(string _, string newAnim) //Camera demo was advanced, play a crossfade
		{
			TransitionManager.StartTransition(new TransitionData()
			{
				type = TransitionData.Type.Crossfade,
				inSpeed = .5f,
			});
		}
		#endregion


		#region Skills
		public static SphereShape PearlCollisionShape = new SphereShape();
		public static SphereShape RichPearlCollisionShape = new SphereShape();
		public static RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		private const float PEARL_ATTRACTOR_MULTIPLIER = .5f;

		private const int ENEMY_PEARL_AMOUNT = 16; //How many pearls are obtained when defeating an enemy

		private void SetUpSkills()
		{
			//TODO Expand hitbox if skills is equipped
			PearlCollisionShape.Radius = PEARL_NORMAL_COLLISION;
			RichPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION;
			if (SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.PearlAttractor))
			{
				PearlCollisionShape.Radius *= PEARL_ATTRACTOR_MULTIPLIER;
				RichPearlCollisionShape.Radius *= PEARL_ATTRACTOR_MULTIPLIER;
			}
		}
		#endregion

		#region Object Spawning
		[Signal]
		public delegate void Respawned();
		[Signal]
		public delegate void StageUnload();
		private const string RESPAWN_FUNCTION = "Respawn";

		public void RegisterRespawnableObject(Node node)
		{
			if (!IsConnected(nameof(StageUnload), node, "queue_free")) //Prevent memory leaks
				Connect(nameof(StageUnload), node, "queue_free");

			if (!node.HasMethod(RESPAWN_FUNCTION))
			{
				GD.PrintErr($"Node {node.Name} doesn't have a function 'Respawn!'");
				return;
			}

			if (!IsConnected(nameof(Respawned), node, RESPAWN_FUNCTION))
				Connect(nameof(Respawned), node, RESPAWN_FUNCTION);
		}

		public void RespawnObjects()
		{
			SoundManager.instance.CancelDialog(); //Cancel any active dialog
			EmitSignal(nameof(Respawned));
		}

		public override void _ExitTree()
		{
			EmitSignal(nameof(StageUnload));
		}
		#endregion

		#region Music
		public bool MusicPaused
		{
			get => BGMPlayer.instance == null || BGMPlayer.instance.StreamPaused;
			set
			{
				if (BGMPlayer.instance == null) return;
				BGMPlayer.instance.StreamPaused = value;
			}
		}

		public void SetMusicVolume(float db)
		{
			if (BGMPlayer.instance == null) return;
			BGMPlayer.instance.VolumeDb = db;
		}
		#endregion
	}
}
