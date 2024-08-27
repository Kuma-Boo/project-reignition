using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Handles the Soul Gauge, Skills, and Stats.
/// </summary>
public partial class PlayerSkillController : Node3D
{
	private CharacterController Character => CharacterController.instance;

	public void Initialize()
	{
		normalCollisionMask = Character.CollisionMask;

		// Determine the size of the soul gauge
		MaxSoulPower = SaveManager.ActiveGameData.CalculateMaxSoulPower();
		timeBreakAnimator.Play("RESET");

		SetUpSkills();
	}

	#region Skills
	private SkillRing SkillRing => SaveManager.ActiveSkillRing;
	public bool IsSkillEquipped(SkillKey key) => SkillRing.IsSkillEquipped(key);
	public int GetAugmentIndex(SkillKey key) => SkillRing.GetAugmentIndex(key);

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
	private bool AllowCrestSkill;
	private void SetUpSkills()
	{
		// Expand hitbox if skills is equipped
		Runtime.Instance.UpdatePearlCollisionShapes(IsSkillEquipped(SkillKey.PearlRange) ? 5 : 1);

		InitializeCrestSkills();
		// Update crest of flame's trail color
		Character.Effect.UpdateTrailHueShift(AllowCrestSkill && SkillRing.IsSkillEquipped(SkillKey.CrestFire) ? CrestOfFlameHueOffset : 0f);
		speedbreakOverlayMaterial.SetShaderParameter(SpeedbreakOverlayOpacityKey, 0);
	}

	private readonly float CrestOfFlameHueOffset = .45f;
	private void InitializeCrestSkills()
	{
		int crestRequirement;
		SkillResource.SkillElement crestType;
		if (SkillRing.IsSkillEquipped(SkillKey.CrestWind))
		{
			crestRequirement = 10;
			crestType = SkillResource.SkillElement.Wind;
		}
		else if (SkillRing.IsSkillEquipped(SkillKey.CrestFire))
		{
			crestRequirement = 8;
			crestType = SkillResource.SkillElement.Fire;
		}
		else if (SkillRing.IsSkillEquipped(SkillKey.CrestDark))
		{
			crestRequirement = 6;
			crestType = SkillResource.SkillElement.Dark;
		}
		else
		{
			// No crest skills equipped
			return;
		}

		foreach (SkillKey key in SkillRing.EquippedSkills)
		{
			if (Runtime.Instance.SkillList.GetSkill(key).Element != crestType)
				continue;

			crestRequirement--;
			if (crestRequirement > 0)
				continue;

			AllowCrestSkill = true;
			break;
		}

		if (!AllowCrestSkill && OS.IsDebugBuild()) // Always allow crest skills when playing the game from the editor
			AllowCrestSkill = true;
	}

	private readonly float WindCrestSpeedMultiplier = 1.5f;
	public void ActivateWindCrest()
	{
		/*
		REFACTOR TODO
		if (!AllowCrestSkill ||
			IsUsingBreakSkills ||
			Character.ActionState != CharacterController.ActionStates.Normal ||
			StageSettings.instance.CurrentRingCount == 0)
		{
			return;
		}

		if (UpdateCrestTimer())
		{
			Character.MoveSpeed = Mathf.Max(Character.MoveSpeed, GroundSettings.Speed * WindCrestSpeedMultiplier);
			StageSettings.instance.UpdateRingCount(1, StageSettings.MathModeEnum.Subtract, true);
			Character.Effect.PlayWindCrestFX();
		}
		*/
	}

	private readonly int DarkCrestSoulAmount = 3;
	public void ActivateDarkCrest()
	{
		if (!AllowCrestSkill || IsUsingBreakSkills)
			return;

		if (UpdateCrestTimer())
		{
			Character.Effect.PlayDarkCrestFX();
			ModifySoulGauge(DarkCrestSoulAmount);
		}
	}

	public void ActivateFireCrest()
	{
		if (!AllowCrestSkill)
			return;

		Character.Effect.PlayFireFX();
		Character.Effect.StartVolcanoFX();
	}

	public void DeactivateFireCrest(bool burst)
	{
		if (!AllowCrestSkill)
			return;

		Character.Effect.StopVolcanoFX();
		if (burst)
		{
			Character.Effect.PlayFireCrestFX();
			Character.AttackState = CharacterController.AttackStates.Weak;
			Character.SetActionState(CharacterController.ActionStates.JumpDash);
			Character.ChangeHitbox("fire-crest");
		}
	}

	private float crestTimer;
	private readonly float CrestInterval = 1.0f;
	private bool UpdateCrestTimer()
	{
		if (Mathf.IsEqualApprox(crestTimer, CrestInterval))
		{
			crestTimer = 0;
			return true;
		}

		crestTimer = Mathf.MoveToward(crestTimer, CrestInterval, PhysicsManager.physicsDelta);
		return false;
	}

