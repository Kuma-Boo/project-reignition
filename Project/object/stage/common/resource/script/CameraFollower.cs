using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Moves the object with the current camera.
	/// </summary>
	public partial class CameraFollower : Node3D
	{
		[Export] private bool followYRotation;
		private PlayerController Player => StageSettings.Player;

		public override void _Process(double _)
		{
			if (Player.IsDefeated)
				return;

			Camera3D camera = GetTree().Root.GetCamera3D();
			GlobalPosition = camera.GlobalPosition;

			if (followYRotation)
				GlobalRotation = Vector3.Up * camera.GlobalRotation.Y;
		}
	}
}
