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
	private AnimationPlayer animator;

	private bool isProcessing;
	private StageSettings Stage => StageSettings.instance;

	public override void _Ready() => Stage?.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, nameof(StartResults)));

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing) return;

		if (animator.IsPlaying())
		{
			if (Input.IsActionJustPressed("button_jump")) // Skip animation
				animator.Advance(animator.CurrentAnimationLength);
		}
		else if (Input.IsActionJustPressed("button_jump") ||
			Input.IsActionJustPressed("button_action"))
		{
			SetInputProcessing(false);

			// Determine which scene to load without connecting it
			if (Input.IsActionJustPressed("button_action")) // Retry stage
				TransitionManager.instance.QueuedScene = string.Empty;
			else// if (Level.storyEventIndex == 0) // Load main menu
				TransitionManager.instance.QueuedScene = TransitionManager.MENU_SCENE_PATH;
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
		technical.Text = "x" + Stage.TechnicalBonus.ToString("0.0", CultureInfo.InvariantCulture);
		total.Text = Stage.TotalScore.ToString();

		// Calculate rank AFTER tallying final score
		int rank = Stage.CalculateRank();
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

		animator.Advance(0.0);
		animator.Play(stageCleared ? "success-start" : "fail-start");

		// Write to file
		SaveManager.ActiveGameData.SetClearStatus(Stage.Data.LevelID, clearStatus);
		SaveManager.ActiveGameData.SetHighScore(Stage.Data.LevelID, Stage.TotalScore);
		SaveManager.ActiveGameData.SetBestTime(Stage.Data.LevelID, Stage.CurrentTime);
		SaveManager.ActiveGameData.SetRank(Stage.Data.LevelID, rank);
	}

	public void SetInputProcessing(bool value) => isProcessing = value;
}
