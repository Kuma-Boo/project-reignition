using Godot;
using Project.Core;

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
				Stage.UpdateScore(100, StageSettings.MathModeEnum.Add);
				Stage.UpdateRingCount(20, StageSettings.MathModeEnum.Add);
				SoundManager.instance.PlayRichRingSFX();
			}
			else
			{
				Stage.UpdateScore(10, StageSettings.MathModeEnum.Add);
				Stage.UpdateRingCount(1, StageSettings.MathModeEnum.Add);
				SoundManager.instance.PlayRingSFX();
			}

			if (animator != null && animator.HasAnimation("collect"))
				animator.Play("collect");
			else
				Despawn();

			base.Collect();
		}

		public void OnBodyEntered(Node3D other)
		{
			GD.Print(other.IsInGroup("player"));
		}
	}
}
