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

			InitializeRings();
			InitializeObjectives();
			InitializeSoulGauge();

			if (Stage != null) //Decouple from level settings
			{
				Stage.Connect(nameof(StageSettings.RingChanged), new Callable(this, MethodName.UpdateRingCount));
				Stage.Connect(nameof(StageSettings.TimeChanged), new Callable(this, MethodName.UpdateTime));
				Stage.Connect(nameof(StageSettings.ScoreChanged), new Callable(this, MethodName.UpdateScore));
				Stage.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, MethodName.LevelComplete)); //Hide interface
			}
		}

		public override void _PhysicsProcess(double _)
		{
			UpdateSoulGauge(); //Animate the soul gauge
		}

		#region Rings
		[ExportGroup("Rings")]
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
			if (Stage != null)
			{
				maxRingLabel.Visible = ringDividerSprite.Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Ring; // Show/Hide max ring count
				if (maxRingLabel.Visible)
					maxRingLabel.Text = Stage.Data.MissionObjectiveCount.ToString(RING_LABEL_FORMAT);

				int startingRingCount = 0; // TODO Determine by skills
				ringAnimator.Active = true;
				UpdateRingCount(startingRingCount, true);
				Stage.UpdateRingCount(startingRingCount, StageSettings.MathModeEnum.Replace, true);
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
					ringAnimator.Set(RING_LOSS_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				}
			}

			ringAnimator.Set(RINGLESS_PARAMETER, Stage.CurrentRingCount == 0 ? ENABLED_PARAMETER : DISABLED_PARAMETER);
			ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
		}
		#endregion

		#region Time and Score
		[ExportGroup("Time & Score")]
		[Export]
		private Label time;
		private void UpdateTime() => time.Text = Stage.DisplayTime;

		[Export]
		private Label score;
		private void UpdateScore() => score.Text = Stage.DisplayScore;
		#endregion

		#region Objectives
		[ExportGroup("Objective Counter")]
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
			objectiveRoot.Visible = Stage != null && Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective;
			if (!objectiveRoot.Visible) return; //Don't do anything when objective counter isn't visible

			objectiveSprite.Visible = true;
			objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
			objectiveMaxValue.Text = Stage.Data.MissionObjectiveCount.ToString("00");

			Stage.Connect(nameof(StageSettings.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		}

		private void UpdateObjective() => objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
		#endregion

		#region Soul Gauge
		[ExportGroup("Soul Gauge")]
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
			soulGauge.OffsetTop = Mathf.Lerp(soulGauge.OffsetTop, 0, SaveManager.ActiveGameData.CalculateSoulGaugeLevelRatio());
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
			else if (!isCharged)
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
