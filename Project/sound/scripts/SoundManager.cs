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
			subtitleAnimator.Play("RESET");
		}

		#region Dialog
		public bool IsDialogActive { get; private set; }
		[Export]
		private Label subtitleLabel;
		[Export]
		private ColorRect subtitleLetterbox;
		[Export]
		private AnimationPlayer subtitleAnimator;
		[Export]
		private AudioStreamPlayer dialogChannel;
		[Export]
		private Timer delayTimer;
		private int currentDialogIndex;
		private DialogTrigger currentDialog;
		public void PlayDialog(DialogTrigger dialog)
		{
			if (dialog.IsInvalid()) return;

			IsDialogActive = true;
			subtitleLabel.Text = string.Empty;

			currentDialog = dialog;
			currentDialogIndex = 0;
			UpdateDialog(true);
		}

		public void CancelDialog()
		{
			if (!IsDialogActive) return;

			delayTimer.Stop();

			CallDeferred(nameof(DisableDialog));
			if (dialogChannel.Playing)
			{
				dialogChannel.Stop();

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
				subtitleAnimator.Play("deactivate-text");
				CallDeferred(nameof(UpdateDialog), true);
			}
			else
				CallDeferred(nameof(DisableDialog));
		}

		private void DisableDialog()
		{
			IsDialogActive = false;
			subtitleAnimator.Play("deactivate");

			//Disconnect signals
			if (delayTimer.IsConnected("timeout", new Callable(this, nameof(OnDialogDelayComplete))))
				delayTimer.Disconnect("timeout", new Callable(this, nameof(OnDialogDelayComplete)));

			if (delayTimer.IsConnected("timeout", new Callable(this, nameof(OnDialogFinished))))
				delayTimer.Disconnect("timeout", new Callable(this, nameof(OnDialogFinished)));

			if (dialogChannel.IsConnected("finished", new Callable(this, nameof(OnDialogFinished))))
				dialogChannel.Disconnect("finished", new Callable(this, nameof(OnDialogFinished)));
		}

		private void UpdateDialog(bool processDelay)
		{
			UpdateSonicDialog();
			UpdateShahraDialog();

			if (processDelay && currentDialog.HasDelay(currentDialogIndex))
			{
				delayTimer.Start(currentDialog.delays[currentDialogIndex]);
				delayTimer.Connect("timeout", new Callable(this, MethodName.OnDialogDelayComplete), (uint)ConnectFlags.OneShot);
				return;
			}

			if (currentDialogIndex == 0)
				subtitleAnimator.Play("activate");
			else
				subtitleAnimator.Play("activate-text");

			if (currentDialog.HasAudio(currentDialogIndex)) //Using audio
			{
				if (SaveManager.UseEnglishVoices)
					dialogChannel.Stream = currentDialog.englishVoiceClips[currentDialogIndex];
				else
					dialogChannel.Stream = currentDialog.japaneseVoiceClips[currentDialogIndex];

				subtitleLabel.Text = Tr(currentDialog.textKeys[currentDialogIndex]);
				dialogChannel.Play();
				if (!currentDialog.HasLength(currentDialogIndex))//Use audio length
				{
					dialogChannel.Connect("finished", new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
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

				dialogChannel.Stream = null; //Disable dialog channel

				string key = currentDialog.textKeys[currentDialogIndex];
				if (string.IsNullOrEmpty(key) || key.EndsWith("*")) //Cutscene Support - To avoid busywork in editor
					key = currentDialog.textKeys[0].Replace("*", (currentDialogIndex + 1).ToString());
				subtitleLabel.Text = Tr(key); //Update subtitles
			}

			//If we've made it this far, we're using the custom specified time
			if (!delayTimer.IsConnected("timeout", new Callable(this, MethodName.OnDialogFinished)))
				delayTimer.Connect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
			delayTimer.Start(currentDialog.displayLength[currentDialogIndex]);
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
		private AudioStreamPlayer ringSoundEffect;
		public void PlayRingSoundEffect() => ringSoundEffect.Play();

		[Export]
		public AudioStream[] pearlStreams;
		[Export]
		private AudioStreamPlayer pearlSoundEffect;
		private int pearlSoundEffectIndex;
		public void ResetPearlSoundEffect() => pearlSoundEffectIndex = 0;
		public void PlayPearlSoundEffect()
		{
			//Needs more work :\
			if (!pearlSoundEffect.Playing || pearlSoundEffect.GetPlaybackPosition() > .06f)
			{
				pearlSoundEffect.Stream = pearlStreams[pearlSoundEffectIndex / 5];
				pearlSoundEffect.Play();
			}

			if (pearlSoundEffectIndex < (pearlStreams.Length - 1) * 5)
				pearlSoundEffectIndex++;
		}
		#endregion
	}
}
