using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class Countdown : Control
{
	public static bool IsCountdownActive { get; private set; }
	public static Countdown Instance { get; private set; }

	[Signal]
	public delegate void CountdownStartedEventHandler();
	[Signal]
	public delegate void CountdownFinishedEventHandler();

	[Export]
	private Node2D tickParent;
	[Export]
	private AnimationPlayer animator;

	public override void _EnterTree() => Instance = this;
	public override void _Ready() => StageSettings.instance.Connect(StageSettings.SignalName.LevelStarted, new(this, MethodName.StartCountdown));

	public void StartCountdown()
	{
		BGMPlayer.StartStageMusic(); // Start BGM

		if (DebugManager.Instance.SkipCountdown || StageSettings.instance.Data.DisableCountdown)
		{
			FinishCountdown();
			return;
		}

		if (DebugManager.Instance.HideCountdown)
		{
			animator.Play("mute");
			animator.Advance(0.0);
			Visible = false;
		}

		animator.Play("countdown");
		TweenCountdownTicks();

		IsCountdownActive = true;
		PauseMenu.AllowPausing = false;
		EmitSignal(SignalName.CountdownStarted);
	}

	public void FinishCountdown()
	{
		IsCountdownActive = false;
		PauseMenu.AllowPausing = true;
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