using Godot;
using System.Collections.Generic;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay;

/// <summary>
/// Responsible for playing sfx/vfx. Controlled from the CharacterAnimator.
/// </summary>
public partial class PlayerEffect : Node3D
{
	private PlayerController Player;
	public void Initialize(PlayerController player)
	{
		Player = player;
		trailFX.Player = Player;

		SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
		SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
	}

	public override void _PhysicsProcess(double _)
	{
		if (isFadingRailSFX)
			isFadingRailSFX = SoundManager.FadeAudioPlayer(grindrailSfx);
	}

	public readonly StringName JumpSfx = "jump";
	public readonly StringName JumpDashSfx = "jump dash";
	public readonly StringName BrakeSfx = "brake";
	public readonly StringName SlideSfx = "slide";
	public readonly StringName SplashSfx = "splash";
	public readonly StringName SidleSfx = "sidle";
	public readonly StringName StompSfx = "stomp";
	public readonly StringName WindSfx = "wind";
	public readonly StringName FireSfx = "fire";
	public readonly StringName DarkSfx = "dark";

	#region Actions
	// Actions (Jumping, sliding, etc)
	[ExportGroup("Skill Effects")]
	[Export]
	private SFXLibraryResource actionSFXLibrary;
	private readonly List<StringName> activeActionChannelKeys = [];
	private readonly List<AudioStreamPlayer> actionChannels = []; // Audio channels for playing action sound effects
	/// <summary> Plays a sound effect given an key (key names are based on actionSFXLibrary). </summary>
	public void PlayActionSFX(StringName key)
	{
		AudioStreamPlayer targetChannel = null;
		AudioStream targetStream = actionSFXLibrary.GetStream(key);

		for (int i = 0; i < actionChannels.Count; i++)
		{
			if (actionChannels[i].Playing &&
				actionChannels[i].Stream != targetStream)
			{
				// Audio channel is already busy playing a different sound effect
				continue;
			}

			targetChannel = actionChannels[i];
			activeActionChannelKeys[i] = key;
		}

		if (targetChannel == null) // Add new target channels as needed
		{
			targetChannel = new()
			{
				VolumeLinear = 0.9f,
				Bus = "GAME SFX"
			};

			GetChild(0).AddChild(targetChannel);
			actionChannels.Add(targetChannel);
			activeActionChannelKeys.Add(key);
		}

		targetChannel.Stream = targetStream;
		targetChannel.Play();
	}

	/// <summary> Stops all action channels with the given key. </summary> 
	public void AbortActionSFX(StringName key)
	{
		while (activeActionChannelKeys.Contains(key))
		{
			int index = activeActionChannelKeys.IndexOf(key);
			if (actionChannels[index].Playing)
				actionChannels[index].Stop();

			activeActionChannelKeys[index] = null;
		}
	}

	[Export]
	public Trail3D trailFX;
	[Export]
	public MeshInstance3D spinFX;
	public void UpdateTrailHueShift(float hueShift)
	{
		(trailFX.material as ShaderMaterial).SetShaderParameter("hue_shift", hueShift);
		(spinFX.MaterialOverride as ShaderMaterial).SetShaderParameter("hue_shift", hueShift);
	}

	[Export]
	private GroupGpuParticles3D teleportParticle;
	public void StartTeleport()
	{
		teleportParticle.RestartGroup();
		PlayActionSFX("teleport start");
	}
	public void StopTeleport()
	{
		teleportParticle.RestartGroup();
		PlayActionSFX("teleport end");
	}

	/// <summary> VFX for drifting dust. </summary>
	[Export]
	private GpuParticles3D dustParticle;
	public void StartDust() => dustParticle.Emitting = true;
	public void StopDust() => dustParticle.Emitting = false;

	public void StartTrailFX() => trailFX.IsEmitting = true;
	public void StopTrailFX() => trailFX.IsEmitting = false;

	public void StartSpinFX() => CreateTween().TweenProperty(spinFX, "transparency", 0.0f, .2f);
	public void StopSpinFX() => CreateTween().TweenProperty(spinFX, "transparency", 1.0f, .2f);

