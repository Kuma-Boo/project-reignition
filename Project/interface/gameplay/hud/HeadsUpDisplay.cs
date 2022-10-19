using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Displays game data to the player. Only handles the graphics.
	/// </summary>
	public partial class HeadsUpDisplay : Control
	{
		public static HeadsUpDisplay instance;
		private StageSettings Stage => StageSettings.instance;

		public override void _Ready()
		{
			instance = this;

			_score = GetNode<Label>(score);
			_time = GetNode<Label>(time);

			InitializeBonuses();
			InitializeRings();
			InitializeObjectives();
			InitializeSoulGauge();

			if (Stage != null) //Decouple from stage settings
			{
				Stage.Connect(nameof(StageSettings.RingChanged), new Callable(this, nameof(UpdateRingCount)));
				Stage.Connect(nameof(StageSettings.TimeChanged), new Callable(this, nameof(UpdateTime)));
				Stage.Connect(nameof(StageSettings.ScoreChanged), new Callable(this, nameof(UpdateScore)));
				Stage.Connect(nameof(StageSettings.BonusAdded), new Callable(this, nameof(AddBonus)));
				Stage.Connect(nameof(StageSettings.StageCompleted), new Callable(this, nameof(StageComplete))); //Hide interface
			}
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateSoulGauge(); //Animate the soul gauge
			UpdateBonus();
		}

		#region Rings
		[Export]
		public NodePath ringLabel;
		private Label _ringLabel;
		[Export]
		public NodePath maxRingLabel;
		private Label _maxRingLabel;
		[Export]
		public NodePath ringDividerSprite;
		private Sprite2D _ringDividerSprite;
		[Export]
		public NodePath ringAnimator;
		private AnimationPlayer _ringAnimator;
		private const string RING_LABEL_FORMAT = "000";

		private void InitializeRings()
		{
			//Initialize ring counter
			_ringLabel = GetNode<Label>(ringLabel);
			_maxRingLabel = GetNode<Label>(maxRingLabel);
			_ringDividerSprite = GetNode<Sprite2D>(ringDividerSprite);
			_ringAnimator = GetNode<AnimationPlayer>(ringAnimator);

			if (Stage != null)
			{
				_maxRingLabel.Visible = _ringDividerSprite.Visible = Stage.MissionType == StageSettings.MissionTypes.Ring; //Show/Hide max ring count
				if (_maxRingLabel.Visible)
					_maxRingLabel.Text = Stage.ObjectiveCount.ToString(RING_LABEL_FORMAT);

				_ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
				if (Stage.CurrentRingCount == 0) //Starting in a ringless state
					_ringAnimator.Play("Ringless");
			}
		}

		private void UpdateRingCount(int amount)
		{
			if (amount > 0) //Play animation
			{
				_ringAnimator.Play("CollectRing");
				_ringAnimator.Seek(0.0f, true);
			}
			else
			{
				_ringAnimator.Play("LoseRing");
				_ringAnimator.AnimationSetNext("LoseRing", Stage.CurrentRingCount == 0 ? "Ringless" : "RESET");
			}

			_ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
		}
		#endregion

		#region Time and Score
		[Export]
		public NodePath time;
		private Label _time;
		private void UpdateTime() => _time.Text = Stage.DisplayTime;

		[Export]
		public NodePath score;
		private Label _score;
		private void UpdateScore() => _score.Text = Stage.DisplayScore;
		#endregion

		#region Bonuses
		[Export]
		public NodePath bonusAnimator;
		private AnimationPlayer _bonusAnimator;
		[Export]
		public Array<NodePath> bonusLabels;
		private int bonusCount = -1;
		private readonly Array<Label> _bonusLabels = new Array<Label>();
		private readonly Array<StageSettings.BonusType> bonusQueue = new Array<StageSettings.BonusType>();
		private const int MAX_BONUS_COUNT = 5; //How many bonuses can be onscreen at once
		private void InitializeBonuses()
		{
			_bonusAnimator = GetNode<AnimationPlayer>(bonusAnimator);
			for (int i = 0; i < bonusLabels.Count; i++)
			{
				_bonusLabels.Add(GetNode<Label>(bonusLabels[i]));
				_bonusLabels[i].Modulate = Colors.Transparent;
			}
		}

		private void AddBonus(StageSettings.BonusType type)
		{
			if (_bonusAnimator.IsPlaying())
			{
				bonusQueue.Add(type);
				return;
			}

			//Increment bonus count
			bonusCount++;
			if (bonusCount > MAX_BONUS_COUNT)
				bonusCount = MAX_BONUS_COUNT;

			//Update text
			for (int i = 1; i < _bonusLabels.Count; i++)
			{
				_bonusLabels[i].Text = _bonusLabels[i - 1].Text;
				_bonusLabels[i].Modulate = i > bonusCount ? Colors.Transparent : Colors.White;
			}
			_bonusLabels[0].Text = Tr(type.ToString());

			//Play animations
			_bonusAnimator.Play("RESET");
			_bonusAnimator.Seek(0, true);
			_bonusAnimator.Play("AddBonus", -1, bonusQueue.Count != 0 ? 1.5f : 1f);
		}

		private float bonusTimer;
		private void UpdateBonus() //Fade out the last bonus text via code
		{
			if (GetTree().Paused) return; //Paused

			if (bonusQueue.Count != 0 && !_bonusAnimator.IsPlaying())
			{
				AddBonus(bonusQueue[0]);
				bonusQueue.RemoveAt(0);
			}

			if (bonusCount >= 0)
			{
				bonusTimer += PhysicsManager.physicsDelta / (float)Engine.TimeScale;

				if (bonusTimer > 1f)
				{
					float fadeAmount = Mathf.Clamp((bonusTimer % 1f) / .25f, 0f, 1f);
					_bonusLabels[bonusCount].Modulate = Colors.White.Lerp(Colors.Transparent, fadeAmount);

					if (fadeAmount >= 1f)
					{
						bonusCount--;
						bonusTimer = 0f;
					}
				}
			}
		}
		#endregion

		#region Objectives
		[Export]
		public NodePath objectiveSprite;
		private TextureRect _objectiveSprite;
		[Export]
		public NodePath objectiveValue;
		private Label _objectiveValue;
		[Export]
		public NodePath objectiveMaxValue;
		private Label _objectiveMaxValue;
		private void InitializeObjectives()
		{
			if (Stage == null || Stage.MissionType != StageSettings.MissionTypes.Objective) return; //Don't do anything when not set to objective based mission

			_objectiveSprite = GetNode<TextureRect>(objectiveSprite);
			_objectiveSprite.Visible = true;

			_objectiveValue = GetNode<Label>(objectiveValue);
			_objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");

			_objectiveMaxValue = GetNode<Label>(objectiveMaxValue);
			_objectiveMaxValue.Text = Stage.ObjectiveCount.ToString("00");

			Stage.Connect(nameof(StageSettings.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		}

		private void UpdateObjective() => _objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
		#endregion

		#region Soul Gauge
		[Export]
		public NodePath soulGauge;
		private Control _soulGauge;
		private Control _soulGaugeRoot;
		[Export]
		public NodePath soulGaugeFill;
		private Control _soulGaugeFill;
		private Control _soulGaugeBackground;
		[Export]
		public NodePath soulGaugeAnimator;
		private AnimationPlayer _soulGaugeAnimator;
		private void InitializeSoulGauge()
		{
			//Initialize soul gauge
			_soulGauge = GetNode<Control>(soulGauge);
			_soulGaugeRoot = _soulGauge.GetParent<Control>();
			_soulGaugeFill = GetNode<Control>(soulGaugeFill);
			_soulGaugeBackground = _soulGaugeFill.GetParent<Control>();
			_soulGaugeAnimator = GetNode<AnimationPlayer>(soulGaugeAnimator);

			//Resize the soul gauge
			int lerpFrom = Mathf.RoundToInt(_soulGaugeRoot.Size.y - _soulGauge.GetMinimumSize().y); //Smallest gauge size
			_soulGauge.OffsetTop = Mathf.Lerp(lerpFrom, 0, SaveManager.ActiveGameData.SoulGaugeLevel); //Set the soul gauge to the correct size
			ModifySoulGauge(0f, false);
		}

		public void ModifySoulGauge(float ratio, bool isCharged)
		{
			targetSoulGaugeRatio = ratio;
			UpdateSoulGaugeColor(isCharged);
		}

		private float targetSoulGaugeRatio;
		private Vector2 soulGaugeVelocity;
		private const int SOUL_GAUGE_FILL_OFFSET = 15;
		private void UpdateSoulGauge()
		{
			Vector2 end = Vector2.Down * Mathf.Lerp(_soulGaugeBackground.Size.y + SOUL_GAUGE_FILL_OFFSET, 0, targetSoulGaugeRatio);
			_soulGaugeFill.Position = ExtensionMethods.SmoothDamp(_soulGaugeFill.Position, end, ref soulGaugeVelocity, 0.1f);
		}

		private bool isSoulGaugeCharged;
		public void UpdateSoulGaugeColor(bool isCharged)
		{
			if (!isSoulGaugeCharged && isCharged)
			{
				//Play animation
				isSoulGaugeCharged = true;
				_soulGaugeAnimator.Play("Charged");
			}
			else if (isSoulGaugeCharged && !isCharged)
			{
				//Lost charge
				isSoulGaugeCharged = false;
				_soulGaugeAnimator.Play("RESET"); //Revert to blue
			}
		}
		#endregion

		public void StageComplete(bool _) => SetVisibility(false); //Ignore parameter
		public void SetVisibility(bool value) => Visible = value;
	}
}
