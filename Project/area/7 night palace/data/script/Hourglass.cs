using Godot;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Behaviour of the Hourglass found in Night Palace.
	/// </summary>
	public partial class Hourglass : TeleportTrigger
	{
		[ExportGroup("Components")]
		[Export]
		private EventTrigger eventHandler;
		private bool isInteractingWithPlayer;

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;
			if (!Character.Skills.IsSpeedBreakActive && Character.ActionState != CharacterController.ActionStates.JumpDash) return;

			if (Character.ActionState == CharacterController.ActionStates.JumpDash) // Bounce the player if necessary
				Character.Lockon.StartBounce();
			eventHandler.Activate();
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = true;
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = false;
		}
	}
}
