using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface
{
	public partial class StageResult : Node
	{
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public NodePath score;
		private Label _score;
		[Export]
		public NodePath time;
		private Label _time;
		[Export]
		public NodePath enemy;
		private Label _enemy;
		[Export]
		public NodePath ring;
		private Label _ring;
		[Export]
		public NodePath technical;
		private Label _technical;
		[Export]
		public NodePath total;
		private Label _total;

		private bool isProcessingInputs;
		private InputManager.Controller Controller => InputManager.controller;
		private StageSettings Stage => StageSettings.instance;
		private const string TECHNICAL_FORMATTING = "0.0";
		private const string MENU_SCENE_PATH = "res://interface/menu/Menu.tscn";

		public override void _Ready()
		{
			_animator = GetNode<AnimationPlayer>(animator);

			_score = GetNode<Label>(score);
			_time = GetNode<Label>(time);
			_enemy = GetNode<Label>(enemy);
			_ring = GetNode<Label>(ring);
			_technical = GetNode<Label>(technical);
			_total = GetNode<Label>(total);

			if (Stage != null)
				Stage.Connect(nameof(StageSettings.StageCompleted), new Callable(this, nameof(StartResults)));
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isProcessingInputs) return;

			if (_animator.IsPlaying())
			{
				//Skip animation
				if (Controller.jumpButton.wasPressed)
					_animator.Seek(_animator.CurrentAnimationLength, true);
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
			_animator.Play(wasSuccessful ? "CompleteStart" : "FailStart");

			_score.Text = Stage.DisplayScore;
			_time.Text = Stage.DisplayTime;

			int enemyBonus = 0; //Stage.CurrentEnemyCount * 50;
			_enemy.Text = enemyBonus.ToString();

			int ringBonus = Stage.CurrentRingCount * 10;
			_ring.Text = ringBonus.ToString();

			float technicalBonus = 1.0f;
			_technical.Text = "x" + technicalBonus.ToString(TECHNICAL_FORMATTING);

			Stage.ChangeScore(Mathf.CeilToInt((ringBonus + enemyBonus) * technicalBonus), StageSettings.ScoreFunction.Add);
			_total.Text = Stage.DisplayScore;
		}

		public void SetInputProcessing(bool value) => isProcessingInputs = value;
	}
}
