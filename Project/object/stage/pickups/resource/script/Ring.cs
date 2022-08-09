using Godot;

namespace Project.Gameplay
{
	public class Ring : RespawnableObject
	{
		[Export]
		public bool isRichRing;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		[Signal]
		public delegate void Collected();

		protected override bool IsRespawnable() => true;

		protected override void SetUp()
		{
			_animator = GetNodeOrNull<AnimationPlayer>(animator);
			base.SetUp();
		}

		public override void Respawn()
		{
			base.Respawn();

			_animator.Play("RESET");
			_animator.Advance(0);

			if (!_animator.Autoplay.Empty())
				_animator.Play(_animator.Autoplay);
		}

		private void OnEntered(Area _)
		{
			HeadsUpDisplay.instance.CollectRing(isRichRing ? 20 : 1);
			SoundManager.instance.PlayRingSoundEffect(); //SFX are played externally to avoid multiple ring sounds at once

			if (_animator != null && _animator.HasAnimation("Collect"))
				_animator.Play("Collect");
			else
				Despawn();

			EmitSignal(nameof(Collected));
		}
	}
}
