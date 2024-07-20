using Godot;
using Project.Core;

namespace Project.Gameplay;

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
		MaxSoulPower = SaveManager.ActiveGameData.CalculateMaxSoulPower();
		timeBreakAnimator.Play("RESET");
		SetUpStats();
		SetUpSkills();
	}

	#region Stats
	[ExportGroup("Stats")]
	[ExportSubgroup("Ground Settings")]
	// Default ground settings
	[Export]
	private int baseGroundSpeed;
	[Export]
	private int baseGroundTraction;
	[Export]
	private int baseGroundFriction;
	[Export]
	private int baseGroundOverspeed;
	[Export]
	private int baseGroundTurnaround;
	/// <summary> How quickly to turn when moving slowly. </summary>
	[Export]
	public float MinTurnAmount { get; private set; }
	/// <summary> How quickly to turn when moving at top speed. </summary>
	[Export]
	public float MaxTurnAmount { get; private set; }
	/// <summary> How quickly to turnaround when at top speed. </summary>
	[Export]
	public float TurnTurnaround { get; private set; }

	[ExportSubgroup("Air Settings")]
	// Default air settings
	[Export]
	private int baseAirSpeed;
	[Export]
	private int baseAirTraction;
	[Export]
	private int baseAirFriction;
	[Export]
	private int baseAirOverspeed;
	[Export]
	private int baseAirTurnaround;
	[Export]
	public float accelerationJumpSpeed;
	[Export]
	public float homingAttackSpeed;
	[Export]
	public float perfectHomingAttackSpeed;
	[Export]
	public float homingAttackAcceleration;

	[ExportSubgroup("Slide Settings")]
	/// <summary> Slide speed when starting from a standstill. </summary>
	[Export]
	public int InitialSlideSpeed { get; private set; }
	// Default slide settings
	[Export]
	private int baseSlideSpeed;
	[Export]
	private int baseSlideTraction;
	[Export]
	private int baseSlideFriction;
	[Export]
	private int baseSlideOverspeed;
	[Export]
	private int baseSlideTurnaround;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float skillOneFrictionRatio = .5f;
	[Export(PropertyHint.Range, "0,1,.1f")]
	private float slideSlopeSpeedInfluence = .5f;

	/// <summary> Updates slide settings to take slope ratio into account. </summary>
	public void UpdateSlideSpeed(float slopeRatio, float slopeStrength)
	{
		// Calculate top speed
		SlideSettings.Speed = GroundSettings.Speed * (1 - (slopeRatio * slideSlopeSpeedInfluence));
		float slopeInfluence = slopeRatio * slopeStrength;
		float frictionRatio = GetSlidingFrictionRatio();
		SlideSettings.Friction = baseSlideFriction * frictionRatio * (1 + slopeInfluence);
		SlideSettings.Overspeed = baseSlideOverspeed * frictionRatio * (1 + slopeInfluence);
		SlideSettings.Traction = Mathf.Lerp(0, baseSlideTraction, Mathf.Clamp(-slopeInfluence, 0, 1));
	}

	/// <summary> Calculates sliding's friction ratio based on skills. </summary>
	public float GetSlidingFrictionRatio()
	{
		if (IsSkillEquipped(SkillKey.SlideDistance))
			return skillOneFrictionRatio;

		return 1.0f;
	}

	// While there aren't any upgrades for the following movement skills, they're here for consistency
	[ExportGroup("Static Action Settings")]
	[Export]
	private int baseBackflipSpeed;
	[Export]
	private int baseBackflipTraction;
	[Export]
	private int baseBackflipFriction;
	[Export]
	private int baseBackflipOverspeed;
	[Export]
	private int baseBackflipTurnaround;
	[Export]
	private int baseBackstepSpeed;
	[Export]
	private int baseBackstepTraction;
	[Export]
	private int baseBackstepFriction;
	[Export]
	private int baseBackstepOverspeed;
	[Export]
	private int baseBackstepTurnaround;

	[ExportGroup("Grind Settings")]
	[Export]
	public float perfectShuffleSpeed;
	[Export]
	private int baseGrindSpeed;
	[Export]
	private int baseGrindFriction;
	[Export]
	private int baseGrindTurnaround;

	[ExportGroup("Sidle Settings")]
	[Export]
	public Curve sidleMovementCurve;

	// References to the actual movement settings being used
	public MovementSetting GroundSettings { get; private set; }
	public MovementSetting AirSettings { get; private set; }
	public MovementSetting BackflipSettings { get; private set; }
	public MovementSetting BackstepSettings { get; private set; }
	public MovementSetting SlideSettings { get; private set; }
	public MovementSetting GrindSettings { get; private set; }

	private void SetUpStats() // Stuff like upgradable speed, increased handling, etc.
	{
		// TODO Interpolate values based on skill ring settings
		// Create MovementSettings based on skills
		GroundSettings = new()
		{
			Speed = baseGroundSpeed,
			Traction = baseGroundTraction,
			Friction = baseGroundFriction,
			Overspeed = baseGroundOverspeed,
			Turnaround = baseGroundTurnaround,
		};

		AirSettings = new()
		{
			Speed = baseAirSpeed,
			Traction = baseAirTraction,
			Friction = baseAirFriction,
			Overspeed = baseAirOverspeed,
			Turnaround = baseAirTurnaround,
		};

		BackflipSettings = new()
		{
			Speed = baseBackflipSpeed,
			Traction = baseBackflipTraction,
			Friction = baseBackflipFriction,
			Overspeed = baseBackflipOverspeed,
			Turnaround = baseBackflipTurnaround,
		};

		BackstepSettings = new()
		{
			Speed = baseBackstepSpeed,
			Traction = baseBackstepTraction,
			Friction = baseBackstepFriction,
			Overspeed = baseBackstepOverspeed,
			Turnaround = baseBackstepTurnaround,
		};

		float frictionRatio = GetSlidingFrictionRatio();
		SlideSettings = new()
		{
			Speed = baseSlideSpeed,
			Traction = baseSlideTraction,
			Friction = baseSlideFriction * frictionRatio,
			Overspeed = baseSlideOverspeed * frictionRatio,
			Turnaround = baseSlideTurnaround
		};

		GrindSettings = new()
		{
			Speed = baseGrindSpeed,
			Friction = baseGrindFriction,
			Turnaround = baseGrindTurnaround,
		};
	}
	#endregion

	#region Skills
	private SkillRing SkillRing => SaveManager.ActiveSkillRing;
	public bool IsSkillEquipped(SkillKey key) => SkillRing.IsSkillEquipped(key);

	[ExportGroup("Countdown Skills")]
	[Export]
	public float countdownBoostSpeed;

	/// <summary> How many rings to start with when the level starts. </summary>
	public int StartingRingCount => IsSkillEquipped(SkillKey.RingSpawn) ? 5 : 0;
	/// <summary> How many rings to start with when respawning. </summary>
	public int RespawnRingCount => IsSkillEquipped(SkillKey.RingRespawn) ? 5 : 0;

	/// <summary> Minimum speed when landing on the ground and holding forward. Makes Sonic feel faster. </summary>
	[Export]
	public float landingDashSpeed;

	private void SetUpSkills()
	{
		// Expand hitbox if skills is equipped
		Runtime.Instance.UpdatePearlCollisionShapes(IsSkillEquipped(SkillKey.PearlRange) ? 5 : 1);
	}
	#endregion

	#region Soul Skills
	[ExportGroup("Soul Skills")]
	private uint normalCollisionMask;
	public bool IsTimeBreakEnabled
	{
		get => isTimeBreakEnabled;
		set
		{
			isTimeBreakEnabled = value;
			if (IsTimeBreakActive && !isTimeBreakEnabled) // Cancel time break
				ToggleTimeBreak();
		}
	}

	public bool IsSpeedBreakEnabled
	{
		get => isSpeedBreakEnabled;
		set
		{
			isSpeedBreakEnabled = value;
			if (IsSpeedBreakActive && !isSpeedBreakEnabled) // Cancel speed break
				ToggleSpeedBreak();
		}
	}
	/// <summary> Is speedbreak currently overriding player's speed? </summary>
	public bool IsSpeedBreakOverrideActive { get; private set; }
	private bool isSpeedBreakEnabled = true;
	private bool isTimeBreakEnabled = true;

	[Export]
	private AnimationPlayer speedBreakAnimator;
	// Audio clips
	[Export]
	private AudioStream speedBreakActivate;
	[Export]
	private AudioStream speedBreakDeactivate;
	// Audio players
	[Export]
	private AudioStreamPlayer speedBreakSFX;
	[Export]
	private AnimationPlayer timeBreakAnimator;
	[Export]
	private AudioStreamPlayer timeBreakSFX;
	[Export]
	private AudioStreamPlayer heartbeatSFX;

	[Export]
	public float speedBreakSpeed; // Movement speed during speed break
	public bool IsTimeBreakActive { get; private set; }
	public bool IsSpeedBreakActive { get; private set; }
	public bool IsSpeedBreakCharging => IsSpeedBreakActive && !Mathf.IsZeroApprox(breakTimer);
	public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;

	private float breakTimer; // Timer for break skills
	private const float SPEEDBREAK_DELAY = 0.4f; // Time to say SPEED BREAK!
	private const float BREAK_SKILLS_COOLDOWN = 1f; // Prevent skill spam
	public const float TIME_BREAK_RATIO = .6f; // Time scale

	public void UpdateSoulSkills()
	{
		if (DebugManager.Instance.InfiniteSoulGauge) // Max out the soul gauge
			ModifySoulGauge(MaxSoulPower);

		UpdateTimeBreak();
		UpdateSpeedBreak();

		breakTimer = Mathf.MoveToward(breakTimer, 0, PhysicsManager.physicsDelta);
	}

	private int timeBreakDrainTimer;
	private const int TimeBreakSoulDrainInterval = 3; // Drain 1 point every x frames
	private void UpdateTimeBreak()
	{
		if (IsTimeBreakActive)
		{
			if (timeBreakDrainTimer <= 0)
			{
				ModifySoulGauge(-1);
				timeBreakDrainTimer = TimeBreakSoulDrainInterval;
			}
			timeBreakDrainTimer--;

			if (IsSoulGaugeEmpty || !Input.IsActionPressed("button_timebreak")) // Cancel time break?
				ToggleTimeBreak();

			return;
		}
		else
		{
			SoundManager.FadeAudioPlayer(timeBreakSFX, .2f);
			SoundManager.FadeAudioPlayer(heartbeatSFX, .2f); // Fade out sfx
			if (breakTimer != 0) return; // Cooldown
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
				if (speedBreakSFX.Stream != speedBreakActivate) // Play sfx when boost starts
				{
					speedBreakSFX.Stream = speedBreakActivate;
					speedBreakSFX.Play();
					ModifySoulGauge(-15); // Instantly lose a bunch of soul power
				}

				ModifySoulGauge(-1); // Drain soul gauge
				if (IsSoulGaugeEmpty || !Input.IsActionPressed("button_speedbreak"))// Check whether we shoudl cancel speed break
					ToggleSpeedBreak();

				if (!IsSpeedBreakOverrideActive && Character.IsOnGround) // Speed is only applied while on the ground
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
		else if (!Mathf.IsZeroApprox(breakTimer))
		{
			return; // Cooldown
		}

		// Check whether we can start speed break
		if (Input.IsActionJustPressed("button_speedbreak") && !IsTimeBreakActive)
		{
			if (!IsSoulGaugeCharged) return;
			if (!IsSpeedBreakEnabled) return;
			if (Character.MovementState == CharacterController.MovementStates.Launcher) return; // Can't speed break during launchers
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
			timeBreakAnimator.Play("start");
			Character.Effect.PlayVoice("time break");
			BGMPlayer.SetStageMusicVolume(-80f);

			// Reset volume and play
			timeBreakSFX.VolumeDb = heartbeatSFX.VolumeDb = 0f;
			timeBreakSFX.Play();
			heartbeatSFX.Play();
		}
		else
		{
			timeBreakAnimator.Play("stop");
			breakTimer = BREAK_SKILLS_COOLDOWN;
			BGMPlayer.SetStageMusicVolume(0f);

			HeadsUpDisplay.instance?.UpdateSoulGaugeColor(IsSoulGaugeCharged);
		}
	}

	public void ToggleSpeedBreak()
	{
		Character.ResetActionState();

		IsSpeedBreakActive = !IsSpeedBreakActive;
		SoundManager.IsBreakChannelMuted = IsSpeedBreakActive;
		breakTimer = IsSpeedBreakActive ? SPEEDBREAK_DELAY : BREAK_SKILLS_COOLDOWN;
		IsSpeedBreakOverrideActive = false; // Always disable override

		if (IsSpeedBreakActive)
		{
			speedBreakAnimator.Play("start");
			Character.Effect.PlayVoice("speed break");
			Character.MovementAngle = Character.PathFollower.ForwardAngle;
			Character.CollisionMask = Runtime.Instance.environmentMask; // Don't collide with any objects
			Character.Animator.SpeedBreak();
			Character.ChangeHitbox("speed break");
			Character.AttackState = CharacterController.AttackStates.OneShot;
		}
		else
		{
			speedBreakAnimator.Play("stop");
			speedBreakSFX.Stream = speedBreakDeactivate;
			speedBreakSFX.Play();

			Character.MoveSpeed = GroundSettings.Speed; // Override speed
			Character.CollisionMask = normalCollisionMask; // Reset collision layer
			Character.AttackState = CharacterController.AttackStates.None;
			Character.ChangeHitbox("RESET");
		}

		HeadsUpDisplay.instance?.UpdateSoulGaugeColor(IsSoulGaugeCharged);
	}

	public void DisableBreakSkills() => IsTimeBreakEnabled = IsSpeedBreakEnabled = false;

	public int SoulPower { get; private set; } // Current soul power
	public int MaxSoulPower { get; private set; } // Calculated on start

	private bool IsSoulGaugeEmpty => !StageSettings.instance.IsControlTest && SoulPower == 0;
	private bool IsSoulGaugeCharged => StageSettings.instance.IsControlTest || SoulPower >= MinimumSoulPower;

	private const int MinimumSoulPower = 50; // Minimum amount of soul power needed to use soul skills.
	public void ModifySoulGauge(int amount)
	{
		SoulPower = Mathf.Clamp(SoulPower + amount, 0, MaxSoulPower);
		float ratio;
		if (SoulPower < MinimumSoulPower)
			ratio = SoulPower / (float)MinimumSoulPower;
		else
			ratio = (SoulPower - MinimumSoulPower) / ((float)MaxSoulPower - MinimumSoulPower);

		HeadsUpDisplay.instance?.ModifySoulGauge(ratio, IsSoulGaugeCharged);
	}

	/// <summary> Returns a string representing the soul gauge for menus to display. </summary>
	public string TextDisplay => $"{SoulPower}/{MaxSoulPower}";
	#endregion
}

