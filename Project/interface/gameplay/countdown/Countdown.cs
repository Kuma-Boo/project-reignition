using Godot;
using Project.Core;

namespace Project.Interface
{
	public partial class Countdown : Node
	{
		public static bool IsCountdownActive { get; private set; }

		[Signal]
		public delegate void CountdownStartedEventHandler();
		[Signal]
		public delegate void CountdownFinishedEventHandler();

		[Export]
		private Node2D tickParent;
		[Export]
		private AnimationPlayer animator;

		public override void _Ready()
		{
			BGMPlayer.StartStageMusic(); // Start BGM

			if (DebugManager.Instance.SkipCountdown)
			{
				FinishCountdown();
				return;
			}

			animator.Play("countdown");
			TweenCountdownTicks();

			IsCountdownActive = true;
			EmitSignal(SignalName.CountdownStarted);
		}

		public void FinishCountdown()
		{
			IsCountdownActive = false;
			EmitSignal(SignalName.CountdownFinished);
		}

		//The ring animation is too tedious to animate by hand, so I'm using a tween instead.
		private void TweenCountdownTicks()
		{
			//Tween seems to create a temporary "memory leak" but the garbage collector cleans it up later
			Tween countdownTweener = CreateTween().SetTrans(Tween.TransitionType.Sine).SetParallel(true);

			for (int i = 0; i < tickParent.GetChildCount(); i++)
			{
				Node2D tick = tickParent.GetChild<Node2D>(i);

				float delay = i * (1f / tickParent.GetChildCount()) + .65f;
				Vector2 targetPosition = tick.Position + (tick.Position.Normalized() * 25f);
				countdownTweener.TweenProperty(tick, "position", targetPosition, .1f).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "position", tick.Position, .2f).From(targetPosition).SetDelay(delay + .1f);
				countdownTweener.TweenProperty(tick, "modulate", Colors.Transparent, .25f).SetDelay(delay);

				delay += 1;
				countdownTweener.TweenProperty(tick, "position", targetPosition, .1f).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "position", tick.Position, .2f).From(targetPosition).SetDelay(delay + .1f);
				countdownTweener.TweenProperty(tick, "modulate", Colors.White, .25f).From(Colors.Transparent).SetDelay(delay);

				delay += 1;
				countdownTweener.TweenProperty(tick, "position", targetPosition, .1f).SetDelay(delay);
				countdownTweener.TweenProperty(tick, "position", tick.Position, .2f).From(targetPosition).SetDelay(delay + .1f);
				countdownTweener.TweenProperty(tick, "modulate", Colors.Transparent, .25f).SetDelay(delay);
			}
		}
	}
}
