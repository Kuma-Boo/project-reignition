using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a ControlLockoutResource. Use this for automated sections where the player still needs some control.
	/// </summary>
	public class LockoutTrigger : StageTriggerModule
	{
		[Export]
		public ControlLockoutResource lockoutData; //Leave empty to make this a RESET trigger.

		public override void Activate() => Character.StartControlLockout(lockoutData);
		public override void Deactivate() => Character.StartControlLockout(null);
	}
}
