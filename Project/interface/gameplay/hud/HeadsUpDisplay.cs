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
		private LevelSettings Level => LevelSettings.instance;

		public override void _Ready()
		{
			instance = this;

			InitializeBonuses();
			InitializeRings();
			InitializeObjectives();
			InitializeSoulGauge();

			if (Level != null) //Decouple from level settings
			{
				Level.Connect(nameof(LevelSettings.RingChanged), new Callable(this, MethodName.UpdateRingCount));
				Level.Connect(nameof(LevelSettings.TimeChanged), new Callable(this, MethodName.UpdateTime));
				Level.Connect(nameof(LevelSettings.ScoreChanged), new Callable(this, MethodName.UpdateScore));
				Level.Connect(nameof(LevelSettings.BonusAdded), new Callable(this, MethodName.AddBonus));
				Level.Connect(nameof(LevelSettings.LevelCompleted), new Callable(this, MethodName.LevelComplete)); //Hide interface
			}
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateSoulGauge(); //Animate the soul gauge
			UpdateBonus();
		}

		#region Rings
		[ExportSubgroup("Rings")]
		[Export]
		private Label ringLabel;
		[Export]
		private Label maxRingLabel;
		[Export]
		private Label ringLossLabel;
		[Export]
		private Sprite2D ringDividerSprite;
		[Export]
		private AnimationTree ringAnimator;

		private const string RING_LABEL_FORMAT = "000";
		private readonly StringName ENABLED_PARAMETER = "enabled";
		private readonly StringName DISABLED_PARAMETER = "disabled";

		private readonly StringName RING_GAIN_PARAMETER = "parameters/gain_trigger/request";
		private readonly StringName RING_LOSS_PARAMETER = "parameters/loss_trigger/request";
		private readonly StringName RINGLESS_PARAMETER = "parameters/ringless_transition/transition_request";

		private void InitializeRings()
		{
			//Initialize ring counter
			if (Level != null)
			{
				maxRingLabel.Visible = ringDividerSprite.Visible = Level.MissionType == LevelSettings.MissionTypes.Ring; //Show/Hide max ring count
				if (maxRingLabel.Visible)
					maxRingLabel.Text = Level.ObjectiveCount.ToString(RING_LABEL_FORMAT);

				ringAnimator.Active = true;
				UpdateRingCount(0, true);
			}
		}

		private void UpdateRingCount(int amount, bool disableAnimations)
		{
			if (!disableAnimations && amount != 0) //Play animation
			{
				if (amount > 0)
					ringAnimator.Set(RING_GAIN_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				else
				{
					ringLossLabel.Text = amount.ToString();
					GD.Print(ringLossLabel.Text);
					ringAnimator.Set(RING_LOSS_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				}
			}

			ringAnimator.Set(RINGLESS_PARAMETER, Level.CurrentRingCount == 0 ? ENABLED_PARAMETER : DISABLED_PARAMETER);
			ringLabel.Text = Level.CurrentRingCount.ToString(RING_LABEL_FORMAT);
		}
		#endregion

		#region Time and Score
		[ExportSubgroup("Time & Score")]
		[Export]
		private Label time;
		private void UpdateTime() => time.Text = Level.DisplayTime;

		[Export]
		private Label score;
		private void UpdateScore() => score.Text = Level.DisplayScore;
		#endregion

		#region Bonuses
		[ExportSubgroup("Bonuses")]
		[Export]
		private AnimationPlayer bonusAnimator;
		[Export]
		private Array<NodePath> bonusLabels;
		private Label[] _bonusLabels;
		private int bonusCount = -1;
		private readonly Array<LevelSettings.BonusType> bonusQueue = new Array<LevelSettings.BonusType>();
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

		private void AddBonus(LevelSettings.BonusType type)
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
		[ExportSubgroup("Objective Counter")]
		[Export]
		private Control objectiveRoot;
		[Export]
		private TextureRect objectiveSprite;
		[Export]
		private Label objectiveValue;
		[Export]
		private Label objectiveMaxValue;
		private void InitializeObjectives()
		{
			objectiveRoot.Visible = Level != null && Level.MissionType == LevelSettings.MissionTypes.Objective;
			if (!objectiveRoot.Visible) return; //Don't do anything when objective counter isn't visible

			objectiveSprite.Visible = true;
			objectiveValue.Text = Level.CurrentObjectiveCount.ToString("00");
			objectiveMaxValue.Text = Level.ObjectiveCount.ToString("00");

			Level.Connect(nameof(LevelSettings.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		}

		private void UpdateObjective() => objectiveValue.Text = Level.CurrentObjectiveCount.ToString("00");
		#endregion

		#region Soul Gauge
		[ExportSubgroup("Soul Gauge")]
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
		/// <summary>
		/// Set soul gauge size based on player's level.
		/// </summary>
		private void InitializeSoulGauge()
		{
			soulGaugeBackground = soulGaugeFill.GetParent<Control>();

			//Resize the soul gauge
			soulGauge.OffsetTop = Mathf.Lerp(soulGauge.OffsetTop, 0, SaveManager.ActiveGameData.SoulGaugeLevel);
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
			Vector2 end = Vector2.Down * Mathf.Lerp(soulGaugeBackground.Size.Y + SOUL_GAUGE_FILL_OFFSET, 0, targetSoulGaugeRatio);
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

		public void LevelComplete() => SetVisibility(false); //Ignore parameter
		public void SetVisibility(bool value) => Visible = value;
	}
}
