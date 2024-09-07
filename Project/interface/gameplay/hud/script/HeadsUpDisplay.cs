using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Displays game data to the player. Only handles the graphics.
/// </summary>
public partial class HeadsUpDisplay : Control
{
	public static HeadsUpDisplay instance;
	private StageSettings Stage => StageSettings.instance;

	public override void _EnterTree() => instance = this;

	public override void _Ready()
	{
		InitializeRankPreviewer();
		InitializeRings();
		InitializeObjectives();
		InitializeSoulGauge();
		InitializeRace();

		if (Stage != null) // Decouple from level settings
		{
			Stage.Connect(nameof(StageSettings.RingChanged), new Callable(this, MethodName.UpdateRingCount));
			Stage.Connect(nameof(StageSettings.TimeChanged), new Callable(this, MethodName.UpdateTime));
			Stage.Connect(nameof(StageSettings.ScoreChanged), new Callable(this, MethodName.UpdateScore));
			Stage.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, MethodName.OnLevelCompleted)); // Hide interface
		}
	}

	public override void _PhysicsProcess(double _) => UpdateSoulGauge(); // Animate the soul gauge

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
	[Export]
	private AnimationPlayer fireSoulAnimator;
	public void CollectFireSoul()
	{
		fireSoulAnimator.Play("firesoul");
		fireSoulAnimator.Seek(0.0, true);
	}

	private const string RingLabelFormat = "000";
	private readonly StringName EnabledParameter = "enabled";
	private readonly StringName DisabledParameter = "disabled";

	private readonly StringName RingGainParameter = "parameters/gain_trigger/request";
	private readonly StringName RingLossParameter = "parameters/loss_trigger/request";
	private readonly StringName RinglessParameter = "parameters/ringless_transition/transition_request";

	private void InitializeRings()
	{
		// Initialize ring counter
		if (Stage != null)
		{
			maxRingLabel.Visible = ringDividerSprite.Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Ring; // Show/Hide max ring count
			if (maxRingLabel.Visible)
				maxRingLabel.Text = Stage.Data.MissionObjectiveCount.ToString(RingLabelFormat);

			ringAnimator.Active = true;
			UpdateRingCount(Stage.CurrentRingCount, true);
		}
	}

	private void UpdateRingCount(int amount, bool disableAnimations)
	{
		if (!disableAnimations) // Play animation
		{
			if (amount >= 0)
			{
				ringAnimator.Set(RingGainParameter, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
			else
			{
				ringLossLabel.Text = amount.ToString();
				ringAnimator.Set(RingLossParameter, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}

		ringAnimator.Set(RinglessParameter, Stage.CurrentRingCount == 0 ? EnabledParameter : DisabledParameter);
		ringLabel.Text = Stage.CurrentRingCount.ToString(RingLabelFormat);
	}
	#endregion

	#region Time and Score
	[ExportGroup("Time & Score")]
	[Export]
	private Node2D rankPreviewerRoot;
	[Export]
	private Sprite2D mainRank;
	[Export]
	private Sprite2D transitionRank;
	[Export]
	private AudioStreamPlayer rankUpSFX;
	[Export]
	private AudioStreamPlayer rankDownSFX;
	private int CurrentRank { get; set; }
	private Tween rankTween;
	private void InitializeRankPreviewer()
	{
		rankPreviewerRoot.Visible = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RankPreview);
		if (!rankPreviewerRoot.Visible)
			return;

		CurrentRank = Stage.CalculateRank(true);
		mainRank.RegionRect = new(mainRank.RegionRect.Position + (Vector2.Down * CurrentRank * 60), mainRank.RegionRect.Size);
	}

	private void UpdateRankPreviewer()
	{
		if (!rankPreviewerRoot.Visible)
			return;

		int rank = Stage.CalculateRank(true);
		if (CurrentRank == rank || rankTween?.IsRunning() == true)
			return;

		int rankDirection = rank - CurrentRank;
		if (rankDirection < 0)
			StartRankDownTween(rankDirection);
		else
			StartRankUpTween(rankDirection);

		CurrentRank = rank;
	}

	private void StartRankDownTween(int amount)
	{
		rankDownSFX.Play();
		transitionRank.RegionRect = mainRank.RegionRect;
		transitionRank.SelfModulate = Colors.White;
		mainRank.RegionRect = new(mainRank.RegionRect.Position + (Vector2.Down * amount * 60), mainRank.RegionRect.Size);
		rankTween = CreateTween().SetParallel();
		rankTween.TweenProperty(transitionRank, "self_modulate", Colors.Transparent, .5f);
		rankTween.TweenProperty(transitionRank, "position", Vector2.Down * 128, .5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		rankTween.TweenCallback(new Callable(this, MethodName.CompleteRankDownTween)).SetDelay(.5f);
	}

	private void StartRankUpTween(int amount)
	{
		rankUpSFX.Play();
		transitionRank.RegionRect = new(mainRank.RegionRect.Position + (Vector2.Down * amount * 60), mainRank.RegionRect.Size);
		transitionRank.Position += Vector2.Up * 256;
		rankTween = CreateTween().SetParallel();
		rankTween.TweenProperty(transitionRank, "self_modulate", Colors.White, .5f);
		rankTween.TweenProperty(transitionRank, "position", Vector2.Zero, .5f).SetTrans(Tween.TransitionType.Bounce);
		rankTween.TweenCallback(new Callable(this, MethodName.CompleteRankUpTween)).SetDelay(.5f);
	}


	private void CompleteRankUpTween()
	{
		mainRank.RegionRect = transitionRank.RegionRect;
		transitionRank.SelfModulate = Colors.Transparent;
		rankTween.Kill();
	}


	private void CompleteRankDownTween() => rankTween.Kill();


	[Export]
	private Label time;
	private void UpdateTime()
	{
		if (Stage.Data.MissionTimeLimit != 0) // Time limit; Draw time counting DOWN
		{
			float timeLeft = Mathf.Clamp(Stage.Data.MissionTimeLimit - Stage.CurrentTime, 0, Stage.Data.MissionTimeLimit);
			time.Text = ExtensionMethods.FormatTime(timeLeft);
			return;
		}

		time.Text = Stage.DisplayTime;
		UpdateRankPreviewer(); // Update rank every frame
	}

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
	[Export]
	private AudioStreamPlayer objectiveSfx;
	private void InitializeObjectives()
	{
		objectiveRoot.Visible = Stage != null &&
			Stage.Data.MissionObjectiveCount != 0 &&
			(Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Enemy ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain);
		if (!objectiveRoot.Visible) return; // Don't do anything when objective counter isn't visible

		// TODO Implement proper objective sprites
		objectiveSprite.Visible = false;
		objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
		objectiveMaxValue.Text = Stage.Data.MissionObjectiveCount.ToString("00");

		Stage.Connect(nameof(StageSettings.SignalName.ObjectiveChanged), new Callable(this, nameof(UpdateObjective)));
		Stage.Connect(nameof(StageSettings.SignalName.ObjectiveReset), new Callable(this, nameof(ResetObjective)));
	}

	private void UpdateObjective()
	{
		if (Stage.Data.MissionType == LevelDataResource.MissionTypes.Objective ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Enemy ||
			Stage.Data.MissionType == LevelDataResource.MissionTypes.Chain)
		{
			objectiveSfx.Play();
		}
		objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
	}

	private void ResetObjective() => objectiveValue.Text = Stage.CurrentObjectiveCount.ToString("00");
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

		// Resize the soul gauge
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
	private const int SoulGaugeChargePoint = 96;
	private const int SoulGaugeFillOffset = 15;
	private void UpdateSoulGauge()
	{
		float chargePoint = soulGaugeBackground.Size.Y - SoulGaugeChargePoint;
		float targetPosition;
		if (isSoulGaugeCharged)
			targetPosition = Mathf.Lerp(chargePoint, 0, targetSoulGaugeRatio);
		else
			targetPosition = Mathf.Lerp(soulGaugeBackground.Size.Y + SoulGaugeFillOffset, chargePoint, targetSoulGaugeRatio);

		soulGaugeFill.Position = soulGaugeFill.Position.SmoothDamp(Vector2.Down * targetPosition, ref soulGaugeVelocity, 0.1f);
	}

	private bool isSoulGaugeCharged;
	public void UpdateSoulGaugeColor(bool isCharged)
	{
		if (!isSoulGaugeCharged && isCharged)
		{
			// Play animation
			isSoulGaugeCharged = true;
			soulGaugeAnimator.Play("charged");
		}
		else if (!isCharged)
		{
			// Lost charge
			isSoulGaugeCharged = false;
			soulGaugeAnimator.Play("RESET"); // Revert to blue
		}
	}
	#endregion

	#region Race
	[ExportGroup("Race")]
	[Export]
	private Control raceRoot;
	[Export]
	private Control raceUhu;
	[Export]
	private Control racePlayer;
	private float uhuVelocity;
	private float playerVelocity;
	private readonly float RaceEndPoint = 512;
	private readonly float RaceSmoothing = 20.0f;
	private void InitializeRace()
	{
		if (Stage == null)
			return;

		raceRoot.Visible = Stage.Data.MissionType == LevelDataResource.MissionTypes.Race;
	}

	public void UpdateRace(float playerRatio, float uhuRatio)
	{
		float uhuPosition = raceUhu.Position.X;
		float playerPosition = racePlayer.Position.X;
		uhuPosition = ExtensionMethods.SmoothDamp(uhuPosition, Mathf.Lerp(0, RaceEndPoint, uhuRatio), ref uhuVelocity, RaceSmoothing * PhysicsManager.physicsDelta);
		playerPosition = ExtensionMethods.SmoothDamp(playerPosition, Mathf.Lerp(0, RaceEndPoint, playerRatio), ref playerVelocity, RaceSmoothing * PhysicsManager.physicsDelta);

		raceUhu.Position = new(uhuPosition, raceUhu.Position.Y);
		racePlayer.Position = new(playerPosition, racePlayer.Position.Y);

		raceUhu.ZIndex = playerRatio >= uhuRatio ? 0 : 1;
		racePlayer.ZIndex = playerRatio >= uhuRatio ? 1 : 0;
	}

	#endregion

	public void OnLevelCompleted() => SetVisibility(false); // Ignore parameter
	public void SetVisibility(bool value)
	{
		if (OS.IsDebugBuild() && DebugManager.Instance.DisableHUD)
		{
			Visible = false;
			return;
		}

		Visible = value;
	}
}