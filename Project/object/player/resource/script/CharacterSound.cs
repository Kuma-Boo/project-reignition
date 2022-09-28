using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	public partial class CharacterSound : Node
	{
		[Export]
		public Array<AudioStream> enClips;
		[Export]
		public Array<AudioStream> jaClips;
		[Export]
		public Array<AudioStream> actionClips;

		[Export]
		public NodePath voiceChannel;
		private AudioStreamPlayer _voiceChannel;
		[Export]
		public Array<NodePath> sfxChannels;

		public override void _Ready()
		{
			_voiceChannel = GetNode<AudioStreamPlayer>(voiceChannel);
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
		}

		public void PlayVoice(int audioIndex)
		{
			_voiceChannel.Stream = SaveManager.UseEnglishVoices ? enClips[audioIndex] : jaClips[audioIndex];
			_voiceChannel.Play();
		}

		private void MuteGameplayVoice() //Kills channel
		{
			_voiceChannel.Stop();
			_voiceChannel.VolumeDb = -80f;
		}

		private void UnmuteGameplayVoice() //Stops any currently active voice clip and resets channel volume
		{
			_voiceChannel.Stop();
			_voiceChannel.VolumeDb = 0f;
		}
	}
}
