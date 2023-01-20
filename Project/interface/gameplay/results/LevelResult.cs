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
		private InputManager.Controller Controller => InputManager.controller;
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
				//Skip animation
				if (Controller.jumpButton.wasPressed)
					animator.Seek(animator.CurrentAnimationLength, true);
			}
			else if (Controller.jumpButton.wasPressed || Controller.actionButton.wasPressed)
			{
				//Determine which scene to load
				TransitionManager.QueueSceneChange(Controller.jumpButton.wasPressed ? TransitionManager.MENU_SCENE_PATH : string.Empty, false);

				TransitionManager.StartTransition(new TransitionData()
				{
					color = Colors.Black,
					inSpeed = 1f,
				});
				SetInputProcessing(false);
			}
		}

		public void StartResults(bool wasSuccessful)
		{
			animator.Play(wasSuccessful ? "complete-start" : "fail-start");

			score.Text = Level.DisplayScore;
			time.Text = Level.DisplayTime;

			int enemyBonus = 0; //Level.CurrentEnemyCount * 50;
			enemy.Text = enemyBonus.ToString();

			int ringBonus = Level.CurrentRingCount * 10;
			ring.Text = ringBonus.ToString();

			float technicalBonus = 1.0f;
			technical.Text = "x" + technicalBonus.ToString(TECHNICAL_FORMATTING);

			Level.ChangeScore(Mathf.CeilToInt((ringBonus + enemyBonus) * technicalBonus), LevelSettings.ScoreFunction.Add);
			total.Text = Level.DisplayScore;
		}

		public void SetInputProcessing(bool value) => isProcessingInputs = value;
	}
}
