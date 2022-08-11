using Godot;

namespace Project.Gameplay.Objects
{
	public class FireSoul : RespawnableObject
	{
		[Export]
		public bool isCollected; //Determined by save data

		protected override bool IsRespawnable() => false;
		protected override void SetUp()
		{
			if (isCollected)
				Despawn();
		}

		private void OnEntered(Area _)
		{
			//TODO Write save data
			//ActiveCharacter.AddScore(1000);
			Despawn();
		}
	}
}
