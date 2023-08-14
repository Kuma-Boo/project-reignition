using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Simply calls LevelSettings.FinishLevel() when triggered.
	/// </summary>
	public partial class Goal : Area3D
	{
		private StageSettings Level => StageSettings.instance;

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Level.MissionType == StageSettings.MissionTypes.None)
				Level.FinishLevel(true); //Mission was simply to reach the goal
			else if (Level.MissionType == StageSettings.MissionTypes.Objective)
				Level.FinishLevel(false); //Failed to complete the objective.
			else if (Level.MissionObjectiveCount == 0) //For no pearls, ringless, stealth, etc.
				StageSettings.instance.FinishLevel(Level.CurrentObjectiveCount == 0);
		}
	}
}
