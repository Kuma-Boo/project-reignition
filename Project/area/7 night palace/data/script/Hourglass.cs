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

			if (!Player.Skills.IsSpeedBreakActive && !Player.IsJumpDashOrHomingAttack) return;

			if (Player.IsJumpDashOrHomingAttack) // Bounce the player if necessary
				Player.StartBounce();

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
