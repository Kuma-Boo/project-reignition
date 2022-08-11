using Godot;

namespace Project.Gameplay.Objects
{
	public class Goal : Area
	{
		public void OnPlayerEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			//End stage
			StageSettings.instance.FinishStage(StageSettings.instance.missionType == StageSettings.MissionType.Objective);
		}
	}
}
