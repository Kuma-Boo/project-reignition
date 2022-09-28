using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Starts a sidle.
	/// </summary>
	public partial class SidleTrigger : Area3D
	{
		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area3D _)
		{
			//Apply state
			Character.StartSidle();
		}

		public void OnExited(Area3D _)
		{
			if (Character.PathFollower.IsAheadOfPoint(GlobalPosition)) return; //Keep state change
			Character.CancelMovementState(CharacterController.MovementStates.Sidle);
		}
	}
}