	[Export]
	private GpuParticles3D windParticle;
	public void PlayWindFX()
	{
		windParticle.Restart();
		PlayActionSFX(WindSfx);
	}

	[Export]
	private GpuParticles3D fireParticle;
	public void PlayFireFX()
	{
		fireParticle.Restart();
		PlayActionSFX(FireSfx);
	}

	[Export]
	private GroupGpuParticles3D stompParticle;
	public void StartStompFX()
	{
		stompParticle.SetEmitting(true);
		PlayActionSFX(StompSfx);
	}
	public void StopStompFX() => stompParticle.SetEmitting(false);

	[Export]
	private GroupGpuParticles3D splashJumpParticle;
	public void PlaySplashJumpFX() => splashJumpParticle.RestartGroup();

	[Export]
	private GpuParticles3D quickStepParticle;
	public void PlayQuickStepFX(bool isSteppingRight)
	{
		quickStepParticle.Rotation = isSteppingRight ? Vector3.Zero : Vector3.Up * Mathf.Pi;
		quickStepParticle.Restart();
		PlayActionSFX("quick step");
	}

	[Export]
	private GpuParticles3D lightDashParticle;
	public void StartLightDashFX()
	{
		lightDashParticle.Emitting = true;
		PlayActionSFX("light dash");
	}

	public void StopLightDashFX() => lightDashParticle.Emitting = false;

	[Export]
	private GroupGpuParticles3D aegisSlideParticle;
	public void StartAegisFX() => aegisSlideParticle.SetEmitting(true);
	public void StopAegisFX() => aegisSlideParticle.SetEmitting(false);
	[Export]
	private GroupGpuParticles3D volcanoSlideParticle;
	public void StartVolcanoFX() => volcanoSlideParticle.SetEmitting(true);
	public void StopVolcanoFX() => volcanoSlideParticle.SetEmitting(false);

	[Export]
	private GroupGpuParticles3D soulSlideParticle;
	public void StartSoulSlideFX() => soulSlideParticle.SetEmitting(true);
	public void StopSoulSlideFX() => soulSlideParticle.SetEmitting(false);

	[Export]
	private GpuParticles3D darkSpiralParticle;
	public void PlayDarkSpiralFX()
	{
		darkSpiralParticle.Restart();
		PlayActionSFX(DarkSfx);
	}

	[Export]
	private GroupGpuParticles3D darkCrestParticle;
	public void PlayDarkCrestFX() => darkCrestParticle.RestartGroup();
	[Export]
	private GroupGpuParticles3D windCrestParticle;
	public void PlayWindCrestFX() => windCrestParticle.RestartGroup();

	[Export]
	private GroupGpuParticles3D fireCrestParticle;
	public void PlayFireCrestFX() => fireCrestParticle.RestartGroup();

	[Export]
	private GpuParticles3D chargeParticle;
	[Export]
	private GpuParticles3D fullChargeParticle;
	public void StartChargeFX()
	{
		chargeParticle.Emitting = true;
		chargeParticle.Visible = true;
	}

	public void StopChargeFX()
	{
		chargeParticle.Emitting = false;
		fullChargeParticle.Emitting = false;
	}

	[Export]
	private GpuParticles3D grindrailSparkParticle;
	[Export]
	private GpuParticles3D grindrailBurstParticle;
	[Export]
	private GpuParticles3D perfectShuffleParticle;
	[Export]
	private AudioStreamPlayer grindrailSfx;
	private bool isFadingRailSFX;
	public void StartGrindFX(bool resetSFX)
	{
		grindrailSparkParticle.Emitting = true;
		isFadingRailSFX = false;

		if (resetSFX)
		{
			grindrailSfx.VolumeDb = 0f;
			grindrailSfx.Play();
		}
	}

	public void StartFullChargeFX()
	{
		chargeParticle.Emitting = false;
		chargeParticle.Visible = false;
		fullChargeParticle.Emitting = true;
		grindrailBurstParticle.Restart();
	}

