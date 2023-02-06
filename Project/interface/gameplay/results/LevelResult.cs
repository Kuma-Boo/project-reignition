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
