using Godot;

namespace Project.Gameplay
{
	public class SFXLibrary : Node2D
	{
		public static SFXLibrary instance;

		public override void _Ready() => instance = this;

		[Export]
		public NodePath ringSoundEffect;
		private AudioStreamPlayer _ringSoundEffect;
		public void PlayRingSoundEffect()
		{
			if (_ringSoundEffect == null)
				_ringSoundEffect = GetNode<AudioStreamPlayer>(ringSoundEffect);

			if (!_ringSoundEffect.Playing || _ringSoundEffect.GetPlaybackPosition() > .12f) //Better sound when picking up rings quickly
				_ringSoundEffect.Play();
		}

		[Export]
		public AudioStream[] pearlStreams;
		[Export]
		public NodePath pearlSoundEffect;
		private AudioStreamPlayer _pearlSoundEffect;
		private int pearlSoundEffectIndex;
		public void ResetPearlSoundEffect() => pearlSoundEffectIndex = 0;
		public void PlayPearlSoundEffect()
		{
			//Might need more work :\
			if (_pearlSoundEffect == null)
				_pearlSoundEffect = GetNode<AudioStreamPlayer>(pearlSoundEffect);

			if (!_pearlSoundEffect.Playing || _pearlSoundEffect.GetPlaybackPosition() > .1f)
			{
				_pearlSoundEffect.Stream = pearlStreams[pearlSoundEffectIndex / 5];
				_pearlSoundEffect.Play();
			}

			if (pearlSoundEffectIndex < (pearlStreams.Length - 1) * 5)
				pearlSoundEffectIndex++;
			else if (!GameplayInterface.instance.IsSoulGaugeFull)
				pearlSoundEffectIndex = 15;
		}


		[Export]
		public NodePath sonicVoice;
		private AudioStreamPlayer _sonicVoice;
	}
}
