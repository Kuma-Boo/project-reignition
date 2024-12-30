using System.Globalization;
using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface;

public partial class LevelResult : Control
{
	[Signal]
	public delegate void ContinuePressedEventHandler();

	[Export]
	private Label score;
	[Export]
	private Label time;
	[Export]
	private Label ring;
	[Export]
	private Label technical;
	[Export]
	private Label total;
	[Export]
	private Control requirementRoot;
	[Export]
	private Label requirementTime;
	[Export]
	private Label requirementScore;
	[Export]
	private BGMPlayer bgm;
	[Export]
	private AnimationPlayer animator;
	[Export]
	private AudioStreamPlayer resultsVoicePlayer;
	[Export]
	private SFXLibraryResource resultsVoiceLibrary;

	private bool isProcessing;
	private bool isFadingBgm;
	private StageSettings Stage => StageSettings.Instance;

	public override void _Ready()
	{
		Stage?.Connect(StageSettings.SignalName.LevelCompleted, new Callable(this, MethodName.StartResults), (uint)ConnectFlags.Deferred);
		Stage?.Connect(StageSettings.SignalName.LevelDemoStarted, new Callable(this, MethodName.MuteGameplaySoundEffects));
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
		{
			if (isFadingBgm)
				isFadingBgm = SoundManager.FadeAudioPlayer(bgm, 2.0f);
			return;
		}

		if (animator.IsPlaying())
		{
			if (Input.IsActionJustPressed("button_jump") ||
				Input.IsActionJustPressed("button_action")) // Skip animation
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
		else if (Input.IsActionJustPressed("button_jump") ||
			Input.IsActionJustPressed("button_action"))
		{
			isFadingBgm = true; // Start fading bgm
			SetInputProcessing(false);

			// Determine which scene to load without connecting it
			if (Input.IsActionJustPressed("button_action")) // Retry stage
			{
				TransitionManager.instance.QueuedScene = string.Empty;
				EmitSignal(SignalName.ContinuePressed);
			}
			else// if (Level.storyEventIndex == 0) // Load main menu
			{
				TransitionManager.instance.QueuedScene = TransitionManager.MENU_SCENE_PATH;
				EmitSignal(SignalName.ContinuePressed);
			}

			// TODO Load story event
			//TransitionManager.QueueSceneChange($"{TransitionManager.EVENT_SCENE_PATH}{Level.storyEventIndex}.tscn");
			// Actual scene transition is handled by the experience results screen (which is connected via this signal)
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
			requirementScore.Text = ExtensionMethods.FormatMenuNumber(Stage.GetRequiredScore());
		}
		else
		{
			// Hide rank requirements
			requirementRoot.Visible = false;
		}

		if (rank <= 0) // Didn't obtain a medal
			animator.Play("medal-none");
		else if (rank == 1)
			animator.Play("medal-bronze");
		else if (rank == 2)
			animator.Play("medal-silver");
		else
			animator.Play("medal-gold");

		bool stageCleared = Stage.LevelState == StageSettings.LevelStateEnum.Success;
		SaveManager.GameData.LevelStatus clearStatus = stageCleared ? SaveManager.GameData.LevelStatus.Cleared : SaveManager.GameData.LevelStatus.Attempted;

		bgm.Play();
		animator.Advance(0.0);
		animator.Play(stageCleared ? "success-start" : "fail-start");

		// Update unlock notifications
		if (stageCleared)
		{
			if (Stage.Data.UnlockWorld != SaveManager.WorldEnum.LostPrologue && !SaveManager.ActiveGameData.IsWorldUnlocked(Stage.Data.UnlockWorld))
			{
				SaveManager.ActiveGameData.UnlockWorld(Stage.Data.UnlockWorld);
				NotificationMenu.AddNotification(NotificationMenu.NotificationType.World, $"unlock_{Stage.Data.UnlockWorld.ToString().ToSnakeCase()}");
			}

			foreach (var stage in Stage.Data.UnlockStage)
			{
				if (!SaveManager.ActiveGameData.IsStageUnlocked(stage.LevelID))
				{
					SaveManager.ActiveGameData.UnlockStage(stage.LevelID);
					NotificationMenu.AddNotification(NotificationMenu.NotificationType.Mission, "unlock_mission");
				}
			}

			// Only write these when the stage is a success
			SaveManager.ActiveGameData.SetHighScore(Stage.Data.LevelID, Stage.TotalScore);
			SaveManager.ActiveGameData.SetBestTime(Stage.Data.LevelID, Stage.CurrentTime);
		}

		// Write common save file
		SaveManager.ActiveGameData.SetRank(Stage.Data.LevelID, rank);
		SaveManager.ActiveGameData.SetClearStatus(Stage.Data.LevelID, clearStatus);
	}

	public void SetInputProcessing(bool value) => isProcessing = value;
	/// <summary> Mutes the gameplay sfx audio channel. </summary>
	private void MuteGameplaySoundEffects() => SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, 0);

	public void PlayRankQuote()
	{
		int voiceIndex = 0;
		if (Stage.LevelState != StageSettings.LevelStateEnum.Failed)
			voiceIndex = SaveManager.ActiveGameData.GetRank(Stage.Data.LevelID) + 1;

		resultsVoicePlayer.Stream = resultsVoiceLibrary.GetStream(voiceIndex, (int)SaveManager.Config.voiceLanguage);
		resultsVoicePlayer.Play();
	}
}
