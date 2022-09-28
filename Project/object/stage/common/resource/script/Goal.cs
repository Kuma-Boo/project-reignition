using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Goal : Area3D
	{
		public void OnPlayerEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			//End stage
			StageSettings.instance.FinishStage(StageSettings.instance.missionType == StageSettings.MissionType.Objective);
		}
	}
}
