using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Handles the Soul Gauge & Skills.
	/// </summary>
	public partial class CharacterSkillManager : Node
	{
		private CharacterController Character => CharacterController.instance;

		[Export(PropertyHint.Layers3dPhysics)]
		public uint speedBreakCollisionMask;
		private uint normalCollisionMask;

		public override void _Ready()
		{
			//Determine the size of the soul gauge
			float levelRatio = SaveManager.ActiveGameData.SoulGaugeLevel; //Current ratio (0 -> 10) compared to the soul gauge level cap (50)
			maxSoulPower = SOUL_GAUGE_BASE + Mathf.FloorToInt(levelRatio * 10f) * 20; //Soul Gauge size increases by 20 every 5 levels, caps at 300 (level 50).
			normalCollisionMask = Character.CollisionMask;

			SetUpSkills();
		}

		//Cancel time break, just in case
		public override void _ExitTree() => IsTimeBreakEnabled = false;

		#region Skills
		[Export]
		public float accelerationJumpSpeed;

		[Export]
		public float unchargedGrindSpeed;
		[Export]
		public float chargedGrindSpeed;
		[Export]
		public MovementResource grindSettings; //Settings for grinding on rails

		[Export]
		public float landingDashSpeed;
		public bool IsLandingDashEnabled { get; private set; }

		[Export(PropertyHint.Range, "1,5,.1")]
		public float pearlAttractorMultiplier = 2f; //Collision multiplier when PearlAttractor skill is enabled 
		private const int ENEMY_PEARL_AMOUNT = 16; //How many pearls are obtained when defeating an enemy

		private void SetUpSkills()
		{
			IsLandingDashEnabled = SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.LandingBoost);

			//Expand hitbox if skills is equipped
			if (SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.PearlAttractor))
				RuntimeConstants.UpdatePearlCollisionShapes(pearlAttractorMultiplier);
			else
				RuntimeConstants.UpdatePearlCollisionShapes();
		}
		#endregion

		#region Soul Skills
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
		private bool isSpeedBreakEnabled = true;
		private bool isTimeBreakEnabled = true;

		[Export]
		public float speedBreakSpeed; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;

		private float breakTimer = 0; //Timer for break skills
		private const float SPEEDBREAK_DELAY = 0.12f; //Time to say SPEED BREAK!
		private const float BREAK_SKILLS_COOLDOWN = 1f; //Prevent skill spam
		public const float TIME_BREAK_RATIO = .6f; //Time scale

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

					if (Character.IsOnGround) //Speed is only applied while on the ground
						Character.MoveSpeed = speedBreakSpeed;
				}
				else
					Character.MoveSpeed = 0f;

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
				BGMPlayer.SetStageMusicVolume(-80f);
			}
			else
			{
				breakTimer = BREAK_SKILLS_COOLDOWN;
				BGMPlayer.SetStageMusicVolume(0f);


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
			{
				Character.Sound.PlayVoice(0);
				Character.CollisionMask = speedBreakCollisionMask; //Don't collide with any objects
			}
			else
			{
				Character.MoveSpeed = Character.groundSettings.speed; //Override speed
				Character.CollisionMask = normalCollisionMask; //Reset collision layer
			}

			if (HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.UpdateSoulGaugeColor(IsSoulGaugeCharged);
		}

		private int soulPower; //Current soul power
		private int maxSoulPower; //Calculated on start

		private bool IsSoulGaugeEmpty => soulPower == 0;
		private bool IsSoulGaugeCharged => soulPower >= MINIMUM_SOUL_POWER;

		private const int MINIMUM_SOUL_POWER = 50; //Minimum amount of soul power needed to use soul skills.
		private const int SOUL_GAUGE_BASE = 100; //Starting size of soul gauge
		private const int SOUL_GAUGE_MAX = 300; //Max size of soul gauge
		public void ModifySoulGauge(int amount)
		{
			soulPower = Mathf.Clamp(soulPower + amount, 0, maxSoulPower);

			if (HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.ModifySoulGauge((float)soulPower / maxSoulPower, IsSoulGaugeCharged);
		}
		#endregion
	}
}
