using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Moves player with moving platforms.
	/// </summary>
	public partial class PlatformTrigger : Area3D
	{
		[Signal]
		public delegate void PlatformInteractedEventHandler();

		private bool isActive;
		private bool isInteractingWithPlayer;
		[Export]
		public Node3D parentCollider;

		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (!isActive && Character.IsOnGround)
			{
				isActive = true;
				EmitSignal(SignalName.PlatformInteracted);
			}

			if (!isActive) return;

			float checkLength = Mathf.Abs(Character.GlobalPosition.Y - GlobalPosition.Y) + Character.CollisionRadius * 2.0f;
			KinematicCollision3D collision = Character.MoveAndCollide(Vector3.Down * checkLength, true);
			if (collision == null || (Node3D)collision.GetCollider() != parentCollider)
				isActive = false;
			else if (Character.IsOnGround)
				Character.GlobalTranslate(Vector3.Up * (GlobalPosition.Y - Character.GlobalPosition.Y));
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
			isActive = false;
		}
	}
}
