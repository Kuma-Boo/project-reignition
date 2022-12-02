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
		private Label ringLabel;
		[Export]
		private Label maxRingLabel;
		[Export]
		private Sprite2D ringDividerSprite;
		[Export]
		private AnimationPlayer ringAnimator;
		private const string RING_LABEL_FORMAT = "000";

		private void InitializeRings()
		{
			//Initialize ring counter
			if (Stage != null)
			{
				maxRingLabel.Visible = ringDividerSprite.Visible = Stage.MissionType == StageSettings.MissionTypes.Ring; //Show/Hide max ring count
				if (maxRingLabel.Visible)
					maxRingLabel.Text = Stage.ObjectiveCount.ToString(RING_LABEL_FORMAT);

				ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
				if (Stage.CurrentRingCount == 0) //Starting in a ringless state
					ringAnimator.Play("Ringless");
			}
		}

		private void UpdateRingCount(int amount)
		{
			if (amount > 0) //Play animation
			{
				ringAnimator.Play("CollectRing");
				ringAnimator.Seek(0.0f, true);
			}
			else
			{
				ringAnimator.Play("LoseRing");
				ringAnimator.AnimationSetNext("LoseRing", Stage.CurrentRingCount == 0 ? "Ringless" : "RESET");
			}

			ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
		}
		#endregion

		#region Time and Score
		[Export]
		private Label time;
		private void UpdateTime() => time.Text = Stage.DisplayTime;

		[Export]
		private Label score;
		private void UpdateScore() => score.Text = Stage.DisplayScore;
		#endregion

		#region Bonuses
		[Export]
		private AnimationPlayer bonusAnimator;
		[Export]
		private Array<NodePath> bonusLabels;
		private Label[] _bonusLabels;
		private int bonusCount = -1;
		private readonly Array<StageSettings.BonusType> bonusQueue = new Array<StageSettings.BonusType>();
		private const int MAX_BONUS_COUNT = 5; //How many bonuses can be onscreen at once
		private void InitializeBonuses()
		{
			_bonusLabels = new Label[bonusLabels.Count];

			for (int i = 0; i < _bonusLabels.Length; i++)
			{
				_bonusLabels[i] = GetNode<Label>(bonusLabels[i]);
				_bonusLabels[i].Modulate = Colors.Transparent;
			}
		}

		private void AddBonus(StageSettings.BonusType type)
		{
			if (bonusAnimator.IsPlaying())
			{
				bonusQueue.Add(type);
				return;
			}

			//Increment bonus count
			bonusCount++;
			if (bonusCount > MAX_BONUS_COUNT)
				bonusCount = MAX_BONUS_COUNT;

			//Update text
			for (int i = 1; i < _bonusLabels.Length; i++)
			{
				_bonusLabels[i].Text = _bonusLabels[i - 1].Text;
				_bonusLabels[i].Modulate = i > bonusCount ? Colors.Transparent : Colors.White;
			}
			_bonusLabels[0].Text = Tr(type.ToString());

			//Play animations
			bonusAnimator.Play("RESET");
			bonusAnimator.Seek(0, true);
			bonusAnimator.Play("AddBonus", -1, bonusQueue.Count != 0 ? 1.5f : 1f);
		}

		private float bonusTimer;
		private void UpdateBonus() //Fade out the last bonus text via code
		{
			if (GetTree().Paused) return; //Paused

			if (bonusQueue.Count != 0 && !bonusAnimator.IsPlaying())
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
		private TextureRect objectiveSprite;
		[Export]
		private Label objectiveValue;
		[Export]
		private Label objectiveMaxValue;
		private void InitializeObjectives()
		{
			if (Stage == null || Stage.MissionType != StageSettings.MissionTypes.Objective) return; //Don't do anything when not set to objective based mission

			objectiveSprite.Visible = true;
			objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
			objectiveMaxValue.Text = Stage.ObjectiveCount.ToString("00");

			Stage.Connect(nameof(StageSettings.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		}

		private void UpdateObjective() => objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
		#endregion

		#region Soul Gauge
		[Export]
		private Control soulGauge;
		[Export]
		private Control soulGaugeRoot;
		[Export]
		private Control soulGaugeFill;
		[Export]
		private Control soulGaugeBackground;
		[Export]
		private AnimationPlayer soulGaugeAnimator;
		private void InitializeSoulGauge()
		{
			//Initialize soul gauge
			soulGaugeBackground = soulGaugeFill.GetParent<Control>();

			//Resize the soul gauge
			int lerpFrom = Mathf.RoundToInt(soulGaugeRoot.Size.y - soulGauge.GetMinimumSize().y); //Smallest gauge size
			soulGauge.OffsetTop = Mathf.Lerp(lerpFrom, 0, SaveManager.ActiveGameData.SoulGaugeLevel); //Set the soul gauge to the correct size
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
			Vector2 end = Vector2.Down * Mathf.Lerp(soulGaugeBackground.Size.y + SOUL_GAUGE_FILL_OFFSET, 0, targetSoulGaugeRatio);
			soulGaugeFill.Position = ExtensionMethods.SmoothDamp(soulGaugeFill.Position, end, ref soulGaugeVelocity, 0.1f);
		}

		private bool isSoulGaugeCharged;
		public void UpdateSoulGaugeColor(bool isCharged)
		{
			if (!isSoulGaugeCharged && isCharged)
			{
				//Play animation
				isSoulGaugeCharged = true;
				soulGaugeAnimator.Play("charged");
			}
			else if (isSoulGaugeCharged && !isCharged)
			{
				//Lost charge
				isSoulGaugeCharged = false;
				soulGaugeAnimator.Play("RESET"); //Revert to blue
			}
		}
		#endregion

		public void StageComplete(bool _) => SetVisibility(false); //Ignore parameter
		public void SetVisibility(bool value) => Visible = value;
	}
}
