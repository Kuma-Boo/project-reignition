using Godot;

namespace Project.Interface
{
	public class TransitionManager : Node
	{
		public static TransitionManager instance;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public NodePath fadeTransition;
		private ColorRect _fadeTransition;
		public static bool IsTransitionActive { get; set; }

		[Signal]
		public delegate void TransitionLoad(); //Called in the middle of the transition (i.e. when the screen is completely black)
		[Signal]
		public delegate void TransitionFinish(); //Called when the transition is finished
		private void TransitionLoading(string _) => EmitSignal(nameof(TransitionLoad));
		private void TransitionFinished(string _)
		{
			IsTransitionActive = false;
			EmitSignal(nameof(TransitionFinish));
		}

		public override void _Ready()
		{
			instance = this;
			_animator = GetNode<AnimationPlayer>(animator);
			_fadeTransition = GetNode<ColorRect>(fadeTransition);
		}

		public static void Fade(Color color, float fadeSpeed = 1f)
		{
			IsTransitionActive = true;
			instance._fadeTransition.Color = color;
			instance._animator.Play("fade");
			instance._animator.PlaybackSpeed = fadeSpeed;
			instance._animator.Connect("animation_finished", instance, nameof(TransitionLoading), null, (uint)ConnectFlags.Oneshot);
		}

		public static void CompleteFade(float fadeSpeed = -1)
		{
			if(fadeSpeed != -1)
				instance._animator.PlaybackSpeed = fadeSpeed;
				
			instance._animator.PlayBackwards("fade");
			instance._animator.Connect("animation_finished", instance, nameof(TransitionFinished), null, (uint)ConnectFlags.Oneshot);
		}
	}
}
