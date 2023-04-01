using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Simply calls LevelSettings.FinishLevel() when triggered.
	/// </summary>
	public partial class Goal : Area3D
	{
		private LevelSettings Level => LevelSettings.instance;

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Level.MissionType == LevelSettings.MissionTypes.None)
				Level.FinishLevel(true); //Mission was simply to reach the goal
			else if (Level.MissionType == LevelSettings.MissionTypes.Objective)
				Level.FinishLevel(false); //Failed to complete the objective.
			else if (Level.MissionObjectiveCount == 0) //For no pearls, ringless, stealth, etc.
				LevelSettings.instance.FinishLevel(Level.CurrentObjectiveCount == 0);
		}
	}
}
