using System.Globalization;
using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class LevelResult : Control
{
	[Signal] public delegate void ContinuePressedEventHandler();

	[Export] private Label score;
	[Export] private Label time;
	[Export] private Label ring;
	[Export] private Label technical;
	[Export] private Label total;
	[Export] private Control requirementRoot;
	[Export] private Label requirementTime;
	[Export] private Label requirementScore;
	[Export] private BGMPlayer[] bgm;
	private int bgmIndex;
	[Export] private AnimationPlayer animator;
	[Export] private AudioStreamPlayer resultsVoicePlayer;
	[Export] private SFXLibraryResource resultsVoiceLibrary;

	private bool isProcessing;
	private bool isFadingBgm;
	private StageSettings Stage => StageSettings.Instance;

	public override void _Ready()
	{
		if (IsInstanceValid(Stage))
		{
			Stage.Connect(StageSettings.SignalName.LevelCompleted, new Callable(this, MethodName.StartResults), (uint)ConnectFlags.Deferred);
			Stage.Connect(StageSettings.SignalName.LevelDemoStarted, new Callable(this, MethodName.MuteGameplaySoundEffects));
		}

		if (IsInstanceValid(DebugManager.Instance))
		{
			OnHUDVisibilityToggled();
			DebugManager.Instance.Connect(DebugManager.SignalName.HUDToggled, new Callable(this, MethodName.OnHUDVisibilityToggled));
		}
	}

	private void OnHUDVisibilityToggled() => Visible = !DebugManager.Instance.DisableHUD;

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
		{
			if (isFadingBgm)
				isFadingBgm = SoundManager.FadeAudioPlayer(bgm[bgmIndex], 2.0f);
			return;
		}

		if (animator.IsPlaying())
		{
			// Don't allow instantly skipping animation (since players may be spamming the jump button)
			if (animator.CurrentAnimationPosition < 1f)
				return;

			if (Runtime.Instance.IsActionJustPressed("sys_select", "ui_select") ||
				Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel", "escape")) // Skip animation
			{
				StringName nextAnimation = animator.AnimationGetNext(animator.CurrentAnimation);
				animator.Advance(animator.CurrentAnimationLength);

				if (!string.IsNullOrEmpty(nextAnimation))
				{
					animator.Play(nextAnimation);
					animator.Advance(animator.CurrentAnimationLength);
					Stage.StartCompletionDemo();
				}
			}
		}
		else if (Runtime.Instance.IsActionJustPressed("sys_select", "ui_select") ||
			Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel", "escape"))
		{
			isFadingBgm = true; // Start fading bgm
			SetInputProcessing(false);

			// Determine which scene to load without connecting it
			if (Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel", "escape")) // Retry stage
				TransitionManager.instance.QueuedScene = string.Empty;
			else// if (Level.storyEventIndex == 0) // Load main menu
				TransitionManager.instance.QueuedScene = TransitionManager.MenuScenePath;

			// TODO Load story event
			//TransitionManager.QueueSceneChange($"{TransitionManager.EVENT_SCENE_PATH}{Level.storyEventIndex}.tscn");

			// Actual scene transition is handled by the experience results screen (which is connected via this signal)
			EmitSignal(SignalName.ContinuePressed);
		}
	}

	public void StartResults()
	{
		score.Text = Stage.DisplayScore;
		time.Text = Stage.DisplayTime;

		ring.Text = Stage.RingBonus.ToString();
		technical.Text = "×" + Stage.TechnicalBonus.ToString("0.0", CultureInfo.InvariantCulture);
		total.Text = ExtensionMethods.FormatMenuNumber(Stage.TotalScore);

		// Calculate rank AFTER tallying final score
		int rank = Stage.CalculateRank();

		// Show the Score Requirements when Rank Preview is equipped
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RankPreview) && rank >= 0 && rank < 3)
		{
			// Show rank requirements
			requirementRoot.Visible = true;
			requirementTime.Text = Stage.GetRequiredTime(rank);
			requirementScore.Visible = !Stage.Data.SkipScore;
			if (requirementScore.Visible)
				requirementScore.Text = ExtensionMethods.FormatMenuNumber(Stage.GetRequiredScore());
		}
		else
		{
			// Hide rank requirements
			requirementRoot.Visible = false;
		}

		switch (rank)
		{
			case 1:
				animator.Play("medal-bronze");
				break;
			case 2:
				animator.Play("medal-silver");
				break;
			case 3:
				animator.Play("medal-gold");
				break;
			default:
				// No medal
				animator.Play("medal-none");
				break;
		}

		bool isStageCleared = Stage.LevelState == StageSettings.LevelStateEnum.Success;

		if (isStageCleared)
			bgmIndex = rank == 3 ? 2 : 1;
		else
			bgmIndex = 0;
		bgm[bgmIndex].Play();

		animator.Advance(0.0);
		animator.Play(isStageCleared ? "success-start" : "fail-start");
	}

	public void SetInputProcessing(bool value) => isProcessing = value;
	/// <summary> Mutes the gameplay sfx audio channel. </summary>
	private void MuteGameplaySoundEffects() => SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, 0);

	public void PlayRankQuote()
	{
		int voiceIndex = StageSettings.Instance.CalculateRank() + 1;
		resultsVoicePlayer.Stream = resultsVoiceLibrary.GetStream(voiceIndex, (int)SaveManager.Config.voiceLanguage);
		resultsVoicePlayer.Play();
	}
}
