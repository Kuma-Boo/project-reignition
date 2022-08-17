using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Starts a sidle.
	/// </summary>
	public class SidleTrigger : Area
	{
		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area _)
		{
			//Apply state
			Character.StartSidle();
		}

		public void OnExited(Area _)
		{
			if (Character.PathFollower.IsAheadOfPoint(GlobalTranslation)) return; //Keep state change
			Character.CancelMovementState(CharacterController.MovementStates.Sidle);
		}
	}
}
