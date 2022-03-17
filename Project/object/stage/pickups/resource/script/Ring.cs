using Godot;

namespace Project.Gameplay
{
	public class Ring : StageObject
	{
		[Export]
		public bool isRichRing;

		public override bool IsRespawnable() => true;
		public override void SetUp()
		{
			base.SetUp();
		}

		public override void OnEnter()
		{
			GameplayInterface.instance.CollectRing(isRichRing ? 20 : 1);
			SFXLibrary.instance.PlayRingSoundEffect();
			Despawn();
		}
	}
}
