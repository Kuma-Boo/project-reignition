using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay
{
	public class SoundManager : Control
	{
		public static SoundManager instance;

		public override void _Ready()
		{
			instance = this;

			_subtitleLabel = GetNode<Label>(subtitleLabel);
			_dialogChannel = GetNode<AudioStreamPlayer>(dialogChannel);

			_ringSoundEffect = GetNode<AudioStreamPlayer>(ringSoundEffect);
			_pearlSoundEffect = GetNode<AudioStreamPlayer>(pearlSoundEffect);
		}

		#region Dialog
		[Export]
		public NodePath subtitleLabel;
		private Label _subtitleLabel;
		[Export]
		public NodePath dialogChannel;
		private AudioStreamPlayer _dialogChannel;
		private int currentDialogIndex;
		private DialogTrigger currentDialog;
		public void PlayDialog (DialogTrigger dialog)
		{
			if (dialog.IsInvalid()) return;

			currentDialog = dialog;
			currentDialogIndex = 0;
			UpdateDialog();
			_subtitleLabel.Visible = true;
		}

		public void OnDialogFinished()
		{
			currentDialogIndex++;
			if (currentDialogIndex < currentDialog.DialogCount)
				UpdateDialog();
			else
				_subtitleLabel.Visible = false;
		}

		private void UpdateDialog()
		{
			if (SaveManager.UseEnglishVoices)
				_dialogChannel.Stream = currentDialog.englishVoiceClips[currentDialogIndex];
			else
				_dialogChannel.Stream = currentDialog.japaneseVoiceClips[currentDialogIndex];

			_subtitleLabel.Text = Tr(currentDialog.textKeys[currentDialogIndex]);
			_dialogChannel.Play();
		}
		#endregion

		#region SFX
		[Export]
		public NodePath ringSoundEffect;
		private AudioStreamPlayer _ringSoundEffect;
		public void PlayRingSoundEffect()
		{
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
		#endregion
	}
}
