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

		public void Initialize()
		{
			normalCollisionMask = Character.CollisionMask;

			// Determine the size of the soul gauge
			maxSoulPower = SOUL_GAUGE_BASE;
			if (SaveManager.ActiveGameData != null)
			{
				float levelRatio = SaveManager.ActiveGameData.CalculateSoulGaugeLevelRatio(); // Current ratio (0 -> 10) compared to the soul gauge level cap (50)
				maxSoulPower += Mathf.FloorToInt(levelRatio * 10f) * 20; // Soul Gauge size increases by 20 every 5 levels, caps at 300 (level 50).
			}

			SetUpStats();
			SetUpSkills();
		}

		// Cancel time break, just in case
		public override void _ExitTree() => IsTimeBreakEnabled = false;

		#region Stats
		[ExportCategory("Stats")]
		[Export]
		private MovementResource groundSettings; // Default ground settings

		[Export]
		private MovementResource airSettings; // Default air settings
		[Export]
		public float accelerationJumpSpeed;
		[Export]
		public float homingAttackSpeed;
		[Export]
		public float homingAttackAcceleration;

		[Export]
		private MovementResource slideSettings; // Default slide settings

		// While there aren't any upgrades for the following movement skills, they're here for consistancy
		[Export]
		private MovementResource backflipSettings;
		[Export]
		private MovementResource backstepSettings;

		[ExportCategory("Grind Settings")]
		[Export]
		public float perfectShuffleSpeed;
		[Export]
		public MovementResource grindSettings; // Settings for grinding on rails

		[ExportCategory("Sidle Settings")]
		[Export]
		public Curve sidleMovementCurve;

		// References to the actual values being used
		public MovementResource GroundSettings { get; private set; }
		public MovementResource AirSettings { get; private set; }
		public MovementResource BackflipSettings { get; private set; }
		public MovementResource BackstepSettings { get; private set; }
		public MovementResource SlideSettings { get; private set; }

		private void SetUpStats() // Stuff like upgradable speed, increased handling, etc.
		{
			// TODO Interpolate values based on skill ring settings
			GroundSettings = groundSettings;
			AirSettings = airSettings;
			BackflipSettings = backflipSettings;
			BackstepSettings = backstepSettings;

			SlideSettings = slideSettings;
		}


		#endregion


		#region Skills
		private SkillRing SkillRing => SaveManager.ActiveGameData.skillRing;
		public bool IsSkillEnabled(SkillKeyEnum key) => SaveManager.ActiveGameData != null && SkillRing.equippedSkills.Contains(key);


		[ExportCategory("Countdown Skills")]
		[Export]
		public float countdownBoostSpeed;

		/// <summary> How many rings to start with when the level starts. </summary>
		public int StartingRingCount => 0;
		/// <summary> How many rings to start with when respawning. </summary>
		public int RespawnRingCount => 0;

		public void SplashJump()
		{
			GD.Print("Splash Jump isn't implemented yet.");
		}

		/// <summary> Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster. </summary>
		[Export]
		public float landingDashSpeed;

		public bool IsAttacking { get; set; } // Is the player using an attack skill? (i.e Any of the fire skills)

		private void SetUpSkills()
		{
			// Expand hitbox if skills is equipped
			Runtime.Instance.UpdatePearlCollisionShapes(IsSkillEnabled(SkillKeyEnum.PearlCollector) ? 5 : 1);
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
		/// <summary> Is speedbreak currently overriding player's speed? </summary>
		public bool IsSpeedBreakOverrideActive { get; private set; }
		private bool isSpeedBreakEnabled = true;
		private bool isTimeBreakEnabled = true;

		//Audio clips
		[Export]
		private AudioStream speedBreakActivate;
		[Export]
		private AudioStream speedBreakDeactivate;
		//Audio players
		[Export]
		private AudioStreamPlayer speedBreakSFX;
		[Export]
		private AudioStreamPlayer timeBreakSFX;
		[Export]
		private AudioStreamPlayer heartbeatSFX;

		[Export]
		public float speedBreakSpeed; //Movement speed during speed break
		public bool IsTimeBreakActive { get; private set; }
		public bool IsSpeedBreakActive { get; private set; }
		public bool IsSpeedBreakCharging => IsSpeedBreakActive && !Mathf.IsZeroApprox(breakTimer);
		public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;

		private float breakTimer = 0; //Timer for break skills
		private const float SPEEDBREAK_DELAY = 0.4f; //Time to say SPEED BREAK!
		private const float BREAK_SKILLS_COOLDOWN = 1f; //Prevent skill spam
		public const float TIME_BREAK_RATIO = .6f; //Time scale

		public void UpdateSoulSkills()
		{
			if (DebugManager.Instance.InfiniteSoulGauge) //Max out the soul gauge
				ModifySoulGauge(SOUL_GAUGE_MAX);

			UpdateTimeBreak();
			UpdateSpeedBreak();

			breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
		}

		private int timeBreakDrainTimer;
		private const int TIME_BREAK_SOUL_DRAIN_INTERVAL = 3; //Drain 1 point every x frames
		private const float SATURATION_ADJUSTMENT_SPEED = 10.0f;
		private void UpdateTimeBreak()
		{
			//Update timebreak satutration visuals
			float targetSaturation = IsTimeBreakActive ? 0.2f : 1.0f;
			StageSettings.instance.Environment.Environment.AdjustmentSaturation =
				Mathf.MoveToward(StageSettings.instance.Environment.Environment.AdjustmentSaturation, targetSaturation, SATURATION_ADJUSTMENT_SPEED * PhysicsManager.physicsDelta);

			if (IsTimeBreakActive)
			{
				if (timeBreakDrainTimer <= 0)
				{
					ModifySoulGauge(-1);
					timeBreakDrainTimer = TIME_BREAK_SOUL_DRAIN_INTERVAL;
				}
				timeBreakDrainTimer--;

				if (IsSoulGaugeEmpty || !Input.IsActionPressed("button_timebreak")) //Cancel time break?
					ToggleTimeBreak();

				return;
			}
			else
			{
				SoundManager.FadeAudioPlayer(timeBreakSFX, .2f);
				SoundManager.FadeAudioPlayer(heartbeatSFX, .2f); //Fade out sfx
				if (breakTimer != 0) return; //Cooldown
			}

			if (Input.IsActionJustPressed("button_timebreak") && !IsSpeedBreakActive)
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
					if (speedBreakSFX.Stream != speedBreakActivate) //Play sfx when boost starts
					{
						speedBreakSFX.Stream = speedBreakActivate;
						speedBreakSFX.Play();
					}

					ModifySoulGauge(-1); //Drain soul gauge
					if (IsSoulGaugeEmpty || !Input.IsActionPressed("button_speedbreak"))//Check whether we shoudl cancel speed break
						ToggleSpeedBreak();

					if (!IsSpeedBreakOverrideActive && Character.IsOnGround) //Speed is only applied while on the ground
					{
						IsSpeedBreakOverrideActive = true;
						Character.MoveSpeed = speedBreakSpeed;
					}
				}
				else
				{
					Character.MoveSpeed = 0;
					Character.Camera.StartCrossfade(); // Crossfade the screen briefly
				}

				return;
			}
			else if (!Mathf.IsZeroApprox(breakTimer)) return; //Cooldown

			//Check whether we can start speed break
			if (Input.IsActionJustPressed("button_speedbreak") && !IsTimeBreakActive)
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
			SoundManager.IsBreakChannelMuted = IsTimeBreakActive;
			Engine.TimeScale = IsTimeBreakActive ? TIME_BREAK_RATIO : 1f;

			if (IsTimeBreakActive)
			{
				Character.Effect.StartTimeBreak();
				Character.Effect.PlayVoice("time break");
				BGMPlayer.SetStageMusicVolume(-80f);

				//Reset volume and play
				timeBreakSFX.VolumeDb = heartbeatSFX.VolumeDb = 0f;
				timeBreakSFX.Play();
				heartbeatSFX.Play();
			}
			else
			{
				Character.Effect.StopTimeBreak();
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
			SoundManager.IsBreakChannelMuted = IsSpeedBreakActive;
			breakTimer = IsSpeedBreakActive ? SPEEDBREAK_DELAY : BREAK_SKILLS_COOLDOWN;
			IsSpeedBreakOverrideActive = false; //Always disable override

			if (IsSpeedBreakActive)
			{
				Character.MovementAngle = Character.PathFollower.ForwardAngle;
				Character.Effect.PlayVoice("speed break");
				Character.CollisionMask = Runtime.Instance.environmentMask; //Don't collide with any objects
				Character.Animator.SpeedBreak();
				Character.Effect.StartSpeedBreak();
			}
			else
			{
				speedBreakSFX.Stream = speedBreakDeactivate;
				speedBreakSFX.Play();

				Character.MoveSpeed = Character.GroundSettings.speed; //Override speed
				Character.CollisionMask = normalCollisionMask; //Reset collision layer
				Character.Effect.StopSpeedBreak();
			}

			if (HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.UpdateSoulGaugeColor(IsSoulGaugeCharged);
		}

		public int SoulPower { get; private set; } //Current soul power
		private int maxSoulPower; //Calculated on start

		private bool IsSoulGaugeEmpty => !StageSettings.instance.IsControlTest && SoulPower == 0;
		private bool IsSoulGaugeCharged => StageSettings.instance.IsControlTest || SoulPower >= MINIMUM_SOUL_POWER;

		private const int MINIMUM_SOUL_POWER = 50; //Minimum amount of soul power needed to use soul skills.
		private const int SOUL_GAUGE_BASE = 100; //Starting size of soul gauge
		private const int SOUL_GAUGE_MAX = 300; //Max size of soul gauge
		public void ModifySoulGauge(int amount)
		{
			SoulPower = Mathf.Clamp(SoulPower + amount, 0, maxSoulPower);

			if (HeadsUpDisplay.instance != null)
				HeadsUpDisplay.instance.ModifySoulGauge((float)SoulPower / maxSoulPower, IsSoulGaugeCharged);
		}


		/// <summary> Returns a string representing the soul gauge for menus to display. </summary>
		public string TextDisplay => $"{SoulPower}/{maxSoulPower}";
		#endregion
	}
}
