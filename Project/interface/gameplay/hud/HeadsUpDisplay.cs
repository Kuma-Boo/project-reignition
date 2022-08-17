using Godot;
using System;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Displays game data to the player. Only handles the graphics.
	/// </summary>
	public class HeadsUpDisplay : Control
	{
		public static HeadsUpDisplay instance;
		private StageSettings Stage => StageSettings.instance;

		public override void _Ready()
		{
			instance = this;

			_time = GetNode<Label>(time);

			//Initialize ring counter
			_ringLabel = GetNode<Label>(ringLabel);
			_maxRingLabel = GetNode<Label>(maxRingLabel);
			_ringDividerSprite = GetNode<Sprite>(ringDividerSprite);
			_ringAnimator = GetNode<AnimationPlayer>(ringAnimator);

			if(Stage != null)
			{
				Stage.Connect(nameof(StageSettings.RingChanged), this, nameof(UpdateRingCount));
				Stage.Connect(nameof(StageSettings.TimeChanged), this, nameof(UpdateTime));
				//Stage.Connect(nameof(StageSettings.ObjectiveChanged), this, nameof(UpdateObjective));
				//Stage.Connect(nameof(StageSettings.ScoreChanged), this, nameof(UpdateScore));

				_maxRingLabel.Visible = _ringDividerSprite.Visible = Stage.missionType == StageSettings.MissionType.Ring; //Show/Hide max ring count
				if (_maxRingLabel.Visible)
					_maxRingLabel.Text = Stage.targetObjectiveCount.ToString(RING_LABEL_FORMAT);

				_ringLabel.Text = Stage.CurrentRingCount.ToString(RING_LABEL_FORMAT);
				if (Stage.CurrentRingCount == 0) //Starting in a ringless state
					_ringAnimator.Play("Ringless");
			}

			//Initialize soul gauge
			_soulGauge = GetNode<Control>(soulGauge);
			_soulGaugeRoot = _soulGauge.GetParent<Control>();
			_soulGaugeFill = GetNode<Control>(soulGaugeFill);
			_soulGaugeBackground = _soulGaugeFill.GetParent<Control>();
			_soulGaugeAnimator = GetNode<AnimationPlayer>(soulGaugeAnimator);

			//Resize the soul gauge
			int lerpFrom = Mathf.RoundToInt(_soulGaugeRoot.RectSize.y - _soulGauge.RectMinSize.y); //Smallest gauge size
			_soulGauge.MarginTop = Mathf.Lerp(lerpFrom, 0, SaveManager.ActiveGameData.SoulGaugeLevel); //Set the soul gauge to the correct size
			ModifySoulGauge(0f, false);
		}

		public override void _PhysicsProcess(float _)
		{
			UpdateSoulGauge(); //Animate the soul gauge
		}

		#region Heads up display
		[Export]
		public NodePath ringLabel;
		private Label _ringLabel;
		[Export]
		public NodePath maxRingLabel;
		private Label _maxRingLabel;
		[Export]
		public NodePath ringDividerSprite;
		private Sprite _ringDividerSprite;
		[Export]
		public NodePath ringAnimator;
		private AnimationPlayer _ringAnimator;
		private const string RING_LABEL_FORMAT = "000";

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

		[Export]
		public NodePath time;
		private Label _time;
		private const string TIME_LABEL_FORMAT = "mm':'ss'.'ff";
		private void UpdateTime()
		{
			TimeSpan time = TimeSpan.FromSeconds(Stage.CurrentTime);
			_time.Text = time.ToString(TIME_LABEL_FORMAT);
		}

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
			Vector2 end = Vector2.Down * Mathf.Lerp(_soulGaugeBackground.RectSize.y + SOUL_GAUGE_FILL_OFFSET, 0, targetSoulGaugeRatio);
			_soulGaugeFill.RectPosition = ExtensionMethods.SmoothDamp(_soulGaugeFill.RectPosition, end, ref soulGaugeVelocity, 0.1f);
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

		public void CountdownStarted() => Visible = false;
		public void CountdownCompleted() => Visible = true;
	}
}
