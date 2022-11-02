using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Ring : Pickup
	{
		[Export]
		private bool isRichRing;
		[Export]
		private AnimationPlayer animator;

		public override void Respawn()
		{
			base.Respawn();

			animator.Play("RESET");
			animator.Queue("loop");
		}

		protected override void Collect()
		{
			StageSettings.instance.ChangeScore(isRichRing ? 100 : 10, StageSettings.ScoreFunction.Add);
			StageSettings.instance.UpdateRingCount(isRichRing ? 20 : 1);
			SoundManager.instance.PlayRingSoundEffect(); //SFX are played externally to avoid multiple ring sounds at once

			if (animator != null && animator.HasAnimation("collect"))
				animator.Play("collect");
			else
				Despawn();

			base.Collect();
		}
	}
}
