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
			_delayTimer = GetNode<Timer>(delayTimer);

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
		[Export]
		public NodePath delayTimer;
		private Timer _delayTimer;
		private int currentDialogIndex;
		private DialogTrigger currentDialog;
		public void PlayDialog (DialogTrigger dialog)
		{
			if (dialog.IsInvalid()) return;

			currentDialog = dialog;
			currentDialogIndex = 0;

			UpdateDialog(true);
		}

		public void CancelDialog()
		{
			_delayTimer.Stop();
			
			if (_dialogChannel.Playing)
			{
				_dialogChannel.Stop();
				_subtitleLabel.Visible = false;
			}
		}
		public void OnDialogDelayComplete() => UpdateDialog(false);

		public void OnDialogFinished()
		{
			currentDialogIndex++;
			if (currentDialogIndex < currentDialog.DialogCount)
				UpdateDialog(true);
			else
				_subtitleLabel.Visible = false;
		}

		private void UpdateDialog(bool processDelay)
		{
			if (processDelay && currentDialog.delays.Count > currentDialogIndex && !Mathf.IsZeroApprox(currentDialog.delays[currentDialogIndex]))
			{
				_delayTimer.Start(currentDialog.delays[currentDialogIndex]);
				return;
			}

			if (SaveManager.UseEnglishVoices)
				_dialogChannel.Stream = currentDialog.englishVoiceClips[currentDialogIndex];
			else
				_dialogChannel.Stream = currentDialog.japaneseVoiceClips[currentDialogIndex];

			_subtitleLabel.Text = Tr(currentDialog.textKeys[currentDialogIndex]);
			_subtitleLabel.Visible = true;
			_dialogChannel.Play();
		}
		#endregion

		#region SFX
		[Export]
		public NodePath ringSoundEffect;
		private AudioStreamPlayer _ringSoundEffect;
		public void PlayRingSoundEffect() => _ringSoundEffect.Play();

		[Export]
		public AudioStream[] pearlStreams;
		[Export]
		public NodePath pearlSoundEffect;
		private AudioStreamPlayer _pearlSoundEffect;
		private int pearlSoundEffectIndex;
		public void ResetPearlSoundEffect() => pearlSoundEffectIndex = 0;
		public void PlayPearlSoundEffect()
		{
			//Needs more work :\
			if (!_pearlSoundEffect.Playing || _pearlSoundEffect.GetPlaybackPosition() > .06f)
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
