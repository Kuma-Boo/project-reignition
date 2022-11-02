using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
	public partial class StageResult : Node
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
		private StageSettings Stage => StageSettings.instance;
		private const string TECHNICAL_FORMATTING = "0.0";
		private const string MENU_SCENE_PATH = "res://interface/menu/Menu.tscn";

		public override void _Ready()
		{
			if (Stage != null)
				Stage.Connect(nameof(StageSettings.StageCompleted), new Callable(this, nameof(StartResults)));
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
				TransitionManager.QueueSceneChange(Controller.jumpButton.wasPressed ? MENU_SCENE_PATH : string.Empty, false);

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
			animator.Play(wasSuccessful ? "CompleteStart" : "FailStart");

			score.Text = Stage.DisplayScore;
			time.Text = Stage.DisplayTime;

			int enemyBonus = 0; //Stage.CurrentEnemyCount * 50;
			enemy.Text = enemyBonus.ToString();

			int ringBonus = Stage.CurrentRingCount * 10;
			ring.Text = ringBonus.ToString();

			float technicalBonus = 1.0f;
			technical.Text = "x" + technicalBonus.ToString(TECHNICAL_FORMATTING);

			Stage.ChangeScore(Mathf.CeilToInt((ringBonus + enemyBonus) * technicalBonus), StageSettings.ScoreFunction.Add);
			total.Text = Stage.DisplayScore;
		}

		public void SetInputProcessing(bool value) => isProcessingInputs = value;
	}
}
