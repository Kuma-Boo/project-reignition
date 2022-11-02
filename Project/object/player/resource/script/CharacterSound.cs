using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	public partial class CharacterSound : Node
	{
		[Export]
		private Array<AudioStream> enClips;
		[Export]
		private Array<AudioStream> jaClips;
		[Export]
		private Array<AudioStream> actionClips;
		[Export]
		private Array<AudioStreamPlayer> sfxChannels;

		public override void _Ready()
		{
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
		}

		[Export]
		private AudioStreamPlayer voiceChannel;
		public void PlayVoice(int audioIndex)
		{
			voiceChannel.Stream = SaveManager.UseEnglishVoices ? enClips[audioIndex] : jaClips[audioIndex];
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
