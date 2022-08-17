using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Handles the Soul Gauge and Lockon Targets.
	/// </summary>
	public class CharacterSoulSkill : Node
	{
		public bool IsTimeBreakEnabled
		{
			get => isTimeBreakEnabled;
			set
			{
				isTimeBreakEnabled = value;
				if (IsTimeBreakActive && !isTimeBreakEnabled) //Cancel time break
					ToggleTimeBreak();
			}
		}
		public bool IsSpeedBreakEnabled
		{
			get => isSpeedBreakEnabled;
			set
			{
				isSpeedBreakEnabled = value;
				if (IsSpeedBreakActive && !isSpeedBreakEnabled) //Cancel speed break
					ToggleSpeedBreak();
			}
		}
		[Export]
		public bool isSpeedBreakEnabled;
		[Export]
		public bool isTimeBreakEnabled;

		[Export]
		public float speedBreakSpeed = 54; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;
		
		private float breakTimer = 0; //Timer for break skills
		private const float SPEEDBREAK_DELAY = 0.12f; //Time to say SPEED BREAK!
		private const float BREAK_SKILLS_COOLDOWN = 1f; //Prevent skill spam
		public const float TIME_BREAK_RATIO = .6f; //Time scale

		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			float levelRatio = SaveManager.ActiveGameData.SoulGaugeLevel; //Current ratio (0 -> 10) compared to the soul gauge level cap (50)
			maxSoulPower = SOUL_GAUGE_BASE + Mathf.FloorToInt(levelRatio * 10f) * 20; //Soul Gauge size increases by 20 every 5 levels, caps at 300 (level 50).
		}

		//Cancel time break, just in case
		public override void _ExitTree() => IsTimeBreakEnabled = false;

		public void UpdateSoulSkills()
		{
			if (CheatManager.InfiniteSoulGauge) //Max out the soul gauge
				ModifySoulGauge(SOUL_GAUGE_MAX);

			UpdateTimeBreak();
			UpdateSpeedBreak();

			breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
		}

		private int timeBreakDrainTimer;
		private const int TIME_BREAK_SOUL_DRAIN_INTERVAL = 3; //Drain 1 point every x frames
		private void UpdateTimeBreak()
		{
			if (IsTimeBreakActive)
			{
				if (timeBreakDrainTimer <= 0)
				{
					ModifySoulGauge(-1);
					timeBreakDrainTimer = TIME_BREAK_SOUL_DRAIN_INTERVAL;
				}
				timeBreakDrainTimer--;

				if (IsSoulGaugeEmpty || !Character.Controller.breakButton.isHeld) //Cancel time break?
					ToggleTimeBreak();

				return;
			}
			else if (breakTimer != 0) return; //Cooldown

			if (Character.Controller.breakButton.wasPressed && !IsSpeedBreakActive)
			{
				if (!IsSoulGaugeCharged) return;
				ToggleTimeBreak();
			}
		}

		private void UpdateSpeedBreak()
		{
			if (IsSpeedBreakActive)
			{
				if (breakTimer == 0)
				{
					ModifySoulGauge(-1); //Drain soul gauge
					if (IsSoulGaugeEmpty || !Character.Controller.boostButton.isHeld)//Check whether we shoudl cancel speed break
						ToggleSpeedBreak();

					if (Character.IsOnGround)
						Character.MoveSpeed = speedBreakSpeed;
				}
				else
					Character.MoveSpeed = Character.StrafeSpeed = 0f;

				return;
			}
			else if (breakTimer != 0) return; //Cooldown

			//Check whether we can start speed break
			if (Character.Controller.boostButton.wasPressed && !IsTimeBreakActive)
			{
				if (!IsSoulGaugeCharged) return;
				if (Character.MovementState == CharacterController.MovementStates.Launcher) return;
				if (!Character.IsOnGround) return;

				ToggleSpeedBreak();
			}
		}

		public void ToggleTimeBreak()
		{
			timeBreakDrainTimer = 0;
			IsTimeBreakActive = !IsTimeBreakActive;
			Engine.TimeScale = IsTimeBreakActive ? TIME_BREAK_RATIO : 1f;

			if (IsTimeBreakActive)
			{
				Character.Sound.PlayVoice(1);
				StageSettings.instance.SetMusicVolume(-80f);
			}
			else
			{
				breakTimer = BREAK_SKILLS_COOLDOWN;
				StageSettings.instance.SetMusicVolume(0f);


				if (HeadsUpDisplay.instance != null)
					HeadsUpDisplay.instance.UpdateSoulGaugeColor(IsSoulGaugeCharged);
			}
		}

		public void ToggleSpeedBreak()
		{
			Character.ResetActionState();

			IsSpeedBreakActive = !IsSpeedBreakActive;
			breakTimer = IsSpeedBreakActive ? SPEEDBREAK_DELAY : BREAK_SKILLS_COOLDOWN;

			if (IsSpeedBreakActive)
				Character.Sound.PlayVoice(0);
			else
				Character.MoveSpeed = Character.moveSettings.speed;

			if(HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.UpdateSoulGaugeColor(IsSoulGaugeCharged);
		}

		private int soulPower; //Current soul power
		private int maxSoulPower; //Calculated on start

		private bool IsSoulGaugeEmpty => soulPower == 0;
		private bool IsSoulGaugeCharged => soulPower > 50;

		private const int MINIMUM_SOUL_POWER = 50; //Minimum amount of soul power needed to use soul skills.
		private const int SOUL_GAUGE_BASE = 100; //Starting size of soul gauge
		private const int SOUL_GAUGE_MAX = 300; //Max size of soul gauge
		public void ModifySoulGauge(int amount)
		{
			soulPower = Mathf.Clamp(soulPower + amount, 0, maxSoulPower);

			if(HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.ModifySoulGauge((float)soulPower / maxSoulPower, soulPower >= MINIMUM_SOUL_POWER);
		}
	}
}
