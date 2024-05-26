using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
	public partial class LevelResult : Node
	{
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

		private bool isProcessingInputs;
		private StageSettings Stage => StageSettings.instance;
		private const string TECHNICAL_FORMATTING = "0.0";

		public override void _Ready()
		{
			if (Stage != null)
				Stage.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, nameof(StartResults)));
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isProcessingInputs) return;

			if (animator.IsPlaying())
			{
				if (Input.IsActionJustPressed("button_jump")) // Skip animation
					animator.Advance(animator.CurrentAnimationLength);
			}
			else if (Input.IsActionJustPressed("button_jump") ||
				Input.IsActionJustPressed("button_action"))
			{
				SaveManager.SaveGameData();

				// Determine which scene to load
				if (Input.IsActionJustPressed("button_action")) // Retry stage
					TransitionManager.QueueSceneChange(string.Empty);
				else// if (Level.storyEventIndex == 0) // Load main menu
					TransitionManager.QueueSceneChange(TransitionManager.MENU_SCENE_PATH);
				// TODO Load story event
				//TransitionManager.QueueSceneChange($"{TransitionManager.EVENT_SCENE_PATH}{Level.storyEventIndex}.tscn");

				TransitionManager.StartTransition(new TransitionData()
				{
					color = Colors.Black,
					inSpeed = 1f,
				});
				SetInputProcessing(false);
			}
		}

		public void StartResults()
		{
			score.Text = Stage.DisplayScore;
			time.Text = Stage.DisplayTime;

			int ringBonus = Stage.CurrentRingCount * 10;
			ring.Text = ringBonus.ToString();

			float technicalBonus = Stage.CalculateTechnicalBonus();
			technical.Text = "x" + technicalBonus.ToString(TECHNICAL_FORMATTING);

			Stage.UpdateScore(Mathf.CeilToInt(ringBonus * technicalBonus), StageSettings.MathModeEnum.Add);
			total.Text = Stage.DisplayScore;


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

			animator.Advance(0.0);
			animator.Play(Stage.LevelState == StageSettings.LevelStateEnum.Success ? "success-start" : "fail-start");

			// Write to file
			SaveManager.ActiveGameData.SetHighScore(Stage.Data.LevelID, Stage.CurrentScore);
			SaveManager.ActiveGameData.SetBestTime(Stage.Data.LevelID, Stage.CurrentTime);
			SaveManager.ActiveGameData.SetRank(Stage.Data.LevelID, rank);
		}

		public void SetInputProcessing(bool value) => isProcessingInputs = value;
	}
}