	public void ResetCrestTimer() => crestTimer = 0;
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
	public ShaderMaterial speedbreakOverlayMaterial;
	[Export]
	public float speedBreakSpeed; // Movement speed during speed break
	public bool IsTimeBreakActive { get; private set; }
	public bool IsSpeedBreakActive { get; private set; }
	public bool IsSpeedBreakCharging => IsSpeedBreakActive && !Mathf.IsZeroApprox(breakTimer);
	public bool IsUsingBreakSkills => IsTimeBreakActive || IsSpeedBreakActive;

	private float breakTimer; // Timer for break skills
	public const float TimebreakRatio = .6f; // Time scale
	private const float SpeedBreakDelay = 0.4f; // Time to say SPEED BREAK!
	private const float BreakSkillsCooldown = 1f; // Prevent skill spam
	private readonly string SpeedbreakOverlayOpacityKey = "opacity";

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
			if (Character.ActionState == CharacterController.ActionStates.Teleport) return; // Can't time break during teleports
			if (Character.IsDefeated) return;

			ToggleTimeBreak();
		}
	}

	private void UpdateSpeedBreak()
	{
		float currentOpacity = (float)speedbreakOverlayMaterial.GetShaderParameter(SpeedbreakOverlayOpacityKey);
		currentOpacity = Mathf.MoveToward(currentOpacity, IsSpeedBreakActive ? 1 : 0, 5.0f * PhysicsManager.physicsDelta);
		speedbreakOverlayMaterial.SetShaderParameter(SpeedbreakOverlayOpacityKey, currentOpacity);

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
			if (Character.ActionState == CharacterController.ActionStates.Teleport) return; // Can't speed break during teleports
			if (!Character.IsOnGround || Character.IsDefeated) return;

			ToggleSpeedBreak();
		}
	}

	public void ToggleTimeBreak()
	{
		timeBreakDrainTimer = 0;
		IsTimeBreakActive = !IsTimeBreakActive;
		SoundManager.IsBreakChannelMuted = IsTimeBreakActive;
		Engine.TimeScale = IsTimeBreakActive ? TimebreakRatio : 1f;

		if (IsTimeBreakActive)
		{
			timeBreakAnimator.Play("start");
			Character.Effect.PlayVoice("time break");
			Character.Camera.RequestMotionBlur();
			Character.Animator.StartMotionBlur();
			BGMPlayer.SetStageMusicVolume(-80f);

			// Reset volume and play
			timeBreakSFX.VolumeDb = heartbeatSFX.VolumeDb = 0f;
			timeBreakSFX.Play();
			heartbeatSFX.Play();
		}
		else
		{
			timeBreakAnimator.Play("stop");
			Character.Camera.UnrequestMotionBlur();
			Character.Animator.StopMotionBlur();
			breakTimer = BreakSkillsCooldown;
			BGMPlayer.SetStageMusicVolume(0f);
			HeadsUpDisplay.instance?.UpdateSoulGaugeColor(IsSoulGaugeCharged);
		}
	}

	public void ToggleSpeedBreak()
	{
		/*
		REFACTOR TODO
		Character.ResetActionState();

		IsSpeedBreakActive = !IsSpeedBreakActive;
		SoundManager.IsBreakChannelMuted = IsSpeedBreakActive;
		breakTimer = IsSpeedBreakActive ? SpeedBreakDelay : BreakSkillsCooldown;
		IsSpeedBreakOverrideActive = false; // Always disable override

		if (IsSpeedBreakActive)
		{
			speedBreakAnimator.Play(SaveManager.Config.useMotionBlur ? "enable-blur" : "disable-blur");
			speedBreakAnimator.Advance(0.0);

			speedBreakAnimator.Play("start");
			Character.Effect.PlayVoice("speed break");
			Character.MovementAngle = Character.PathFollower.ForwardAngle;
			Character.CollisionMask = Runtime.Instance.environmentMask; // Don't collide with any objects
			Character.Animator.SpeedBreak();
			Character.ChangeHitbox("speed break");
			Character.AttackState = CharacterController.AttackStates.OneShot;
			Character.Camera.RequestMotionBlur();
			Character.Animator.StartMotionBlur();
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
			Character.Camera.UnrequestMotionBlur();
			Character.Animator.StopMotionBlur();
		}

		HeadsUpDisplay.instance?.UpdateSoulGaugeColor(IsSoulGaugeCharged);

		*/
	}

	public void DisableBreakSkills() => IsTimeBreakEnabled = IsSpeedBreakEnabled = false;

	public int SoulPower { get; private set; } // Current soul power
	public int MaxSoulPower { get; private set; } // Calculated on start

	public bool IsSoulGaugeEmpty => !StageSettings.instance.IsControlTest && SoulPower == 0;
	public bool IsSoulGaugeCharged => StageSettings.instance.IsControlTest || SoulPower >= MinimumSoulPower;
	public const int MinimumSoulPower = 50; // Minimum amount of soul power needed to use soul skills.
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