using Godot;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing sfx/vfx.
	/// </summary>
	public partial class CharacterEffect : Node
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

		//Actions (Jumping, sliding, etc)
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
		private AudioStreamPlayer landingChannel;
		/// <summary> Index of the current type of ground the player is walking on. </summary>
		private int groundKeyIndex;

		/// <summary>
		/// Plays landing sfx and vfx based on the current groundKeyIndex.
		/// </summary>
		public void PlayLandingFX()
		{
			landingChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 1);
			landingChannel.Play();

			landingDustParticle.Restart();
		}

		public void PlayFootstepSFX()
		{
			footstepChannel.Stream = materialSFXLibrary.GetStream(materialSFXLibrary.GetKeyByIndex(groundKeyIndex), 0);
			footstepChannel.Play();
		}

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
