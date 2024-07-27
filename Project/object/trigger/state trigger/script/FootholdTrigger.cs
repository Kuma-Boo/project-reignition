using Godot;

namespace Project.Gameplay.Triggers
{
	public partial class FootholdTrigger : Area3D
	{
		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;

			SidleTrigger.CurrentFoothold = this;
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;

			if (SidleTrigger.CurrentFoothold == this)
				SidleTrigger.CurrentFoothold = null;
		}
	}
}