	public void PerfectGrindShuffleFX()
	{
		perfectShuffleParticle.Restart();
		PlayActionSFX("perfect shuffle");
	}

	public void UpdateGrindFX(float speedRatio)
	{
		grindrailSfx.VolumeDb = -9f * Mathf.SmoothStep(0, 1, 1 - speedRatio); // Set sfx volume based on speed
	}

	public void StopGrindFX()
	{
		isFadingRailSFX = true; // Start fading sound effect
		grindrailSparkParticle.Emitting = false;
		StopChargeFX();
	}

	[Export]
	private GpuParticles3D petrifyParticle;
	public void StartPetrifyShatterFX() => petrifyParticle.Restart();
	#endregion

	#region Ground
	[ExportGroup("Material Effects")]
	// SFX for different ground materials (footsteps, landing, etc)
	[Export]
	private SFXLibraryResource materialSFXLibrary;
	private enum MaterialEnum
	{
		Pavement,
		Sand,
		Grass,
		Wood,
		Snow,
		Metal,
		Water,
		Count
	}

	[Export]
	private Node3D rightFoot;
	[Export]
	private Node3D leftFoot;
	[Export]
	private AudioStreamPlayer footstepChannel;
	[Export]
	private AudioStreamPlayer landingChannel;
	/// <summary> Index of the current type of ground the player is walking on. </summary>
	private MaterialEnum groundMaterial;
	private int GroundMaterialIndex => (int)groundMaterial;

	[Export]
	private GpuParticles3D[] landingParticles;
	/// <summary>
	/// Plays landing sfx and vfx based on the current groundKeyIndex.
	/// </summary>
	public void PlayLandingFX()
	{
		if (groundMaterial == MaterialEnum.Water) // Water is a special case because it can be called from a Water DeathTrigger
		{
			PlayLandingWaterFX();
			return;
		}

		// Play landing sfx
		landingChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(GroundMaterialIndex), 1);
		landingChannel.Play();

		if (landingParticles[GroundMaterialIndex] == null) // Unimplemented VFX
			return;

