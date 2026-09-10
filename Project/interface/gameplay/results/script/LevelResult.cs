using System.Globalization;
using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class LevelResult : Control
{
	[Signal] public delegate void ContinuePressedEventHandler();

	[Export] private Control nextButton;
	[Export] private Control retryButton;
	[Export] private Label score;
	[Export] private Label time;
	[Export] private Label timeTotal; //For time attack
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

	/// <summary> Tracks whether the stage was already cleared when starting; Used to skip repeat cutscenes. </summary>
	private bool wasStageClearedWhenLoaded;
	private bool isProcessing;
	private bool isFadingBgm;
	private StageSettings Stage => StageSettings.Instance;

	private readonly StringName AchievementGoldKey = "the ultimate";
	private readonly int AchievementGoldRequirement = 111;
	public const string AchievementGoldTimeAttackKey = "record buster";
	public const int AchievementGoldTimeAttackRequirement = 114; // All gold medals and categories

	public override void _Ready()
	{
		wasStageClearedWhenLoaded = SaveManager.ActiveGameData.LevelData.GetClearStatus(Stage.Data.LevelID) == SaveManager.LevelSaveData.LevelStatus.Cleared;

		if (IsInstanceValid(Stage))
		{
			Stage.LevelCompleted += () => CallDeferred(MethodName.StartResults);
			Stage.LevelDemoStarted += MuteGameplaySoundEffects;
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

		bool isButtonPressed = Runtime.Instance.IsActionJustPressed("sys_select", "ui_select") ||
			(Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel", "escape") && retryButton.IsVisibleInTree());

		if (animator.IsPlaying())
		{
			// Don't allow instantly skipping animation (since players may be spamming the jump button)
			if (animator.CurrentAnimationPosition < 1f)
				return;

			if (isButtonPressed) // Skip animation
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

			return;
		}

		if (isButtonPressed)
			ProcessMenuButtons();
	}

	private void ProcessMenuButtons()
	{
		// Determine which scene to load without connecting it

		if (Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel", "escape")) // Retry stage
		{
			TransitionManager.Instance.QueuedScene = string.Empty;
			ActivateTransition();
		}
		else
		{
			if (!nextButton.IsVisibleInTree())
				return;
			// Adventure mode; Process events
			TransitionManager.Instance.QueuedScene = TransitionManager.MenuScenePath;

			if (Stage.LevelState == StageSettings.LevelStateEnum.Success &&
				!string.IsNullOrEmpty(Stage.Data.PostStoryEvent) &&
				(!SaveManager.Config.skipRepeatCutscenes || !wasStageClearedWhenLoaded))
			{
				TransitionManager.Instance.QueuedScene = $"{TransitionManager.EventScenePath}{Stage.Data.PostStoryEvent}.tscn";
			}

			if (TimeAttackManager.Instance.IsRunActive && !TimeAttackManager.Instance.IsLastLevel() && TimeAttackManager.Instance.CurrentRunType != TimeAttackManager.RunType.SingleRun)
			{
				TimeAttackManager.Instance.LoadLevel(TimeAttackManager.Instance.GetNextLevel());
				TimeAttackManager.Instance.IncreaseLevel();
			}
			else if (TimeAttackManager.Instance.IsRunActive && TimeAttackManager.Instance.IsLastLevel() && TimeAttackManager.Instance.CurrentRunType != TimeAttackManager.RunType.SingleRun)
				TimeAttackManager.Instance.LoadResults();
			else if (TimeAttackManager.Instance.IsRunActive && TimeAttackManager.Instance.CurrentRunType == TimeAttackManager.RunType.SingleRun)
				TimeAttackManager.Instance.LoadTimeAttack(true);
			else
				ActivateTransition();
		}

		isFadingBgm = true; // Start fading bgm
		SetInputProcessing(false);
	}

	public void StartResults()
	{
		SoundManager.instance.StageMusicPlayer.SetBgmResource(null);
		bool isRetryButtonDisabled = StageSettings.Instance.Data == SaveManager.ActiveGameData.CurrentStoryLevel &&
			Stage.LevelState == StageSettings.LevelStateEnum.Success;

		if (TimeAttackManager.Instance.IsRunActive && Stage.LevelState == StageSettings.LevelStateEnum.Success)
			isRetryButtonDisabled = true;

		if (TimeAttackManager.Instance.IsRunActive && Stage.LevelState == StageSettings.LevelStateEnum.Failed)
		{
			nextButton.Visible = false;
			isRetryButtonDisabled = false;
		}

		if (TimeAttackManager.Instance.IsRunActive && TimeAttackManager.Instance.CurrentRunType == TimeAttackManager.RunType.SingleRun)
		{
			nextButton.Visible = true;
			isRetryButtonDisabled = false;
		}

		retryButton.Visible = !isRetryButtonDisabled;
		score.Text = ExtensionMethods.FormatMenuNumber(Stage.CurrentScore);
		time.Text = Stage.DisplayTime;

		if (TimeAttackManager.Instance.IsRunActive && Stage.LevelState != StageSettings.LevelStateEnum.Failed)
		{
			if (TimeAttackManager.Instance.CurrentRunType != TimeAttackManager.RunType.SingleRun)
			{
				timeTotal.Text = ExtensionMethods.FormatTime(TimeAttackManager.Instance.GetTotalRunTime() + Stage.CurrentTime);
				TimeAttackManager.Instance.AddTime(Stage.CurrentTime);

				if (TimeAttackManager.Instance.IsLastLevel())
					SaveManager.TimeData.AddCurrentRun();
				else
					SaveManager.TimeData.CurrentPlacement += 1;
				SaveManager.SaveTimeAttackData();
			}
			else
			{
				if (!SaveManager.TimeData.HasRank(Stage.Data) && Stage.CalculateRank() == 3)
					SaveManager.TimeData.GoldMedalCount++;

				if (SaveManager.TimeData.SingleRun.ContainsKey(Stage.Data.LevelID))
					SaveManager.TimeData.SingleRun[Stage.Data.LevelID].Add(Stage.CurrentTime);
				else
				{
					SaveManager.TimeData.SingleRun.Add(Stage.Data.LevelID, []);
					SaveManager.TimeData.SingleRun[Stage.Data.LevelID].Add(Stage.CurrentTime);
				}
				SaveManager.SaveTimeAttackData();
			}
		}

		ring.Text = Stage.RingBonus.ToString();
		technical.Text = "×" + Stage.TechnicalBonus.ToString("0.0", CultureInfo.InvariantCulture);
		total.Text = ExtensionMethods.FormatMenuNumber(Stage.TotalScore);

		// Calculate rank AFTER tallying final score
		int rank = Stage.CalculateRank();
		bool isRankPreviewShown = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RankPreview) &&
			(!TimeAttackManager.Instance.IsRunActive || TimeAttackManager.Instance.CurrentRunType == TimeAttackManager.RunType.SingleRun);

		// Show the Score Requirements when Rank Preview is equipped
		if (isRankPreviewShown && rank >= 0 && rank < 3)
		{
			// Show rank requirements
			requirementRoot.Visible = true;
			requirementTime.Text = Stage.GetRequiredTime(rank);
			requirementScore.Visible = !Stage.Data.SkipScore && !TimeAttackManager.Instance.IsRunActive;
			if (requirementScore.Visible)
			{
				int rankUpScore = Stage.Data.Score;
				if (rank < 2)
					rankUpScore = Stage.Data.SilverScore;

				requirementScore.Text = ExtensionMethods.FormatMenuNumber(rankUpScore);
			}
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
		animator.Advance(0.0);

		if (TimeAttackManager.Instance.IsRunActive)
		{
			animator.Play("rank-preview-ta");
			animator.Advance(0.0);
		}

		bool isStageCleared = Stage.LevelState == StageSettings.LevelStateEnum.Success;

		if (isStageCleared)
			bgmIndex = rank == 3 ? 2 : 1;
		else
			bgmIndex = 0;
		bgm[bgmIndex].Play();


		if (TimeAttackManager.Instance.IsRunActive && TimeAttackManager.Instance.CurrentRunType == TimeAttackManager.RunType.SingleRun)
			animator.Play(isStageCleared ? "success-start-timeattack-single" : "fail-start-ta");
		else if (TimeAttackManager.Instance.IsRunActive)
			animator.Play(isStageCleared ? "success-start-timeattack" : "fail-start-ta");
		else
			animator.Play(isStageCleared ? "success-start" : "fail-start");
	}

	private void ActivateTransition()
	{
		OnRankQuoteFinished();

		// Actual scene transition is handled by the experience results screen (which is connected via this signal)
		EmitSignal(SignalName.ContinuePressed);
	}


	public void SetInputProcessing(bool value) => isProcessing = value;
	/// <summary> Mutes the gameplay sfx audio channel. </summary>
	private void MuteGameplaySoundEffects() => SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, 0);

	public void PlayRankQuote()
	{
		int voiceIndex = Stage.CalculateRank() + 1;
		string key = "results fail";
		switch (voiceIndex)
		{
			case 1:
				key = "results none";
				break;
			case 2:
				key = "results bronze";
				break;
			case 3:
				key = "results silver";
				break;
			case 4:
				key = "results gold";
				break;
		}

		if (SaveManager.ActiveGameData.LevelData.GoldMedalCount >= AchievementGoldRequirement)
			AchievementManager.Instance.UnlockAchievement(AchievementGoldKey);

		if (SaveManager.TimeData.GoldMedalCount >= AchievementGoldTimeAttackRequirement)
			AchievementManager.Instance.UnlockAchievement(AchievementGoldTimeAttackKey);

		resultsVoicePlayer.Stream = StageSettings.Player.Effect.voiceLibrary.GetStream(key, SaveManager.GetCurrentVoiceLocaleIndex());
		resultsVoicePlayer.Play();

		SoundManager.instance.IsRankQuotePlaying = true;
		resultsVoicePlayer.Finished += OnRankQuoteFinished;
	}

	private void OnRankQuoteFinished() => SoundManager.instance.IsRankQuotePlaying = false;
}
