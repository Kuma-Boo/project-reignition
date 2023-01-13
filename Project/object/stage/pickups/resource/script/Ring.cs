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
				Level.ChangeScore(100, LevelSettings.ScoreFunction.Add);
				Level.UpdateRingCount(20);
				SoundManager.instance.PlayRichRingSFX();
			}
			else
			{
				Level.ChangeScore(10, LevelSettings.ScoreFunction.Add);
				Level.UpdateRingCount(1);
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
