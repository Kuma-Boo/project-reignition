using Godot;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Moves the skybox with the player.
	/// </summary>
	public partial class Skybox : Node3D
	{
		private CharacterController Character => CharacterController.instance;

		public override void _Process(double _)
		{
			GlobalPosition = Character.GlobalPosition;
		}
	}
}
