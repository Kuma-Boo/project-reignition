using Godot;

namespace Project.Gameplay.Objects
{
	public partial class FireSoul : Pickup
	{
		private bool isCollected; //Determined by save data

		protected override void SetUp() //TODO Check save data
		{
			if (isCollected)
				Despawn();
		}

		protected override void Collect()
		{
			//TODO Write save data
			Despawn();
		}
	}
}
