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

		public override void Activate()
		{
			Character.AddLockoutData(lockoutData);

			if (!Character.IsConnected(CharacterController.SignalName.Defeated, new(this, MethodName.Deactivate)))
				Character.Connect(CharacterController.SignalName.Defeated, new(this, MethodName.Deactivate), (uint)ConnectFlags.OneShot + (uint)ConnectFlags.Deferred);
		}

		public override void Deactivate()
		{
			Character.RemoveLockoutData(lockoutData);

			if (Character.IsConnected(CharacterController.SignalName.Defeated, new(this, MethodName.Deactivate)))
				Character.Disconnect(CharacterController.SignalName.Defeated, new(this, MethodName.Deactivate));
		}
	}
}
