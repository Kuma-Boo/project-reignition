using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Moves player with moving platforms.
	/// </summary>
	public partial class PlatformTrigger : Area3D
	{
		private bool isActive;
		private bool isInteractingWithPlayer;

		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			Connect(SignalName.AreaEntered, new Callable(this, MethodName.OnEntered));
			Connect(SignalName.AreaExited, new Callable(this, MethodName.OnExited));
		}

		public override void _Process(double _)
		{
			if (isInteractingWithPlayer)
			{
				if (Character.IsOnGround && Character.GlobalPosition.Y >= GlobalPosition.Y)
					isActive = true;
			}

			if (!isActive) return;

			if (Character.GlobalPosition.Y < GlobalPosition.Y || Character.IsOnGround)
				Character.GlobalTranslate(Vector3.Up * (GlobalPosition.Y - Character.GlobalPosition.Y));
			else if (Character.VerticalSpd < 0) //Player is falling
				isActive = false;
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
