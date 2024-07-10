using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Ring : Pickup
	{
		[Export]
		private bool isRichRing;
		[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")]
		private NodePath animator;
		private AnimationPlayer Animator { get; set; }

		protected override void SetUp()
		{
			Animator = GetNodeOrNull<AnimationPlayer>(animator);
			base.SetUp();
		}

		public override void Respawn()
		{
			base.Respawn();

			Animator.Play("RESET");
			Animator.Queue("loop");
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

			BonusManager.instance.AddRingChain();
			Animator.Play("collect");
			base.Collect();
		}
	}
}
