using Godot;
using Project.Gameplay;

namespace Project.Interface
{
    public class Countdown : Node
	{
		[Export]
		public bool skipCountdown;
		[Export]
		public NodePath countdownTickParent;
		private Node2D _countdownTickParent;
		[Export]
		public NodePath countdownAnimator;
		private AnimationPlayer _countdownAnimator;
		private Tween _countdownTweener;

		public override void _Ready()
		{
			_countdownTweener = new Tween();
			AddChild(_countdownTweener);
			_countdownTickParent = GetNode<Node2D>(countdownTickParent);

			StartCountdown();
		}

		public bool IsCountDownComplete { get; private set; }
		public void OnCountdownCompleted()
		{
			IsCountDownComplete = true;
			CharacterController.instance.OnCountdownCompleted(); //Enables the player
		}

		private void StartCountdown()
		{
			IsCountDownComplete = false;

			if (skipCountdown)
				OnCountdownCompleted();
			else
			{
				if (_countdownAnimator == null)
					_countdownAnimator = GetNode<AnimationPlayer>(countdownAnimator);

				_countdownAnimator.Play("Countdown");
				CharacterController.instance.OnCountdownStarted(); //Enables the player
			}

			TweenCountdownTicks();
		}

		//The ring animation is too tedious to animate by hand, so I'm using a tween instead.
		private void TweenCountdownTicks()
		{
			_countdownTweener.ResetAll();

			for (int i = 0; i < _countdownTickParent.GetChildCount(); i++)
			{
				Node2D tick = _countdownTickParent.GetChild<Node2D>(i);

				float delay = i * .04f + .6f;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position, tick.Position + (tick.Position.Normalized() * 48f), .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.White, Colors.Transparent, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);

				delay += 1;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position + (tick.Position.Normalized() * 48f), tick.Position, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.Transparent, Colors.White, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);

				delay += 1;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position, tick.Position + (tick.Position.Normalized() * 48f), .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.White, Colors.Transparent, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
			}

			_countdownTweener.Start();
		}
	}
}
