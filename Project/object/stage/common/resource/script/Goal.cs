using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Simply calls StageSettings.FinishStage() when triggered.
	/// </summary>
	public partial class Goal : Area3D
	{
		private StageSettings Stage => StageSettings.instance;

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Stage.MissionType == StageSettings.MissionTypes.None)
				Stage.FinishStage(true); //Mission was simply to reach the goal
			else if (Stage.MissionType == StageSettings.MissionTypes.Objective)
				Stage.FinishStage(false); //Failed to complete the objective.
			else if (Stage.ObjectiveCount == 0) //For no pearls, ringless, stealth, etc.
				StageSettings.instance.FinishStage(Stage.CurrentObjectiveCount == 0);
		}
	}
}
