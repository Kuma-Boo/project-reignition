using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Parent class for all stage trigger modules.
	/// Connect a signal to Activate() or Deactivate(), or use a StageTrigger to automatically assign signals at runtime.
	/// </summary>
	public partial class StageTriggerModule : Node3D
	{
		protected PlayerController Player => StageSettings.Player;

		public virtual void Activate(Area3D a)
		{
			if (!a.IsInGroup("player detection"))
				return;

			Activate();
		}
		public virtual void Deactivate(Area3D a)
		{
			if (!a.IsInGroup("player detection"))
				return;

			Deactivate();
		}

		public virtual void Activate() { }
		public virtual void Deactivate() { }
		public virtual void Respawn() { }
	}
}
