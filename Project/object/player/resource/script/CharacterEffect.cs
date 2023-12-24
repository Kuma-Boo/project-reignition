using Godot;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing sfx/vfx. Controlled from the CharacterAnimator.
	/// </summary>
	public partial class CharacterEffect : Node3D
	{
		/*
		For some reason, there seem to be a lot of duplicate AudioStreams from the original game.
		Will leave them unused for now.
		*/

		public override void _Ready()
		{
			/*
			//Use this code snippet to figure out the hint key for arrays
			foreach (Dictionary item in GetPropertyList())
			{
				if ((string)item["name"] == "test")
					GD.Print(item);
			}
			*/

			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
		}


		public readonly StringName JUMP_SFX = "jump";

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
		private AnimationPlayer screenShockAnimator;
		[Export]
		private AnimationPlayer timeBreakAnimator;
		/// <summary> VFX for drifting dust. </summary>
		[Export]
		private GpuParticles3D dustParticle;
		public void StartDust() => dustParticle.Emitting = true;
		public void StopDust() => dustParticle.Emitting = false;

		public void StartTrailFX()
		{
			trailFX.IsEmitting = true;
		}

		public void StopTrailFX()
		{
			trailFX.IsEmitting = false;
		}

		public void ScreenShockFX()
		{
			screenShockAnimator.Play("shock");
			screenShockAnimator.Seek(0.0, true);
		}


		public void StartTimeBreak() => timeBreakAnimator.Play("start");
		public void StopTimeBreak() => timeBreakAnimator.Play("stop");

		//Materials (footsteps, landing, etc)
		[ExportGroup("Ground Interactions")]
		[Export]
		private SFXLibraryResource materialSFXLibrary;
		/// <summary> VFX for landing with a dust cloud. </summary>
		[Export]
		private GpuParticles3D landingDustParticle;
		[Export]
		private AudioStreamPlayer footstepChannel;
		[Export]
		private Node3D rightFoot;
		[Export]
		private Node3D leftFoot;
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
				case 6: // Water
					PlaySplashFX();
					break;
				default:
					landingChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 1);
					landingChannel.Play();
					landingDustParticle.Restart();
					break;
			}
		}


		/// <summary>
		/// Plays water splash sfx and vfx.
		/// </summary>
		[Export]
		private PackedScene splashParticle;
		private List<Editor.CustomNodes.GpuParticles3DGroup> splashParticleList = new List<Editor.CustomNodes.GpuParticles3DGroup>();
		public void PlaySplashFX()
		{
			PlayActionSFX("splash");

			Editor.CustomNodes.GpuParticles3DGroup activeSplashParticle = null;
			for (int i = 0; i < splashParticleList.Count; i++)
			{
				if (!splashParticleList[i].IsActive)
				{
					activeSplashParticle = splashParticleList[i];
					break;
				}
			}

			if (activeSplashParticle == null)
				activeSplashParticle = splashParticle.Instantiate<Editor.CustomNodes.GpuParticles3DGroup>();

			StageSettings.instance.AddChild(activeSplashParticle);
			activeSplashParticle.GlobalPosition = GlobalPosition;
			activeSplashParticle.StartParticles();
		}

		public void PlayFootstepFX(bool isRightFoot)
		{
			footstepChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 0);
			footstepChannel.Play();

			Transform3D spawnTransform = isRightFoot ? rightFoot.GlobalTransform : leftFoot.GlobalTransform;
			spawnTransform.Basis = GlobalTransform.Basis;

			switch (groundKeyIndex)
			{
				case 1:
					CreateSandFootFX(spawnTransform); // Check if the ground key is 1 (Corresponds to sand)
					break;
				case 6:
					CreateSplashFootFX(isRightFoot);
					break;
				default: // TODO Create basic dust
					break;
			}

		}


		#region Footsteps and footprints
		[Export]
		private PackedScene footprintDecal;
		private List<Node3D> footprintDecalList = new List<Node3D>();
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
		private Editor.CustomNodes.GpuParticles3DGroup waterStep;
		private void CreateSplashFootFX(bool isRightFoot)
		{
			waterStep.GlobalPosition = isRightFoot ? rightFoot.GlobalPosition : leftFoot.GlobalPosition;

			uint flags = (uint)GpuParticles3D.EmitFlags.Position + (uint)GpuParticles3D.EmitFlags.Velocity;
			waterStep.EmitParticle(waterStep.GlobalTransform, CharacterController.instance.Velocity * .2f, Colors.White, Colors.White, flags);

			//Vector3 splashVelocity = CharacterController.instance.Velocity * .1f + Vector3.Up * 5;
			for (int i = 0; i < 8; i++)
				waterStep.subSystems[0].EmitParticle(Transform3D.Identity, Vector3.Zero, Colors.White, Colors.White, 0);
		}
		#endregion

		public void UpdateGroundType(Node collision)
		{
			//Loop through material keys and see if anything matches
			for (int i = 0; i < materialSFXLibrary.KeyCount; i++)
			{
				if (!collision.IsInGroup(materialSFXLibrary.GetKeyByIndex(i))) continue;

				groundKeyIndex = i;
				return;
			}

			if (groundKeyIndex != 0) //Avoid being spammed with warnings
			{
				GD.PrintErr($"'{collision.Name}' isn't in any sound groups found in CharacterSound.cs.");
				groundKeyIndex = 0; //Default to first key is the default (pavement)
			}
		}

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
