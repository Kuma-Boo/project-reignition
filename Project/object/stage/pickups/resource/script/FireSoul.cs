using Godot;

namespace Project.Gameplay.Objects
{
	public partial class FireSoul : Pickup
	{
		[Export]
		public bool isCollected; //Determined by save data

		protected override void SetUp() //TODO Check save data
		{
			if (isCollected)
				Despawn();
		}

		private void OnEntered(Area3D _)
		{
			//TODO Write save data
			//ActiveCharacter.AddScore(1000);
			Despawn();
		}
	}
}
