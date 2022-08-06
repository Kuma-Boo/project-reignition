using Godot;

namespace Project.Gameplay.Triggers
{
	public class SidleTrigger : Area
	{
		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			//Apply state
			Character.StartSidle();
		}

		public void OnExited(Area a)
		{
			if (!a.IsInGroup("player")) return;

			if (Character.PathFollower.IsAheadOfPoint(GlobalTranslation)) return; //Keep state change
			Character.CancelMovementState(CharacterController.MovementStates.Sidle);
		}
	}
}
