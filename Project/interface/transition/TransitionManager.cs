using Godot;

namespace Project.Core;

/// <summary>
/// Handles transitions and scene changes.
/// The transition will play halfway, at which point a signal will be emitted, allowing for loading.
/// Call <see cref="FinishTransition"/> to complete the transition.
/// </summary>
public partial class TransitionManager : Node
{
	public static TransitionManager instance;
	/// <summary> Path to the main menu scene. </summary>
	public const string MENU_SCENE_PATH = "res://interface/menu/Menu.tscn";
	/// <summary> Path to story events. </summary>
	public const string EVENT_SCENE_PATH = "res://video/event/scene/Event";

	[Export]
	private ColorRect fade;
	[Export]
	private AnimationPlayer animator;
	[Export]
	private AnimationPlayer loadingAnimator;

	//Converts realtime seconds to a ratio for the animation player's speed. ALL ANIMATIONS MUST BE 1 SECOND LONG.
	public float ConvertToAnimatorSpeed(float seconds) => 1f / seconds;

	public override void _EnterTree() => instance = this;

	#region Transition Types
	//Simple cut transition. During loading, everything will freeze temporarily.
	private void StartCut() => EmitSignal(SignalName.TransitionProcess);
	private void StartFade()
	{
		if (IsTransitionActive)
		{
			GD.Print("Transition is already active!");
			return;
		}

		if (CurrentTransitionData.loadAsynchronously)
			loadingAnimator.Play("show");

		IsTransitionActive = true;
		fade.Color = CurrentTransitionData.color;
		animator.Play("fade");

		if (CurrentTransitionData.inSpeed == 0)
		{
			animator.Seek(animator.CurrentAnimationLength, true);
			CallDeferred(MethodName.EmitSignal, SignalName.TransitionProcess);
		}
		else
		{
			animator.SpeedScale = ConvertToAnimatorSpeed(CurrentTransitionData.inSpeed);
			animator.Connect(AnimationPlayer.SignalName.AnimationFinished, new(instance, MethodName.TransitionLoading), (uint)ConnectFlags.OneShot);
		}
	}

	private void FinishFade()
	{
		if (CurrentTransitionData.loadAsynchronously)
			loadingAnimator.Play("hide");

		animator.PlayBackwards("fade");
		if (Mathf.IsZeroApprox(CurrentTransitionData.outSpeed)) // Cut
			animator.Seek(animator.CurrentAnimationLength, true);
		else
			animator.SpeedScale = ConvertToAnimatorSpeed(CurrentTransitionData.outSpeed);

		animator.Connect(AnimationPlayer.SignalName.AnimationFinished, new(instance, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
	}
	#endregion

	private TransitionData CurrentTransitionData { get; set; }
	public static bool IsTransitionActive { get; set; }
	[Signal]
	public delegate void SceneChangedEventHandler(); //Called when the scene changes
	[Signal]
	public delegate void TransitionProcessEventHandler(); //Called in the middle of the transition (i.e. when the screen is completely black)
	[Signal]
	public delegate void TransitionFinishEventHandler(); //Called when the transition is finished
	private void TransitionLoading(string _) => EmitSignal(SignalName.TransitionProcess);
	private void TransitionFinished(string _)
	{
		IsTransitionActive = false;
		EmitSignal(SignalName.TransitionFinish);
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

	/// <summary> The scene to load. Note that the scene only gets applied if queued using QueueSceneChange(). </summary>
	public string QueuedScene { get; set; }
	/// <summary> Queues a scene to load and connects the TransitionProcess signal. Be sure to call StartTransition to actually transition to the scene. </summary>
	public static void QueueSceneChange(string scene)
	{
		instance.QueuedScene = scene;

		var call = new Callable(instance, MethodName.ApplySceneChange);
		if (!instance.IsConnected(SignalName.TransitionProcess, call))
			instance.Connect(SignalName.TransitionProcess, call, (uint)ConnectFlags.OneShot);
	}

	private async void ApplySceneChange()
	{
		SoundManager.instance.CancelDialog(); //Cancel any active dialog
		if (string.IsNullOrEmpty(QueuedScene)) //Reload the current scene
			GetTree().ReloadCurrentScene();
		else
		{
			if (CurrentTransitionData.loadAsynchronously)
			{
				ResourceLoader.LoadThreadedRequest(QueuedScene, "");
				while (ResourceLoader.LoadThreadedGetStatus(QueuedScene) == ResourceLoader.ThreadLoadStatus.InProgress) // Still loading
					await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout); // Wait a bit

				var scene = ResourceLoader.LoadThreadedGet(QueuedScene) as PackedScene;
				GetTree().ChangeSceneToPacked(scene);
			}
			else
			{
				GetTree().ChangeSceneToFile(QueuedScene);
			}
		}

		QueuedScene = string.Empty; //Clear queue
		EmitSignal(SignalName.SceneChanged);

		if (!CurrentTransitionData.disableAutoTransition)
			FinishFade();
	}
}

public struct TransitionData
{
	//Keep both speeds at 0 to perform simple cut transitions
	public float inSpeed;
	public float outSpeed;
	public Color color;
	public bool loadAsynchronously;
	public bool disableAutoTransition;
}