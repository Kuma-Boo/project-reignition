using Godot;

namespace Project.Gameplay.Objects
{
	public partial class MajinEgg : Node3D
	{
		[Signal]
		public delegate void ShatteredEventHandler();

		public override void _Ready()
		{
		}

		public override void _Process(double delta)
		{
		}
	}
}
