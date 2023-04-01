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
		private Label enemy;
		[Export]
		private Label ring;
		[Export]
		private Label technical;
		[Export]
		private Label total;
		[Export]
		private AnimationPlayer animator;

		private bool isProcessingInputs;
		private LevelSettings Level => LevelSettings.instance;
		private const string TECHNICAL_FORMATTING = "0.0";

		public override void _Ready()
		{
			if (Level != null)
				Level.Connect(nameof(LevelSettings.LevelCompleted), new Callable(this, nameof(StartResults)));
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isProcessingInputs) return;

			if (animator.IsPlaying())
			{
				if (Input.IsActionJustPressed("button_jump")) // Skip animation
					animator.Seek(animator.CurrentAnimationLength, true);
			}
			else if (Input.IsActionJustPressed("button_jump") ||
				Input.IsActionJustPressed("button_action"))
			{
				// Determine which scene to load
				if (Input.IsActionJustPressed("button_action")) // Retry stage
					TransitionManager.QueueSceneChange(string.Empty, false);
				else if (Level.storyEventIndex == 0) // Load main menu
					TransitionManager.QueueSceneChange(TransitionManager.MENU_SCENE_PATH, false);
				else //Load story event
					TransitionManager.QueueSceneChange($"{TransitionManager.EVENT_SCENE_PATH}{Level.storyEventIndex}.tscn", false);

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
			int rank = Level.CalculateRank();
			if (rank <= 0) //Didn't obtain a medal
				animator.Play("medal-none");
			else if (rank == 1)
				animator.Play("medal-bronze");
			else if (rank == 2)
				animator.Play("medal-silver");
			else
				animator.Play("medal-gold");

			animator.Seek(0.0, true);
			animator.Play(Level.LevelState == LevelSettings.LevelStateEnum.Success ? "success-start" : "fail-start");

			score.Text = Level.DisplayScore;
			time.Text = Level.DisplayTime;

			int enemyBonus = 0; //Level.CurrentEnemyCount * 50;
			enemy.Text = enemyBonus.ToString();

			int ringBonus = Level.CurrentRingCount * 10;
			ring.Text = ringBonus.ToString();

			float technicalBonus = 1.0f;
			technical.Text = "x" + technicalBonus.ToString(TECHNICAL_FORMATTING);

			Level.UpdateScore(Mathf.CeilToInt((ringBonus + enemyBonus) * technicalBonus), LevelSettings.MathModeEnum.Add);
			total.Text = Level.DisplayScore;
		}

		public void SetInputProcessing(bool value) => isProcessingInputs = value;
	}
}
