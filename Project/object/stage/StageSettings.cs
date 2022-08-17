using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Manager responsible for stage setup.
	/// This is the first thing that gets loaded in a stage.
	/// </summary>
	public class StageSettings : Node
	{
		public static StageSettings instance;

		public override void _EnterTree()
		{
			instance = this; //Always override previous instance

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
		public float CurrentTime { get; private set; } //How long has the player been on this stage?
		public int CurrentObjectiveCount { get; private set; } //How much has the player currently completed?
		public int CurrentRingCount { get; private set; } //How many rings is the player currently holding?
		public int CurrentScore { get; private set; } //How high is the current score?

		[Signal]
		public delegate void ObjectiveChanged(); //Progress towards the objective has changed
		[Signal]
		public delegate void RingChanged(int change); //Ring count has changed
		[Signal]
		public delegate void ScoreChanged(); //Score has changed, normally occours from a bonus
		[Signal]
		public delegate void TimeChanged(); //Time has changed.

		public void IncrementObjective()
		{
			CurrentObjectiveCount++;
			EmitSignal(nameof(ObjectiveChanged));
			GD.Print("Objective is now " + CurrentObjectiveCount);

			if (CurrentObjectiveCount >= targetObjectiveCount)
				FinishStage(true);
		}

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

		private void UpdateTime()
		{
			CurrentTime += PhysicsManager.normalDelta; //Add current time
			EmitSignal(nameof(TimeChanged));
		}

		public void FinishStage(bool isSuccess)
		{
			if (isSuccess)
			{
				GD.Print("Stage Complete.");
				return;
			}

			//Asssign rank
			//GameplayInterface.instance.Score;
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
