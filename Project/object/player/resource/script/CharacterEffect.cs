using Godot;
using System.Collections.Generic;
using Project.Core;
using Project.CustomNodes;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing sfx/vfx. Controlled from the CharacterAnimator.
	/// </summary>
	public partial class CharacterEffect : Node3D
	{
		public override void _Ready()
		{
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
		}

		public readonly StringName JUMP_SFX = "jump";
		public readonly StringName JUMP_DASH_SFX = "jump dash";
		public readonly StringName SLIDE_SFX = "slide";
		public readonly StringName SPLASH_SFX = "splash";
		public readonly StringName SIDLE_SFX = "sidle";
		private readonly StringName STOMP_SFX = "stomp";


		#region Actions
		// Actions (Jumping, sliding, etc)
		[ExportGroup("Actions")]
		[Export]
		private SFXLibraryResource actionSFXLibrary;
		[Export]
		private AudioStreamPlayer actionChannel; //Channel for playing action sound effects
		public void PlayActionSFX(StringName key)
		{
			actionChannel.Stream = actionSFXLibrary.GetStream(key);
			actionChannel.Play();
		}

		[Export]
		public Trail3D trailFX;
		[Export]
		public MeshInstance3D spinFX;
		[Export]
		private AnimationPlayer timeBreakAnimator;
		[Export]
		private AnimationPlayer speedBreakAnimator;
		/// <summary> VFX for drifting dust. </summary>
		[Export]
		private GpuParticles3D dustParticle;
		public void StartDust() => dustParticle.Emitting = true;
		public void StopDust() => dustParticle.Emitting = false;

		public void StartTrailFX() => trailFX.IsEmitting = true;
		public void StopTrailFX() => trailFX.IsEmitting = false;

		public void StartSpinFX() => CreateTween().TweenProperty(spinFX, "transparency", 0.0f, .2f);
		public void StopSpinFX() => CreateTween().TweenProperty(spinFX, "transparency", 1.0f, .2f);

		public void StartTimeBreak() => timeBreakAnimator.Play("start");
		public void StopTimeBreak() => timeBreakAnimator.Play("stop");

		public void StartSpeedBreak() => speedBreakAnimator.Play("start");
		public void StopSpeedBreak() => speedBreakAnimator.Play("stop");


		[Export]
		private GroupGpuParticles3D stompFX;
		public void StartStompFX()
		{
			stompFX.SetEmitting(true);
			PlayActionSFX(STOMP_SFX);

		}
		public void StopStompFX() => stompFX.SetEmitting(false);
		#endregion


		#region Ground
		[ExportGroup("Ground Interactions")]
		// SFX for different ground materials (footsteps, landing, etc)
		[Export]
		private SFXLibraryResource materialSFXLibrary;
		[Export]
		private Node3D rightFoot;
		[Export]
		private Node3D leftFoot;
		[Export]
		private AudioStreamPlayer footstepChannel;
		[Export]
		private AudioStreamPlayer landingChannel;
		/// <summary> Index of the current type of ground the player is walking on. </summary>
		private int groundKeyIndex;

		/// <summary>
		/// Plays landing sfx and vfx based on the current groundKeyIndex.
		/// </summary>
		public void PlayLandingFX()
		{
			switch (groundKeyIndex)
			{
				case 1: // Sand
					PlayLandingSandFX();
					break;
				case 6: // Water
					PlayLandingWaterFX();
					break;
				default:
					PlayLandingDustFX();
					break;
			}
		}


		[ExportSubgroup("Landing VFX")]
		/// <summary> Sand landing VFX. </summary>
		[Export]
		private GpuParticles3D landingSandParticle;
		private void PlayLandingSandFX()
		{
			landingChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 1);
			landingChannel.Play();
			landingSandParticle.Restart();
		}


		/// <summary>
		/// Plays water splash sfx and vfx.
		/// </summary>
		[Export]
		private GroupGpuParticles3D landingWaterParticle;
		public void PlayLandingWaterFX()
		{
			PlayActionSFX(SPLASH_SFX);
			landingWaterParticle.RestartGroup();
		}


		/// <summary> VFX for landing with a dust cloud. </summary>
		[Export]
		private GpuParticles3D landingDustParticle;
		private void PlayLandingDustFX()
		{
			landingChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 1);
			landingChannel.Play();
			landingDustParticle.Restart();
		}


		[ExportSubgroup("Step VFX")]
		[Export]
		/// <summary> Emitters responsible for dust when moving on the ground. </summary>
		private GroupGpuParticles3D[] stepEmitters;
		/// <summary> Index of the current step emitter. </summary>
		private int currentStepEmitter = -1;
		/// <summary> Is step dust be emitted? </summary>
		public bool IsEmittingStepDust
		{
			get => currentStepEmitter != -1;
			set
			{
				if ((value && currentStepEmitter == groundKeyIndex) || (!value && !IsEmittingStepDust)) // Unnecessary assignment; return early
					return;

				// Start by disabling any current emission (if applicable)
				if (IsEmittingStepDust && stepEmitters.Length > currentStepEmitter && stepEmitters[currentStepEmitter] != null)
					stepEmitters[currentStepEmitter].SetEmitting(false);

				if (!value) // Disabling emitters, return early
				{
					currentStepEmitter = -1;
					return;
				}

				currentStepEmitter = groundKeyIndex; // Update current step emitter based on current ground type

				if (stepEmitters.Length - 1 < currentStepEmitter || stepEmitters[currentStepEmitter] == null) // Validate that step emitter exists
					return;

				// Start the emitter
				stepEmitters[currentStepEmitter].SetEmitting(true);
			}
		}


		/// <summary> Plays FXs that occur the moment a foot strikes the ground (i.e. SFX, Footprints, etc.). </summary>
		public void PlayFootstepFX(bool isRightFoot)
		{
			if (Mathf.IsZeroApprox(CharacterController.instance.MoveSpeed)) // Probably called during a blend to idle state; Ignore.
				return;

			footstepChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 0);
			footstepChannel.Play();

			Transform3D spawnTransform = isRightFoot ? rightFoot.GlobalTransform : leftFoot.GlobalTransform;
			spawnTransform.Basis = GlobalTransform.Basis;

			switch (groundKeyIndex)
			{
				case 1: // Sand
					CreateSandFootFX(spawnTransform); // Create a footprint
					break;
				case 6: // Water
					CreateSplashFootFX(isRightFoot); // Create a ripple at the player's foot
					break;
				default:
					break;
			}
		}

		[Export]
		private PackedScene footprintDecal;
		private readonly List<Node3D> footprintDecalList = new();
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
				StageSettings.instance.AddChild(activeFootprintDecal);
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

			uint flags = (uint)GpuParticles3D.EmitFlags.Position + (uint)GpuParticles3D.EmitFlags.Velocity;
			waterStep.EmitParticle(waterStep.GlobalTransform, CharacterController.instance.Velocity * .2f, Colors.White, Colors.White, flags);
		}


		public void UpdateGroundType(Node collision)
		{
			// Loop through material keys and see if anything matches
			for (int i = 0; i < materialSFXLibrary.KeyCount; i++)
			{
				if (!collision.IsInGroup(materialSFXLibrary.GetKeyByIndex(i))) continue;

				groundKeyIndex = i;
				return;
			}

			if (groundKeyIndex != 0) // Avoid being spammed with warnings
			{
				GD.PushWarning($"'{collision.Name}' isn't in any sound groups found in CharacterSound.cs.");
				groundKeyIndex = 0; // Default to first key (pavement)
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
			voiceChannel.Stream = voiceLibrary.GetStream(key, SoundManager.LanguageIndex);
			voiceChannel.Play();
		}

		private void MuteGameplayVoice() //Kills channel
		{
			voiceChannel.Stop();
			voiceChannel.VolumeDb = -80f;
		}

		private void UnmuteGameplayVoice() //Stops any currently active voice clip and resets channel volume
		{
			voiceChannel.Stop();
			voiceChannel.VolumeDb = 0f;
		}
	}
}