		if (landingParticles[GroundMaterialIndex] is GroupGpuParticles3D)
			(landingParticles[GroundMaterialIndex] as GroupGpuParticles3D).RestartGroup();
		else
			landingParticles[GroundMaterialIndex].Restart();
	}

	/// <summary> Special method to play water splash fx. Also used by Water DeathTriggers. </summary>
	public void PlayLandingWaterFX()
	{
		PlayActionSFX(SplashSfx);
		(landingParticles[(int)MaterialEnum.Water] as GroupGpuParticles3D).RestartGroup();
	}

	[Export]
	/// <summary> Emitters responsible for dust when moving on the ground. </summary>
	private GpuParticles3D[] stepEmitters;
	/// <summary> Index of the current step emitter. </summary>
	private int currentStepEmitter = -1;
	/// <summary> Is step dust be emitted? </summary>
	public bool IsEmittingStepDust
	{
		get => currentStepEmitter != -1;
		set
		{
			if ((value && currentStepEmitter == GroundMaterialIndex) || (!value && !IsEmittingStepDust)) // Unnecessary assignment; return early
				return;

			// Start by disabling any current emission (if applicable)
			if (IsEmittingStepDust && stepEmitters.Length > currentStepEmitter && stepEmitters[currentStepEmitter] != null)
			{
				if (stepEmitters[currentStepEmitter] is GroupGpuParticles3D)
					(stepEmitters[currentStepEmitter] as GroupGpuParticles3D).SetEmitting(false);
				else
					stepEmitters[currentStepEmitter].Emitting = false;
			}

			if (!value) // Disabling emitters, return early
			{
				currentStepEmitter = -1;
				return;
			}

			currentStepEmitter = GroundMaterialIndex; // Update current step emitter based on current ground type

			if (stepEmitters.Length - 1 < currentStepEmitter || stepEmitters[currentStepEmitter] == null) // Validate that step emitter exists
				return;

			// Start the emitter
			if (stepEmitters[currentStepEmitter] is GroupGpuParticles3D)
				(stepEmitters[currentStepEmitter] as GroupGpuParticles3D).SetEmitting(true);
			else
				stepEmitters[currentStepEmitter].Emitting = true;
		}
	}

	/// <summary> Plays FXs that occur the moment a foot strikes the ground (i.e. SFX, Footprints, etc.). </summary>
	public void PlayFootstepFX(bool isRightFoot)
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed)) // Probably called during a blend to idle state; Ignore.
			return;

		footstepChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(GroundMaterialIndex), 0);
		footstepChannel.Play();

		Transform3D spawnTransform = isRightFoot ? rightFoot.GlobalTransform : leftFoot.GlobalTransform;
		spawnTransform.Basis = GlobalTransform.Basis;

		switch (groundMaterial)
		{
			case MaterialEnum.Sand:
				CreateSandFootFX(spawnTransform); // Create a footprint
				break;
			case MaterialEnum.Water:
				CreateSplashFootFX(isRightFoot); // Create a ripple at the player's foot
				break;
		}
	}

	[Export]
	private PackedScene footprintDecal;
	private readonly List<Node3D> footprintDecalList = [];
	private void CreateSandFootFX(Transform3D spawnTransform)
	{
		Node3D activeFootprintDecal = null;
		for (int i = 0; i < footprintDecalList.Count; i++)
		{
			if (footprintDecalList[i].Visible) // Footprint is already active
				continue;

			activeFootprintDecal = footprintDecalList[i]; // Try to reuse decals if possible
		}

		if (activeFootprintDecal == null) // Create new footprint decal
		{
			activeFootprintDecal = footprintDecal.Instantiate<Node3D>();
			footprintDecalList.Add(activeFootprintDecal);
			StageSettings.Instance.AddChild(activeFootprintDecal);
		}

		// Reset fading animation
		AnimationPlayer animator = activeFootprintDecal.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animator != null)
		{
			animator.Seek(0.0);
			animator.Play(animator.Autoplay);
		}
		activeFootprintDecal.GlobalTransform = spawnTransform;
	}

	[Export]
	private GpuParticles3D waterStep;
	private void CreateSplashFootFX(bool isRightFoot)
	{
		waterStep.GlobalPosition = isRightFoot ? rightFoot.GlobalPosition : leftFoot.GlobalPosition;

		const uint flags = (uint)GpuParticles3D.EmitFlags.Position + (uint)GpuParticles3D.EmitFlags.Velocity;
		waterStep.EmitParticle(waterStep.GlobalTransform, Player.Velocity * .2f, Colors.White, Colors.White, flags);
	}

	public void UpdateGroundType(Node collision)
	{
		// Loop through material keys and see if anything matches
		for (int i = 0; i < materialSFXLibrary.KeyCount; i++)
		{
			if (!collision.IsInGroup(materialSFXLibrary.GetKeyByIndex(i))) continue;

			groundMaterial = (MaterialEnum)i;
			return;
		}

		if (groundMaterial != MaterialEnum.Pavement) // Avoid being spammed with warnings
		{
			GD.PushWarning($"'{collision.Name}' isn't in any sound groups found in CharacterSound.cs.");
			groundMaterial = MaterialEnum.Pavement; // Default to pavement
		}
	}
	#endregion

	[ExportGroup("Voices")]
	[Export]
	public SFXLibraryResource voiceLibrary;
	[Export]
	private AudioStreamPlayer voiceChannel;
	public void PlayVoice(StringName key)
	{
		if (SoundManager.instance.IsDialogActive)
			return;

		voiceChannel.Stream = voiceLibrary.GetStream(key, SoundManager.LanguageIndex);
		voiceChannel.Play();
	}

	/// <summary> Stops any currently active voice clip and mutes the voice channel. </summary>
	private void MuteGameplayVoice()
	{
		voiceChannel.Stop();
		voiceChannel.VolumeDb = -80f;
	}

	/// <summary> Stops any currently active voice clip and resets channel volume. </summary>
	private void UnmuteGameplayVoice()
	{
		voiceChannel.Stop();
		voiceChannel.VolumeDb = 0f;
	}
}
