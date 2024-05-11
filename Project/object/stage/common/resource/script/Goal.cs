using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Simply calls LevelSettings.FinishLevel() when triggered.
	/// </summary>
	public partial class Goal : Area3D
	{
		private StageSettings Stage => StageSettings.instance;

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Stage.Data.MissionType == LevelDataResource.MissionTypes.None)
				Stage.FinishLevel(true); // Mission was simply to reach the goal
			else if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective)
				Stage.FinishLevel(false); // Failed to complete the objective.
			else if (Stage.Data.MissionObjectiveCount == 0) // For no pearls, ringless, stealth, etc.
				StageSettings.instance.FinishLevel(Stage.CurrentObjectiveCount == 0);
		}
	}
}
