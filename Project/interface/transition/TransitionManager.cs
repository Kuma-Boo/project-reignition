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
	public const string EVENT_SCENE_PATH = "res:// video/event/scene/Event";

	public bool IsReloadingScene { get; private set; }

	[Export] private Label loadLabel;
	[Export] private ColorRect fade;
	[Export] private AnimationPlayer animator;
	[Export] private AnimationPlayer loadingAnimator;
	[Export] private Control missionDescriptionRoot;
	[Export] private Label missionDescriptionLabel;

	public override void _EnterTree() => instance = this;

	#region Transition Types
	// Simple cut transition. During loading, everything will freeze temporarily.
	private void StartCut() => EmitSignal(SignalName.TransitionProcess);
	private void StartFade()
	{
		if (IsTransitionActive)
		{
			GD.PushWarning("Transition is already active!");
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
			animator.SpeedScale = 1.0f / CurrentTransitionData.inSpeed;
			animator.Connect(AnimationPlayer.SignalName.AnimationFinished, new(instance, MethodName.TransitionLoading), (uint)ConnectFlags.OneShot);
		}

		EmitSignal(SignalName.TransitionStarted);
	}

	private void FinishFade()
	{
		if (CurrentTransitionData.loadAsynchronously)
			loadingAnimator.Play("hide");

		animator.PlayBackwards("fade");
		if (Mathf.IsZeroApprox(CurrentTransitionData.outSpeed)) // Cut
			animator.Seek(animator.CurrentAnimationLength, true);
		else
			animator.SpeedScale = 1.0f / CurrentTransitionData.outSpeed;

		animator.Connect(AnimationPlayer.SignalName.AnimationFinished, new(instance, MethodName.TransitionFinished), (uint)ConnectFlags.OneShot);
	}
	#endregion

	private TransitionData CurrentTransitionData { get; set; }
	public static bool IsTransitionActive { get; set; }
	/// <summary> Called when the scene changes. </summary>
	[Signal] public delegate void SceneChangedEventHandler();
	/// <summary> Called whenever a transition is started. </summary>
	[Signal] public delegate void TransitionStartedEventHandler();
	/// <summary> Called in the middle of the transition (when the screen is completely black). </summary>
	[Signal] public delegate void TransitionProcessEventHandler();
	/// <summary> Called when the transition is finished. </summary>
	[Signal] public delegate void TransitionFinishEventHandler();
	private void TransitionLoading(string _) => EmitSignal(SignalName.TransitionProcess);
	private void TransitionFinished(string _)
	{
		IsTransitionActive = false;
		EmitSignal(SignalName.TransitionFinish);
	}

	public static void StartTransition(TransitionData data)
	{
		instance.animator.Play("RESET"); // Reset animator, just in case
		instance.animator.Advance(0);
		instance.UpdateLoadingText(null);

		instance.CurrentTransitionData = data;
		instance.missionDescriptionRoot.Visible = data.showMissionDescription;

		if (data.inSpeed == 0 && data.outSpeed == 0)
		{
			instance.StartCut(); // Cut transition
			return;
		}

		instance.StartFade();
	}

	public static void FinishTransition()
	{
		instance.UpdateLoadingText(null);
		instance.FinishFade();
	}

	/// <summary> The scene to load. Note that the scene only gets applied if queued using QueueSceneChange(). </summary>
	public string QueuedScene { get; set; }
	/// <summary> Queues a scene to load and connects the TransitionProcess signal. Be sure to call StartTransition to actually transition to the scene. </summary>
	public static void QueueSceneChange(string scene)
	{
		GD.Print("Scene Change Queued");
		instance.QueuedScene = scene;

		var call = new Callable(instance, MethodName.ApplySceneChange);
		if (!instance.IsConnected(SignalName.TransitionProcess, call))
			instance.Connect(SignalName.TransitionProcess, call, (uint)ConnectFlags.OneShot);
	}

	private async void ApplySceneChange()
	{
		SoundManager.instance.CancelDialog(); // Cancel any active dialog
		IsReloadingScene = string.IsNullOrEmpty(QueuedScene);
		if (IsReloadingScene) // Reload the current scene
		{
			GetTree().ReloadCurrentScene();
		}
		else
		{
			/* TODO Godot v4.5 Fix asynchronous loading
			if (CurrentTransitionData.loadAsynchronously)
			{
				GD.Print(ResourceLoader.LoadThreadedRequest(QueuedScene));
				while (ResourceLoader.LoadThreadedGetStatus(QueuedScene) == ResourceLoader.ThreadLoadStatus.InProgress) // Still loading
				{
					GD.PrintT("Loading Scene...", );
					await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout); // Wait a bit
				}

				var scene = ResourceLoader.LoadThreadedGet(QueuedScene) as PackedScene;
				GetTree().ChangeSceneToPacked(scene);
				GD.Print("Scene Changed.");
			}
			*/

			GetTree().ChangeSceneToFile(QueuedScene);
		}

		// Reset time scale and unpause whenever we change scenes
		Engine.TimeScale = 1f;
		GetTree().Paused = false;

		QueuedScene = string.Empty; // Clear queue
		EmitSignal(SignalName.SceneChanged);

		if (!CurrentTransitionData.disableAutoTransition)
			FinishFade();
	}

	public void UpdateLoadingText(StringName localizationKey, int currentProgress = 0, int maxProgress = 0)
	{
		if (localizationKey == null)
		{
			loadLabel.Text = string.Empty;
			return;
		}

		loadLabel.Text = Tr(localizationKey);
		if (maxProgress != 0)
			loadLabel.Text += $" {currentProgress}/{maxProgress}";
	}

	public void SetMissionDescriptionText(StringName typeKey, StringName descriptionKey)
	{
		missionDescriptionLabel.Text = $"{Tr(typeKey)}: {Tr(descriptionKey)}";
	}
}

public struct TransitionData
{
	// Keep both speeds at 0 to perform simple cut transitions
	public float inSpeed;
	public float outSpeed;
	public Color color;
	public bool loadAsynchronously;
	public bool disableAutoTransition;
	public bool showMissionDescription;
}