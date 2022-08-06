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

		public override bool IsRespawnable() => true;

		public override void SetUp()
		{
			_animator = GetNodeOrNull<AnimationPlayer>(animator);
			base.SetUp();
		}

		public override void OnEntered(Area _)
		{
			GameplayInterface.instance.CollectRing(isRichRing ? 20 : 1);
			SoundManager.instance.PlayRingSoundEffect(); //SFX are played separately to avoid volume increase when collecting multiple rings at once

			if (_animator != null && _animator.HasAnimation("Collect"))
				_animator.Play("Collect");
			else
				Despawn();
		}

		public override void Spawn()
		{
			if (_animator != null && _animator.HasAnimation("RESET"))
			{
				_animator.Play("RESET");
				_animator.Advance(0);
			}

			if(!_animator.Autoplay.Empty())
				_animator.Play(_animator.Autoplay);
		}
	}
}
