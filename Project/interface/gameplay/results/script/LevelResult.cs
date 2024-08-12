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
	private Sprite2D requirements_border;
	[Export]
	private Sprite2D requirements_medal;
	[Export]
	private Label time_requirement;
	[Export]
	private Label score_requirement;
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
	private StageSettings Stage => StageSettings.instance;

	public override void _Ready() => Stage?.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, nameof(StartResults)), (uint)ConnectFlags.Deferred);

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
		total.Text = ExtensionMethods.FormatMenuNumber(Stage.TotalScore).ToString();

		

		// Calculate rank AFTER tallying final score
		int rank = Stage.CalculateRank();

		//Show the Score Requirements when Rank Preview is equipped
		if (rank >= 0 && rank < 3 && CharacterController.instance.Skills.IsSkillEquipped(SkillKey.RankPreview))
		{
			GD.Print("Showing rank preview");
			requirements_border.Visible = true;
			requirements_medal.Visible = true;
			time_requirement.Visible = true;
			score_requirement.Visible = true;
		}
		else if (rank == 3)
		{
			GD.Print("Hiding rank preview");
			requirements_border.Visible = false;
			requirements_medal.Visible = false;
			time_requirement.Visible = false;
			score_requirement.Visible = false;
		}


		if (rank <= 0) // Didn't obtain a medal
		{
			animator.Play("medal-none");
			time_requirement.Text = Stage.GetRequiredTime(0);
			score_requirement.Text = ExtensionMethods.FormatMenuNumber2(Stage.GetRequiredScore(0)).ToString();
		}
		else if (rank == 1)
		{
			animator.Play("medal-bronze");
			time_requirement.Text = Stage.GetRequiredTime(1);
			score_requirement.Text = ExtensionMethods.FormatMenuNumber2(Stage.GetRequiredScore(1)).ToString();
		}
		else if (rank == 2)
		{
			animator.Play("medal-silver");
			time_requirement.Text = Stage.GetRequiredTime(2);
			score_requirement.Text = ExtensionMethods.FormatMenuNumber2(Stage.GetRequiredScore(2)).ToString();
		}
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
	public void MuteGameplaySoundEffects() => SoundManager.SetAudioBusVolume(SoundManager.AudioBuses.GameSfx, 0);

	public void PlayRankQuote()
	{
		int voiceIndex = 0;
		if (Stage.LevelState != StageSettings.LevelStateEnum.Failed)
			voiceIndex = SaveManager.ActiveGameData.GetRank(Stage.Data.LevelID) + 1;

		resultsVoicePlayer.Stream = resultsVoiceLibrary.GetStream(voiceIndex, (int)SaveManager.Config.voiceLanguage);
		resultsVoicePlayer.Play();
	}
}
