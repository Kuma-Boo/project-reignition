using Godot;

namespace Project.Gameplay.Objects;

/// <summary>
/// Simply calls LevelSettings.FinishLevel() when triggered.
/// </summary>
public partial class Goal : Area3D
{
	private StageSettings Stage => StageSettings.Instance;

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		Activate();
	}

	public void Activate()
	{
		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.None)
		{
			Stage.FinishLevel(true); // Mission was simply to reach the goal
			return;
		}

		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Race)
		{
			Stage.FinishLevel(Stage.IsRaceActive); // Mission was simply to reach the goal
			return;
		}

		if (Stage.Data.MissionObjectiveCount == 0) // For no pearls, ringless, stealth, etc.
		{
			Stage.FinishLevel(Stage.CurrentObjectiveCount == 0);
			return;
		}

		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain)
		{
			Stage.FinishLevel(Stage.CurrentObjectiveCount >= Stage.Data.MissionObjectiveCount);
			return;
		}

		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective || Stage.Data.MissionObjectiveCount != 0)
			Stage.FinishLevel(false); // Failed to complete the objective.
	}
}