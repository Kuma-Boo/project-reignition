using Godot;
using Project.Core;

namespace Project.Interface
{
    public class Countdown : Node
	{
		[Export]
		public NodePath countdownTickParent;
		private Node2D _countdownTickParent;
		[Export]
		public NodePath countdownAnimator;
		private AnimationPlayer _countdownAnimator;

		public override void _Ready()
		{
			_countdownTickParent = GetNode<Node2D>(countdownTickParent);
			StartCountdown();
		}

		[Signal]
		public delegate void CountdownStarted();
		[Signal]
		public delegate void CountdownCompleted();

		private void StartCountdown()
		{
			if (CheatManager.SkipCountdown)
				EmitSignal(nameof(CountdownCompleted));
			else
			{
				if (_countdownAnimator == null)
					_countdownAnimator = GetNode<AnimationPlayer>(countdownAnimator);

				_countdownAnimator.Play("Countdown");
				TweenCountdownTicks();
			}
		}

		//The ring animation is too tedious to animate by hand, so I'm using a tween instead.
		//WIP - NEEDS MORE WORK!
		private void TweenCountdownTicks()
		{
			//SceneTreeTween creates a temporary "memory leak" but the garbage collector cleans it up later
			SceneTreeTween countdownTweener = CreateTween().SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut).SetParallel(true);

			for (int i = 0; i < _countdownTickParent.GetChildCount(); i++)
			{
				Node2D tick = _countdownTickParent.GetChild<Node2D>(i);

				float delay = i * .04f + .6f;
				Vector2 targetPosition = tick.Position + (tick.Position.Normalized() * 48f);
				countdownTweener.TweenProperty(tick, "position", targetPosition, .2f).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "modulate", Colors.Transparent, .2f).SetDelay(delay);

				delay += 1;
				countdownTweener.TweenProperty(tick, "position", tick.Position, .2f).From(targetPosition).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "modulate", Colors.White, .2f).From(Colors.Transparent).SetDelay(delay);

				delay += 1;
				countdownTweener.TweenProperty(tick, "position", targetPosition, .2f).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "modulate", Colors.Transparent, .2f).SetDelay(delay);
			}
		}
	}
}
