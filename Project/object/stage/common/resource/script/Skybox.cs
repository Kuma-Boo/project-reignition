using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Moves the skybox with the current camera.
	/// </summary>
	public partial class Skybox : Node3D
	{
		private PlayerController Player => StageSettings.Player;

		public override void _Process(double _)
		{
			if (Player.State.IsDefeated)
				return;

			GlobalPosition = GetTree().Root.GetCamera3D().GlobalPosition;
		}
	}
}
