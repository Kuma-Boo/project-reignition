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
			if (isRichRing)
			{
				Stage.ChangeScore(100, StageSettings.ScoreFunction.Add);
				Stage.UpdateRingCount(20);
				SoundManager.instance.PlayRichRingSFX();
			}
			else
			{
				Stage.ChangeScore(10, StageSettings.ScoreFunction.Add);
				Stage.UpdateRingCount(1);
				SoundManager.instance.PlayRingSFX();
			}

			if (animator != null && animator.HasAnimation("collect"))
				animator.Play("collect");
			else
				Despawn();

			base.Collect();
		}
	}
}
