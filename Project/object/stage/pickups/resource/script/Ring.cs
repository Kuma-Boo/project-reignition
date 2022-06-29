using Godot;

namespace Project.Gameplay
{
	public class Ring : RespawnableObject
	{
		[Export]
		public bool isRichRing;

		public override bool IsRespawnable() => true;

		public override void OnEntered(Area _)
		{
			GameplayInterface.instance.CollectRing(isRichRing ? 20 : 1);
			SFXLibrary.instance.PlayRingSoundEffect();
			Despawn();
		}
	}
}
