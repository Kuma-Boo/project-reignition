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

			/*
			REFACTOR TODO
			if (!Player.Skills.IsSpeedBreakActive && Player.ActionState != PlayerController.ActionStates.JumpDash) return;

			if (Player.ActionState == PlayerController.ActionStates.JumpDash) // Bounce the player if necessary
				Player.Lockon.StartBounce();
			*/
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