/// <summary>
/// Contains data of movement settings. Leave values at -1 to ignore (primarily for skill overrides)
/// </summary>
public class MovementSetting
{
	public float Speed { get; set; }
	public float Traction { get; set; } // Speed up rate
	public float Friction { get; set; } // Slow down rate
	public float Overspeed { get; set; } // Slow down rate when going faster than speed
	public float Turnaround { get; set; } // Skidding

	public MovementSetting()
	{
		Speed = 0;
		Traction = 0;
		Friction = 0;
	}

	/// <summary> Interpolates between speeds based on input. </summary>
	public float UpdateInterpolate(float currentSpeed, float input)
	{
		float delta = Traction;
		float targetSpeed = Speed * input;
		targetSpeed = Mathf.Max(targetSpeed, 0);

		if (Mathf.Abs(currentSpeed) > Speed)
			delta = Overspeed;

		if (input == 0) // Deccelerate
			delta = Friction;
		else if (!Mathf.IsZeroApprox(currentSpeed) && Mathf.Sign(targetSpeed) != Mathf.Sign(Speed)) // Turnaround
			delta = Turnaround;

		return Mathf.MoveToward(currentSpeed, targetSpeed, delta * PhysicsManager.physicsDelta);
	}

	/// <summary> Special addition mode for Sliding. Does NOT support negative speeds. </summary>
	public float UpdateSlide(float currentSpeed, float input)
	{
		bool clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		if (Mathf.Abs(currentSpeed) > Speed) // Reduce by overspeed
		{
			currentSpeed -= Overspeed * PhysicsManager.physicsDelta;
			if (Mathf.Abs(currentSpeed) > Speed && (Mathf.IsZeroApprox(input) || input > 0)) // Allow overspeed sliding
				return currentSpeed;
		}

		if (input > 0) // Accelerate
		{
			if (!clampFinalSpeed)
				currentSpeed = Speed;
			else
				currentSpeed += Traction * input * PhysicsManager.physicsDelta;
		}
		else
		{
			// Deccelerate and Turnaround
			currentSpeed -= Mathf.Lerp(Friction, Turnaround, Mathf.Abs(input)) * PhysicsManager.physicsDelta;
			clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		}

		if (clampFinalSpeed)
			currentSpeed = Mathf.Clamp(currentSpeed, 0, Speed);

		return currentSpeed;
	}

	public float GetSpeedRatio(float spd) => spd / Speed;
	public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), -1f, 1f);
}