using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Moves the skybox with the current camera.
	/// </summary>
	public partial class Skybox : Node3D
	{
		private CharacterController Character => CharacterController.instance;

		public override void _Process(double _)
		{
			if (Character.IsDefeated)
				return;

			GlobalPosition = GetTree().Root.GetCamera3D().GlobalPosition;
		}
	}
}
