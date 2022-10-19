using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Ring : Pickup
	{
		[Export]
		public bool isRichRing;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		protected override void SetUp()
		{
			_animator = GetNodeOrNull<AnimationPlayer>(animator);
			base.SetUp();
		}

		public override void Respawn()
		{
			base.Respawn();

			_animator.Play("RESET");
			_animator.Queue("loop");
		}

		protected override void Collect()
		{
			StageSettings.instance.ChangeScore(isRichRing ? 100 : 10, StageSettings.ScoreFunction.Add);
			StageSettings.instance.UpdateRingCount(isRichRing ? 20 : 1);
			SoundManager.instance.PlayRingSoundEffect(); //SFX are played externally to avoid multiple ring sounds at once

			if (_animator != null && _animator.HasAnimation("collect"))
				_animator.Play("collect");
			else
				Despawn();

			base.Collect();
		}
	}
}
