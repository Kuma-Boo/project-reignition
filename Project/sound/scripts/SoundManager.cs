using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay
{
	public partial class SoundManager : Node
	{
		public static SoundManager instance;

		public override void _Ready()
		{
			instance = this;

			_subtitleLabel = GetNode<Label>(subtitleLabel);
			_subtitleLetterbox = GetNode<ColorRect>(subtitleLetterbox);
			_subtitleAnimator = GetNode<AnimationPlayer>(subtitleAnimator);
			_dialogChannel = GetNode<AudioStreamPlayer>(dialogChannel);
			_delayTimer = GetNode<Timer>(delayTimer);

			_ringSoundEffect = GetNode<AudioStreamPlayer>(ringSoundEffect);
			_pearlSoundEffect = GetNode<AudioStreamPlayer>(pearlSoundEffect);

			_subtitleAnimator.Play("RESET");
		}

		#region Dialog
		public bool IsDialogActive { get; private set; }
		[Export]
		public NodePath subtitleLabel;
		private Label _subtitleLabel;
		[Export]
		public NodePath subtitleLetterbox;
		private ColorRect _subtitleLetterbox;
		[Export]
		public NodePath subtitleAnimator;
		private AnimationPlayer _subtitleAnimator;
		[Export]
		public NodePath dialogChannel;
		private AudioStreamPlayer _dialogChannel;
		[Export]
		public NodePath delayTimer;
		private Timer _delayTimer;
		private int currentDialogIndex;
		private DialogTrigger currentDialog;
		public void PlayDialog(DialogTrigger dialog)
		{
			if (dialog.IsInvalid()) return;

			IsDialogActive = true;
			_subtitleLabel.Text = string.Empty;

			currentDialog = dialog;
			currentDialogIndex = 0;
			UpdateDialog(true);
		}

		public void CancelDialog()
		{
			if (!IsDialogActive) return;

			_delayTimer.Stop();

			CallDeferred(nameof(DisableDialog));
			if (_dialogChannel.Playing)
			{
				_dialogChannel.Stop();

				if (isSonicSpeaking)
				{
					EmitSignal(nameof(SonicSpeechEndEventHandler));
					isSonicSpeaking = false;
				}
			}
		}
		public void OnDialogDelayComplete() => UpdateDialog(false);

		public void OnDialogFinished()
		{
			currentDialogIndex++;
			if (currentDialogIndex < currentDialog.DialogCount)
			{
				_subtitleAnimator.Play("deactivate-text");
				CallDeferred(nameof(UpdateDialog), true);
			}
			else
				CallDeferred(nameof(DisableDialog));
		}

		private void DisableDialog()
		{
			IsDialogActive = false;
			_subtitleAnimator.Play("deactivate");

			//Disconnect signals
			if (_delayTimer.IsConnected("timeout", new Callable(this, nameof(OnDialogDelayComplete))))
				_delayTimer.Disconnect("timeout", new Callable(this, nameof(OnDialogDelayComplete)));

			if (_delayTimer.IsConnected("timeout", new Callable(this, nameof(OnDialogFinished))))
				_delayTimer.Disconnect("timeout", new Callable(this, nameof(OnDialogFinished)));

			if (_dialogChannel.IsConnected("finished", new Callable(this, nameof(OnDialogFinished))))
				_dialogChannel.Disconnect("finished", new Callable(this, nameof(OnDialogFinished)));
		}

		private void UpdateDialog(bool processDelay)
		{
			UpdateSonicDialog();
			UpdateShahraDialog();

			if (processDelay && currentDialog.HasDelay(currentDialogIndex))
			{
				_delayTimer.Start(currentDialog.delays[currentDialogIndex]);
				_delayTimer.Connect("timeout", new Callable(this, MethodName.OnDialogDelayComplete), (uint)ConnectFlags.OneShot);
				return;
			}

			if (currentDialogIndex == 0)
				_subtitleAnimator.Play("activate");
			else
				_subtitleAnimator.Play("activate-text");

			if (currentDialog.HasAudio(currentDialogIndex)) //Using audio
			{
				if (SaveManager.UseEnglishVoices)
					_dialogChannel.Stream = currentDialog.englishVoiceClips[currentDialogIndex];
				else
					_dialogChannel.Stream = currentDialog.japaneseVoiceClips[currentDialogIndex];

				_subtitleLabel.Text = Tr(currentDialog.textKeys[currentDialogIndex]);
				_dialogChannel.Play();
				if (!currentDialog.HasLength(currentDialogIndex))//Use audio length
				{
					_dialogChannel.Connect("finished", new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
					return;
				}
			}
			else  //Text-only keys
			{
				if (!currentDialog.HasLength(currentDialogIndex)) //Skip
				{
					GD.PrintErr("Text-only dialog doesn't have a specified length. Skipping.");
					OnDialogFinished();
					return;
				}

				_dialogChannel.Stream = null; //Disable dialog channel

				string key = currentDialog.textKeys[currentDialogIndex];
				if (string.IsNullOrEmpty(key) || key.EndsWith("*")) //Cutscene Support - To avoid busywork in editor
					key = currentDialog.textKeys[0].Replace("*", (currentDialogIndex + 1).ToString());
				_subtitleLabel.Text = Tr(key); //Update subtitles
			}

			//If we've made it this far, we're using the custom specified time
			if (!_delayTimer.IsConnected("timeout", new Callable(this, MethodName.OnDialogFinished)))
				_delayTimer.Connect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
			_delayTimer.Start(currentDialog.displayLength[currentDialogIndex]);
		}

		private bool isSonicSpeaking;
		[Signal]
		public delegate void SonicSpeechStartEventHandler();
		[Signal]
		public delegate void SonicSpeechEndEventHandler();
		private const string SONIC_VOICE_SUFFIX = "so"; //Any dialog key that ends with this will be Sonic speaking
		private void UpdateSonicDialog() //Checks whether Sonic is the one speaking, and mutes his gameplay audio.
		{
			bool wasSonicSpeaking = isSonicSpeaking;
			isSonicSpeaking = currentDialog.textKeys[currentDialogIndex].EndsWith(SONIC_VOICE_SUFFIX);
			if (isSonicSpeaking && !wasSonicSpeaking)
				EmitSignal(SignalName.SonicSpeechStart);
			else if (!isSonicSpeaking && wasSonicSpeaking)
				EmitSignal(SignalName.SonicSpeechEnd);
		}

		private bool isShahraSpeaking;
		[Signal]
		public delegate void ShahraSpeechStartEventHandler();
		[Signal]
		public delegate void ShahraSpeechEndEventHandler();
		private const string SHAHRA_VOICE_SUFFIX = "sh"; //Any dialog key that ends with this will be Shahra speaking
		private void UpdateShahraDialog() //Checks whether Shahra is the one speaking, and mutes his gameplay audio.
		{
			bool wasShahraSpeaking = isShahraSpeaking;
			isShahraSpeaking = currentDialog.textKeys[currentDialogIndex].EndsWith(SHAHRA_VOICE_SUFFIX);
			if (isShahraSpeaking && !wasShahraSpeaking)
				EmitSignal(SignalName.ShahraSpeechStart);
			else if (!isShahraSpeaking && wasShahraSpeaking)
				EmitSignal(SignalName.ShahraSpeechEnd);
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
		}
		#endregion
	}
}
