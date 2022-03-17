using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class GameplayInterface : CanvasLayer
	{
		public static GameplayInterface instance;

		public override void _Ready()
		{
			instance = this;

			_hud = GetNode<Control>(hud);

			_countdownTweener = new Tween();
			AddChild(_countdownTweener);
			_countdownTickParent = GetNode<Node2D>(countdownTickParent);

			StartCountdown();
			InitializeRingCounter();
			InitializeSoulGauge();
			InitializePauseMenu();
		}

		public override void _Process(float _)
		{
			UpdatePauseMenu();
		}

		#region Countdown
		[Export]
		public bool skipCountdown;
		[Export]
		public NodePath countdownTickParent;
		private Node2D _countdownTickParent;
		[Export]
		public NodePath countdownAnimator;
		private AnimationPlayer _countdownAnimator;
		private Tween _countdownTweener;

		public bool IsCountDownComplete { get; private set; }
		public void OnCountdownCompleted()
		{
			_hud.Visible = true;
			IsCountDownComplete = true;
			canInteractWithPauseMenu = true; //Enable Pausing
		}

		private void StartCountdown()
		{
			_hud.Visible = false;
			IsCountDownComplete = false;
			canInteractWithPauseMenu = false; //Disable Pausing

			if (skipCountdown)
				OnCountdownCompleted();
			else
			{
				_countdownAnimator = GetNode<AnimationPlayer>(countdownAnimator);
				_countdownAnimator.Play("Countdown");
			}

			TweenCountdownTicks();
		}

		//The ring animation is too tedious to animate by hand, so I'm using a tween instead.
		private void TweenCountdownTicks()
		{
			_countdownTweener.ResetAll();

			for (int i = 0; i < _countdownTickParent.GetChildCount(); i++)
			{
				Node2D tick = _countdownTickParent.GetChild<Node2D>(i);

				float delay = i * .04f + .6f;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position, tick.Position + (tick.Position.Normalized() * 48f), .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.White, Colors.Transparent, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);

				delay += 1;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position + (tick.Position.Normalized() * 48f), tick.Position, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.Transparent, Colors.White, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);

				delay += 1;
				_countdownTweener.InterpolateProperty(tick, "position", tick.Position, tick.Position + (tick.Position.Normalized() * 48f), .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
				_countdownTweener.InterpolateProperty(tick, "modulate", Colors.White, Colors.Transparent, .2f, Tween.TransitionType.Linear, Tween.EaseType.InOut, delay);
			}

			_countdownTweener.Start();
		}
		#endregion

		#region Heads up display
		[Export]
		public NodePath hud;
		private Control _hud;

		[Export]
		public NodePath ringLabel;
		private Label _ringLabel;
		[Export]
		public NodePath maxRingLabel;
		private Label _maxRingLabel;
		[Export]
		public NodePath ringAnimator;
		private AnimationPlayer _ringAnimator;

		public int Score { get; private set; }
		private int ringCount;
		private int maxRingCount;

		private void InitializeRingCounter()
		{
			_ringLabel = GetNode<Label>(ringLabel);
			_maxRingLabel = GetNode<Label>(maxRingLabel);
			_ringAnimator = GetNode<AnimationPlayer>(ringAnimator);

			//TODO set ring count based on skills
			maxRingCount = StageManager.instance.maxRingCount;
			_maxRingLabel.Text = maxRingCount.ToString("000");

			_ringLabel.Text = ringCount.ToString("000");

			if (ringCount == 0)
				_ringAnimator.Play("Ringless");
		}

		public void CollectRing(int amount)
		{
			//Play animation
			if (ringCount != maxRingCount)
				_ringAnimator.Play("CollectRing");

			ringCount += amount;
			ringCount = Mathf.Clamp(ringCount, 0, maxRingCount);

			if (amount > 0)
			{
				//Play SFX and particle effect
				CharacterController.instance.PlayRingParticleEffect();
			}

			_ringLabel.Text = ringCount.ToString("000");
		}

		public void LoseRings(int amount)
		{
			ringCount -= amount;
			if (ringCount <= 0)
				ringCount = 0;

			_ringLabel.Text = ringCount.ToString("000");

			_ringAnimator.Play("LoseRing");
			_ringAnimator.AnimationSetNext("LoseRing", ringCount == 0 ? "Ringless" : "RESET");
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
		private Tween _soulGaugeTweener;

		public bool IsSoulGaugeFull => soulPower == maxSoulPower;

		private bool isSoulGaugeCharged;
		private int soulPower;
		private int maxSoulPower;

		private float currentSoulPowerLength; //The length (in seconds) of soul skill

		private const int SOUL_GAUGE_BASE = 100; //Base size of soul gauge
		private const int SOUL_GAUGE_MAX = 300; //Max size of soul gauge
		private const int MINIMUM_SOUL_POWER = 50; //Minimum amount of soul power needed to use soul skills.

		private const int SOUL_GAUGE_FILL_OFFSET = 15;

		private void InitializeSoulGauge()
		{
			_soulGauge = GetNode<Control>(soulGauge);
			_soulGaugeRoot = _soulGauge.GetParent<Control>();
			_soulGaugeFill = GetNode<Control>(soulGaugeFill);
			_soulGaugeBackground = _soulGaugeFill.GetParent<Control>();
			_soulGaugeAnimator = GetNode<AnimationPlayer>(soulGaugeAnimator);
			_soulGaugeTweener = new Tween();
			AddChild(_soulGaugeTweener);

			//Soul Gauge increases by 20 every 5 levels, caps at 300 (level 50).
			float levelRatio = Mathf.Clamp(SaveManager.ActiveGameData.level, 0, 50) / 5f; //Current ratio (0 -> 10) compared to the soul gauge level cap (50)
			maxSoulPower = SOUL_GAUGE_BASE + Mathf.FloorToInt(levelRatio) * 20;

			int lerpFrom = Mathf.RoundToInt(_soulGaugeRoot.RectSize.y - _soulGauge.RectMinSize.y);
			levelRatio *= .1f; //Convert from 0 -> 10 to 0 -> 1
			_soulGauge.MarginTop = Mathf.Lerp(lerpFrom, 0, levelRatio); //Set the soul gauge to the correct size
			UpdateSoulFill(0f);
		}

		public void CollectSoulPearl(int amount)
		{
			soulPower += amount;
			soulPower = Mathf.Clamp(soulPower, 0, maxSoulPower);
			currentSoulPowerLength = soulPower / 60f;
			UpdateSoulFill((float)soulPower / maxSoulPower);

			if (!isSoulGaugeCharged && soulPower >= MINIMUM_SOUL_POWER)
			{
				//Play animation & sfx
				isSoulGaugeCharged = true;
				_soulGaugeAnimator.Play("Charged");
			}
			else if (isSoulGaugeCharged && soulPower < MINIMUM_SOUL_POWER)
			{
				//Lost charge
				isSoulGaugeCharged = false;
				_soulGaugeAnimator.Play("RESET"); //Revert to blue
			}
		}

		private void UpdateSoulFill(float t)
		{
			_soulGaugeTweener.StopAll();

			Vector2 end = Vector2.Down * Mathf.Lerp(_soulGaugeBackground.RectSize.y + SOUL_GAUGE_FILL_OFFSET, 0, t);
			_soulGaugeTweener.InterpolateProperty(_soulGaugeFill, "rect_position", _soulGaugeFill.RectPosition, end, .08f, Tween.TransitionType.Sine, Tween.EaseType.Out);
			_soulGaugeTweener.Start();
		}
		#endregion

		#region Pause
		[Export]
		public NodePath pauseAnimator;
		private AnimationPlayer _pauseAnimator;

		[Export]
		public NodePath pauseCursor;
		private Node2D _pauseCursor;

		private bool canInteractWithPauseMenu;
		private void InitializePauseMenu()
		{
			_pauseAnimator = GetNode<AnimationPlayer>(pauseAnimator);
			_pauseCursor = GetNode<Node2D>(pauseCursor);
		}

		private void TogglePause()
		{
			canInteractWithPauseMenu = false; //Disable pause inputs during the animation
			GetTree().Paused = !GetTree().Paused;
			_pauseAnimator.Play(GetTree().Paused ? "Pause" : "Unpause");
		}

		public void OnPauseToggled() //Called after the pause animation is completed
		{
			canInteractWithPauseMenu = true;
		}

		private void UpdatePauseMenu()
		{
			if (!canInteractWithPauseMenu) return;

			if (InputManager.controller.pauseButton.wasPressed)
			{
				TogglePause();
				return;
			}

			if (!GetTree().Paused)
			{

			}
		}
		#endregion

		#region Camera Transition
		//Used for instant camera transitions
		[Export]
		public NodePath cameraTransition;
		private TextureRect _cameraTransition;
		[Export]
		public NodePath cameraTransitionAnimator;
		private AnimationPlayer _cameraTransitionAnimator;
		public void PlayCameraTransition(ImageTexture texture)
		{
			if (_cameraTransition == null)
			{
				_cameraTransition = GetNode<TextureRect>(cameraTransition);
				_cameraTransitionAnimator = GetNode<AnimationPlayer>(cameraTransitionAnimator);
			}

			_cameraTransition.Texture = texture;
			_cameraTransitionAnimator.Play("Transition");
		}
		#endregion
	}
}
