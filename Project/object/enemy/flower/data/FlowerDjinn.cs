using Godot;
using Godot.Collections;

namespace Project.Gameplay
{
	/// <summary> Flower Majin that spits out seeds to attack the player. </summary>
	public partial class FlowerDjinn : Enemy
	{
		private readonly Seed[] seedPool = new Seed[3]; //Only three seeds can be spawned at a time.

		// Called when the node enters the scene tree for the first time.
		public override void _Ready()
		{
		}

		protected override void ProcessEnemy()
		{

		}
	}
}
