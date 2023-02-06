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
				Level.UpdateScore(100, LevelSettings.MathModeEnum.Add);
				Level.UpdateRingCount(20, LevelSettings.MathModeEnum.Add);
				SoundManager.instance.PlayRichRingSFX();
			}
			else
			{
				Level.UpdateScore(10, LevelSettings.MathModeEnum.Add);
				Level.UpdateRingCount(1, LevelSettings.MathModeEnum.Add);
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
