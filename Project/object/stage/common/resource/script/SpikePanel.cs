using Godot;

namespace Project.Gameplay
{
	public class SpikePanel : Hitbox
	{
		[Export]
		public float inactiveLength;
		[Export]
		public float warningLength;
		[Export]
		public float activeLength;
		[Export(PropertyHint.Range, "0, 1")]
		public float startingOffset;

		private AttackState attackState;
		private enum AttackState
		{
			Inactive,
			Warning,
			Active,
		}

		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		[Export]
		public NodePath timer;
		private Timer _timer;

		public override void _Ready()
		{
			_animator = GetNode<AnimationPlayer>(animator);
			_timer = GetNode<Timer>(timer);

			if (Mathf.IsZeroApprox(inactiveLength))
				TimerCompleted();
			else
			{
				_timer.WaitTime = inactiveLength - (startingOffset * inactiveLength);
				_timer.Start();
			}
		}

		public void TimerCompleted()
		{
			float targetWaitTime = 0;

			switch (attackState)
			{
				case AttackState.Active:
					attackState = AttackState.Inactive;
					targetWaitTime = inactiveLength;
					_animator.Play("inactive");
					break;
				case AttackState.Inactive:
					attackState = AttackState.Warning;
					targetWaitTime = warningLength;
					_animator.Play("warning");
					break;
				case AttackState.Warning:
					attackState = AttackState.Active;
					targetWaitTime = activeLength;
					_animator.Play("active");
					break;
			}

			_animator.Advance(0);

			//Update Animation
			targetWaitTime += _animator.CurrentAnimationLength;
			_timer.WaitTime = targetWaitTime;
			_timer.Start();
		}
	}
}
