using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class FireSoul : Pickup
	{
		[Export(PropertyHint.Range, "1, 3")]
		public int fireSoulIndex = 1; // Which fire soul is this?
		private bool isCollected; // Determined by save data
		[Export]
		private AnimationPlayer animator;

		protected override void SetUp()
		{
			base.SetUp();

			// Check save data
			isCollected = SaveManager.ActiveGameData.IsFireSoulCollected(Stage.Data.LevelID, fireSoulIndex);
			if (isCollected)
				Despawn();
		}

		protected override void Collect()
		{
			// Write save data
			SaveManager.ActiveGameData.SetFireSoulCollected(Stage.Data.LevelID, fireSoulIndex, true);
			animator.Play("collect");
		}
	}
}
