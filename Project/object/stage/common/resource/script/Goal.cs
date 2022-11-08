using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Goal : Area3D
	{
		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			//End stage
			StageSettings.instance.FinishStage(StageSettings.instance.MissionType == StageSettings.MissionTypes.Objective);
		}
	}
}
