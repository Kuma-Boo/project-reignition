using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Handles the Soul Gauge, Skills, and Stats.
	/// </summary>
	public partial class CharacterSkillManager : Node
	{
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			//Determine the size of the soul gauge
			float levelRatio = SaveManager.ActiveGameData.SoulGaugeLevel; //Current ratio (0 -> 10) compared to the soul gauge level cap (50)
			maxSoulPower = SOUL_GAUGE_BASE + Mathf.FloorToInt(levelRatio * 10f) * 20; //Soul Gauge size increases by 20 every 5 levels, caps at 300 (level 50).
			normalCollisionMask = Character.CollisionMask;

			SetUpStats();
			SetUpSkills();
		}

		//Cancel time break, just in case
		public override void _ExitTree() => IsTimeBreakEnabled = false;

		#region Stats
		[ExportCategory("Stats")]
		[Export]
		private MovementResource baseGroundSettings; //Slowest Sonic
		[Export]
		private MovementResource maxGroundSettings; //Fastest Sonic

		[Export]
		public MovementResource strafeSettings; //Strafe settings used during strafe sections
		[Export]
		public MovementResource speedbreakStrafeSettings; //Strafe settings used during speed break

		[Export]
		private MovementResource baseAirSettings; //Slowest Sonic
		[Export]
		private MovementResource maxAirSettings; //Fastest Sonic
		[Export]
		public float accelerationJumpSpeed;
		[Export]
		public float homingAttackSpeed;

		[Export]
		public float baseSlideFriction; //Shortest slide
		[Export]
		public float maxSlideFriction; //Longest slide

		//While there aren't any upgrades for the following movement skills, they're here for consistancy
		[Export]
		private MovementResource backflipSettings;
		[Export]
		private MovementResource backstepSettings;

		[ExportCategory("Grind Settings")]
		[Export]
		public float perfectShuffleSpeed;
		[Export]
		public MovementResource grindSettings; //Settings for grinding on rails

		[ExportCategory("Sidle Settings")]
		[Export]
		public Curve sidleMovementCurve;

		//References to the actual values being used
		public MovementResource GroundSettings { get; private set; }
		public MovementResource AirSettings { get; private set; }
		public MovementResource BackflipSettings { get; private set; }
		public MovementResource BackstepSettings { get; private set; }

		public float SlideFriction { get; private set; }

		private void SetUpStats() //Stuff like upgradable speed, increased handling, etc.
		{
			//TODO Interpolate values based on skill ring settings
			GroundSettings = baseGroundSettings;
			AirSettings = baseAirSettings;
			BackflipSettings = backflipSettings;
			BackstepSettings = backstepSettings;

			SlideFriction = baseSlideFriction;
		}
		#endregion


		#region Skills
		[ExportCategory("Countdown Skills")]
		[Export]
		public bool isCountdownBoostEnabled;
		[Export]
		public float countdownBoostSpeed;

		[Export]
		public bool isSplashJumpEnabled;
		public void SplashJump()
		{
			GD.Print("Splash Jump isn't implemented yet.");
		}

		/// <summary> Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster. </summary>
		[Export]
		public float landingDashSpeed;
		[Export]
		public bool isLandingDashEnabled;

		public bool IsAttacking { get; set; } //Is the player using an attack skill? (i.e Any of the fire skills)

		[Export(PropertyHint.Range, "1,5,.1")]
		public float pearlAttractorMultiplier = 2f; //Collision multiplier when PearlAttractor skill is enabled
		[Export]
		private bool isPearlAttractionEnabled;
		private const int ENEMY_PEARL_AMOUNT = 16; //How many pearls are obtained when defeating an enemy

		private void LoadSkillsFromSaveData()
		{
			isLandingDashEnabled = SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.LandingBoost);
			isPearlAttractionEnabled = SaveManager.ActiveGameData.skillRing.equippedSkills.IsSet(SaveManager.SkillRing.Skills.PearlAttractor);
		}

		private void SetUpSkills()
		{
			if (!CheatManager.UseEditorSkillValues)
				LoadSkillsFromSaveData();

			//Expand hitbox if skills is equipped
			if (isPearlAttractionEnabled)
				RuntimeConstants.Instance.UpdatePearlCollisionShapes(pearlAttractorMultiplier);
			else
				RuntimeConstants.Instance.UpdatePearlCollisionShapes();
		}
		#endregion

		#region Soul Skills
		private uint normalCollisionMask;
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

		//Audio clips
		[Export]
		private AudioStream speedBreakActivate;
		[Export]
		private AudioStream speedBreakDeactivate;
		//Audio players
		[Export]
		private AudioStreamPlayer breakSkillSfx;
		[Export]
		private AudioStreamPlayer heartbeatSfx;

		[Export]
		public float speedBreakSpeed; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsSpeedBreakCharging => IsSpeedBreakActive && !Mathf.IsZeroApprox(breakTimer);
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
			else
			{
				SoundManager.instance.FadeSFX(heartbeatSfx, 80f); //Fade out sfx
				if (breakTimer != 0) return; //Cooldown
			}

			if (Character.Controller.breakButton.wasPressed && !IsSpeedBreakActive)
			{
				if (!IsTimeBreakEnabled) return;
				if (!IsSoulGaugeCharged) return;
				ToggleTimeBreak();
			}
		}

		private void UpdateSpeedBreak()
		{
			if (IsSpeedBreakActive)
			{
				if (Mathf.IsZeroApprox(breakTimer))
				{
					if (breakSkillSfx.Stream != speedBreakActivate) //Play sfx when boost starts
					{
						breakSkillSfx.Stream = speedBreakActivate;
						breakSkillSfx.Play();
					}

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
			else if (!Mathf.IsZeroApprox(breakTimer)) return; //Cooldown

			//Check whether we can start speed break
			if (Character.Controller.boostButton.wasPressed && !IsTimeBreakActive)
			{
				if (!IsSoulGaugeCharged) return;
				if (!IsSpeedBreakEnabled) return;
				if (Character.MovementState == CharacterController.MovementStates.Launcher) return; //Can't speed break during launchers
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
				Character.Effect.PlayVoice("time break");
				BGMPlayer.SetStageMusicVolume(-80f);

				//Reset volume and play
				heartbeatSfx.VolumeDb = 0f;
				heartbeatSfx.Play();
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
				Character.Effect.PlayVoice("speed break");
				Character.CollisionMask = RuntimeConstants.Instance.environmentMask; //Don't collide with any objects
			}
			else
			{
				breakSkillSfx.Stream = speedBreakDeactivate;
				breakSkillSfx.Play();

				Character.MoveSpeed = Character.GroundSettings.speed; //Override speed
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
