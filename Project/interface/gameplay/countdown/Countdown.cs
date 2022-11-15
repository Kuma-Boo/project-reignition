using Godot;
using Project.Core;

namespace Project.Interface
{
	public partial class Countdown : Node
	{
		[Export]
		private Node2D tickParent;
		[Export]
		private AnimationPlayer animator;

		public override void _Ready()
		{
			StartCountdown();
		}

		[Signal]
		public delegate void CountdownStartEventHandler();
		[Signal]
		public delegate void CountdownCompleteEventHandler();

		private void StartCountdown()
		{
			if (CheatManager.SkipCountdown)
				EmitSignal(SignalName.CountdownComplete);
			else
			{
				animator.Play("countdown");
				TweenCountdownTicks();
			}
		}

		//The ring animation is too tedious to animate by hand, so I'm using a tween instead.
		//WIP - NEEDS MORE WORK!
		private void TweenCountdownTicks()
		{
			//Tween creates a temporary "memory leak" but the garbage collector cleans it up later
			Tween countdownTweener = CreateTween().SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut).SetParallel(true);

			for (int i = 0; i < tickParent.GetChildCount(); i++)
			{
				Node2D tick = tickParent.GetChild<Node2D>(i);

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
