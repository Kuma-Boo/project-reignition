using Godot;

namespace Project.Core
{
	/// <summary>
	/// Handles transitions and scene changes.
	/// The transition will play halfway, at which point a signal will be emitted, allowing for loading.
	/// Call <see cref="FinishTransition"/> to complete the transition.
	/// </summary>
	public partial class TransitionManager : Node
	{
		public static TransitionManager instance;

		[Export]
		private ColorRect fade;
		[Export]
		private AnimationPlayer animator;

		//Converts realtime seconds to a ratio for the animation player's speed. ALL ANIMATIONS MUST BE 1 SECOND LONG.
		public float ConvertToAnimatorSpeed(float seconds) => 1f / seconds;

		public override void _Ready()
		{
			instance = this;
		}

		#region Transition Types
		//Simple cut transition. During loading, everything will freeze temporarily.
		private void StartCut() => EmitSignal(SignalName.Load);
		private void StartFade()
		{
			IsTransitionActive = true;
			fade.Color = CurrentTransitionData.color;
			animator.Play("fade");

			if (CurrentTransitionData.inSpeed == 0)
			{
				animator.Seek(animator.CurrentAnimationLength, true);
				EmitSignal(SignalName.Load);
			}
			else
			{
				animator.PlaybackSpeed = ConvertToAnimatorSpeed(CurrentTransitionData.inSpeed);
				animator.Connect("animation_finished", new Callable(instance, MethodName.TransitionLoading), (uint)ConnectFlags.OneShot);
			}
		}

		private void FinishFade()
		{
			if (CurrentTransitionData.outSpeed != 0)
				animator.PlaybackSpeed = ConvertToAnimatorSpeed(CurrentTransitionData.outSpeed);

			animator.PlayBackwards("fade");
			animator.Connect("animation_finished", new Callable(instance, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
		}
		#endregion

		private TransitionData CurrentTransitionData { get; set; }
		public static bool IsTransitionActive { get; set; }
		[Signal]
		public delegate void LoadEventHandler(); //Called in the middle of the transition (i.e. when the screen is completely black)
		[Signal]
		public delegate void FinishEventHandler(); //Called when the transition is finished
		private void TransitionLoading(string _) => EmitSignal(SignalName.Load);
		private void TransitionFinished(string _)
		{
			IsTransitionActive = false;
			EmitSignal(SignalName.Finish);
		}

		public static void StartTransition(TransitionData data)
		{
			instance.animator.Play("RESET"); //Reset animator, just in case
			instance.animator.Advance(0);

			instance.CurrentTransitionData = data;

			if (data.inSpeed == 0 && data.outSpeed == 0)
			{
				instance.StartCut(); //Cut transition
				return;
			}

			instance.StartFade();
		}

		public static void FinishTransition() => instance.FinishFade();

		public static void QueueSceneChange(string scene, bool changeInstantly)
		{
			instance.queuedScene = scene;
			if (changeInstantly)
				instance.ApplySceneChange();
			else
				instance.Connect(SignalName.Load, new Callable(instance, MethodName.ApplySceneChange), (uint)ConnectFlags.OneShot);
		}

		private string queuedScene;
		private void ApplySceneChange()
		{
			Gameplay.SoundManager.instance.CancelDialog(); //Cancel any active dialog
			if (string.IsNullOrEmpty(queuedScene)) //Reload the current scene
				GetTree().ReloadCurrentScene();
			else
				GetTree().ChangeSceneToFile(queuedScene);

			queuedScene = string.Empty; //Clear queue
			FinishFade();
		}
	}

	public struct TransitionData
	{
		//Keep both speeds at 0 to perform simple cut transitions
		public float inSpeed;
		public float outSpeed;
		public Color color;
	}
}
