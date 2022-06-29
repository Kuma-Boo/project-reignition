using Godot;

namespace Project.Gameplay
{
	public class LockoutTrigger : StageTriggerObject
	{
		[Export]
		public ControlLockoutResource lockoutData; //Leave empty to make this a RESET trigger.

		public override void Activate()
		{
			Character.SetControlLockout(lockoutData);
		}

		public override void Deactivate(bool isMovingForward)
		{
			Character.SetControlLockout(null);
		}
	}
}
