using Godot;

namespace Project.Gameplay
{
	public class FireSoul : RespawnableObject
	{
		[Export]
		public bool isCollected; //Determined by save data

		public override bool IsRespawnable() => false;
		public override void SetUp()
		{
			if (isCollected)
				Despawn();
		}

		public override void OnEntered(Area _)
		{
			//TODO Write save data
			//ActiveCharacter.AddScore(1000);
			Despawn();
		}
	}
}
