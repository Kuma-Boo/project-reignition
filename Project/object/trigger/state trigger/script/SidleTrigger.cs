using Godot;

namespace Project.Gameplay
{
    public class SidleTrigger : StageTriggerModule
    {
		[Export]
		public CharacterController.MovementStates targetState;
		private bool isStateChanged;

		public override void Activate()
		{
			if (isStateChanged) return; //State change already applied

			isStateChanged = true;
			//Apply state
		}

		public override void Deactivate(bool isMovingForward)
		{
			GD.Print(isMovingForward);
			if (isMovingForward) return; //Keep state change

			isStateChanged = false;
		}
	}
}
