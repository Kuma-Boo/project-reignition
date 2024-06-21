using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a ControlLockoutResource. Use this for automated sections where the player still needs some control.
	/// </summary>
	public partial class LockoutTrigger : StageTriggerModule
	{
		[Export]
		public LockoutResource lockoutData;

		public override void Activate() => Character.AddLockoutData(lockoutData);
		public override void Deactivate() => Character.RemoveLockoutData(lockoutData);
	}
}
