using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Moves the object with the current camera.
	/// </summary>
	public partial class CameraFollower : Node3D
	{
		private PlayerController Player => StageSettings.Player;

		public override void _Process(double _)
		{
			if (Player.IsDefeated)
				return;

			GlobalPosition = GetTree().Root.GetCamera3D().GlobalPosition;
		}
	}
}
