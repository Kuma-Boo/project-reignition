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
				StageSettings.instance.ChangeScore(100, StageSettings.ScoreFunction.Add);
				StageSettings.instance.UpdateRingCount(20);
				SoundManager.instance.PlayRichRingSFX();
			}
			else
			{
				StageSettings.instance.ChangeScore(10, StageSettings.ScoreFunction.Add);
				StageSettings.instance.UpdateRingCount(1);
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
