using Godot;

namespace Project.Gameplay.Objects
{
	public partial class FireSoul : Pickup
	{
		[Export(PropertyHint.Range, "1, 3")]
		public int fireSoulIndex = 1; //Which fire soul is this?
		private bool isCollected; //Determined by save data
		[Export]
		private AnimationPlayer animator;

		protected override void SetUp() //TODO Check save data
		{
			base.SetUp();

			if (isCollected)
				Despawn();
		}

		protected override void Collect()
		{
			//TODO Write save data
			animator.Play("collect");
		}
	}
}
