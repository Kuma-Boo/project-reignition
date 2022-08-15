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

		#region Stage Settings
		[Export]
		public MissionType missionType; //Type of mission
		public enum MissionType
		{
			Objective, //For Get to the Goal stages, add a goal node and set the objective count to 0.
			Ring, //Collect a certain amount of rings
			Enemy, //Destroy a certain amount of enemies
		}

		[Export]
		public int targetObjectiveCount;
		public int CurrentObjectiveCount { get; private set; }

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

		public void IncrementObjective()
		{
			CurrentObjectiveCount++;
			GD.Print("Objective is now " + CurrentObjectiveCount);

			if(CurrentObjectiveCount == targetObjectiveCount)
				FinishStage(true);
		}

		/*
		Ranking system

		Score is calculated as
		(Action bonus + Enemy Bonus + Ring Bonus) * Technical Bonus

		Action Bonus:
		Pearls - 1 point, Resets
		Rings - 5 points
		Grinding- 10 point every two meters on the rail multiplied by the 
		number of rails (I think... can't understand the site...), 20 points per 
		trick (leaning on a rail or changing rails.)
		Time Break- 30 points first 3 seconds, then 10 points for each second 
		afterwards.
		Speed Break- 10 points for each meter multiplied by number of times 
		Speed Break was used (I think that's what the official site is trying 
		to say >_>;)
		
		Ring Bonus: (Number of Rings / Max Rings) * 1000. Note that skills that give rings will provide more leeway

		Enemy Bonus:
		50 points per enemy, 1,000-4,000 for each Boss

		Technical Bonus goes down each time you are hit or you die. If you 
		fail a mission, Technical bonus is below 1.
		No damage: x2
		Damage once: x1.5
		2-3 times: x1.2
		4-5 times: x1.1
		6 or more times, or you died: X1
		Failed Mission: x0.3

		Mission Bonus: Received the very first time you beat a mission and is 
		a set number, if you replay that mission you can't get it again.

		Getting a gold medal requires all requirements at once.
		- Beat the time record
		- Break the score record
			- Doing this will almost certainly require a deathless run.
		*/
		[Export]
		public int scoreRequirement; //Requirement for score rank
		[Export]
		public int timeRequirement; //Requirement (in seconds) for time rank
		[Export]
		public int goldRankScore;

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
	}
}
