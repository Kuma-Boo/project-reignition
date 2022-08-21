using Godot;

namespace Project.Core
{
	/// <summary>
	/// Handles transitions and scene changes.
	/// The transition will play halfway, at which point a signal will be emitted, allowing for loading.
	/// Call <see cref="FinishTransition"/> to complete the transition.
	/// </summary>
	public class TransitionManager : Node
	{
		public static TransitionManager instance;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public NodePath fade;
		private ColorRect _fade;
		[Export]
		public NodePath crossfade;
		private TextureRect _crossfade;

		//Converts realtime seconds to a ratio for the animation player's speed. ALL ANIMATIONS MUST BE 1 SECOND LONG.
		public float ConvertToAnimatorSpeed(float seconds) => 1f / seconds;

		public override void _Ready()
		{
			instance = this;
			_animator = GetNode<AnimationPlayer>(animator);
			_crossfade = GetNode<TextureRect>(crossfade);
			_fade = GetNode<ColorRect>(fade);
		}

		#region Transition Types
		//Simple cut transition. During loading, everything will freeze temporarily.
		private void StartCut() => EmitSignal(nameof(PerformLoading));

		//Render the viewport and crossfade the texture
		private void StartCrossfade()
		{
			Image img = GetViewport().GetTexture().GetData();
			ImageTexture tex = new ImageTexture();
			tex.CreateFromImage(img, 0);
			_crossfade.Texture = tex;
			_animator.Play("crossfade");
			_animator.Connect("animation_finished", instance, nameof(TransitionFinished), null, (uint)ConnectFlags.Oneshot);
		}

		private void StartFade()
		{
			IsTransitionActive = true;
			_fade.Color = CurrentTransitionData.color;
			_animator.PlaybackSpeed = ConvertToAnimatorSpeed(CurrentTransitionData.inSpeed);
			_animator.Play("fade");
			_animator.Connect("animation_finished", instance, nameof(TransitionLoading), null, (uint)ConnectFlags.Oneshot);
		}

		private void FinishFade()
		{
			if(CurrentTransitionData.outSpeed != 0)
				_animator.PlaybackSpeed = ConvertToAnimatorSpeed(CurrentTransitionData.outSpeed);
				
			_animator.PlayBackwards("fade");
			_animator.Connect("animation_finished", instance, nameof(TransitionFinished), null, (uint)ConnectFlags.Oneshot);
		}
		#endregion

		private TransitionData CurrentTransitionData { get; set; }
		public static bool IsTransitionActive { get; set; }
		[Signal]
		public delegate void PerformLoading(); //Called in the middle of the transition (i.e. when the screen is completely black)
		[Signal]
		public delegate void TransitionComplete(); //Called when the transition is finished
		private void TransitionLoading(string _) => EmitSignal(nameof(PerformLoading));
		private void TransitionFinished(string _)
		{
			IsTransitionActive = false;
			EmitSignal(nameof(TransitionComplete));
		}

		public static void StartTransition(TransitionData data)
		{
			instance._animator.Play("RESET"); //Reset animator, just in case
			instance._animator.Advance(0);

			instance.CurrentTransitionData = data;

			if (instance.CurrentTransitionData.inSpeed == 0)
			{
				instance.StartCut(); //Cut transition
				return;
			}

			switch (instance.CurrentTransitionData.type)
			{
				case TransitionData.Type.Crossfade:
					instance.StartCrossfade();
					break;
				case TransitionData.Type.Fade:
					instance.StartFade();
					break;
			}
		}
		
		public static void FinishTransition()
		{
			switch (instance.CurrentTransitionData.type)
			{
				case TransitionData.Type.Fade:
					instance.FinishFade();
					break;
			}
		}

		public static void QueueSceneChange(string scene, bool changeInstantly)
		{
			if (changeInstantly)
				instance.ChangeScene(scene);
			else
				instance.Connect(nameof(PerformLoading), instance, nameof(ChangeScene), new Godot.Collections.Array() { scene }, (uint)ConnectFlags.Oneshot);
		}

		private void ChangeScene(string queuedScene)
		{
			if (string.IsNullOrEmpty(queuedScene)) //Reload the current scene
				GetTree().ReloadCurrentScene();
			else
				GetTree().ChangeScene(queuedScene);

			FinishFade();
		}
	}

	public struct TransitionData
	{
		public float inSpeed; //Keep this at 0 to perform a simple cut transition
		public float outSpeed;
		public Color color;
		public Type type;

		public enum Type
		{
			Crossfade,
			Fade,
		}
	}
}
