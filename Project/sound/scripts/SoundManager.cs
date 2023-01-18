using Godot;
using Godot.Collections;
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

			InitializePearlSFX();
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
					isSonicSpeaking = false;
					EmitSignal(SignalName.SonicSpeechEnd);
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
			if (delayTimer.IsConnected("timeout", new Callable(this, MethodName.OnDialogDelayComplete)))
				delayTimer.Disconnect("timeout", new Callable(this, MethodName.OnDialogDelayComplete));

			if (delayTimer.IsConnected("timeout", new Callable(this, MethodName.OnDialogFinished)))
				delayTimer.Disconnect("timeout", new Callable(this, MethodName.OnDialogFinished));

			if (dialogChannel.IsConnected("finished", new Callable(this, MethodName.OnDialogFinished)))
				dialogChannel.Disconnect("finished", new Callable(this, MethodName.OnDialogFinished));
		}

		private void UpdateDialog(bool processDelay)
		{
			if (dialogChannel.IsConnected("finished", new Callable(this, MethodName.OnDialogFinished))) //Must have been interrupted
				dialogChannel.Disconnect("finished", new Callable(this, MethodName.OnDialogFinished));

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

			string key = currentDialog.textKeys[currentDialogIndex];
			AudioStream targetStream = StageSettings.instance.dialogLibrary.GetStream(key, SaveManager.UseEnglishVoices ? 0 : 1);
			if (targetStream != null) //Using audio
			{
				dialogChannel.Stream = targetStream;
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
		/// <summary>
		/// Fade a sound effect player to -80f, then stop the sfx. Returns true if the sfx is still playing.
		/// </summary>
		public bool FadeSFX(AudioStreamPlayer sfx, float fadeSpeed = 40f)
		{
			if (!sfx.Playing) //Already stopped playing
				return false;

			sfx.VolumeDb = Mathf.MoveToward(sfx.VolumeDb, -80f, fadeSpeed * PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(sfx.VolumeDb, -80f))
			{
				sfx.Stop();
				return false;
			}

			return true;
		}

		// Item pickups are played in the SoundManager to avoid volume increase when collecting more than one at a time.
		[Export]
		private AudioStreamPlayer ringSFX;
		public void PlayRingSFX() => ringSFX.Play();
		[Export]
		private AudioStreamPlayer richRingSFX;
		public void PlayRichRingSFX() => richRingSFX.Play();

		[Export]
		private Node pearlSFX;
		private readonly Array<AudioStreamPlayer> pearlSFXList = new Array<AudioStreamPlayer>();
		public int PearlSoundEffectIndex { get; set; }
		[Export]
		private AudioStreamPlayer richPearlSFX;
		[Export]
		private Timer pearlTimer;
		private const float PEARL_AUDIO_DUCK_STRENGTH = .8f;

		private void InitializePearlSFX()
		{
			for (int i = 0; i < pearlSFX.GetChildCount(); i++)
			{
				AudioStreamPlayer audioPlayer = pearlSFX.GetChildOrNull<AudioStreamPlayer>(i);
				if (audioPlayer != null)
					pearlSFXList.Add(audioPlayer);
			}
		}

		public void ResetPearlSFX() => PearlSoundEffectIndex = 0;
		public void PlayPearlSFX()
		{
			pearlSFXList[PearlSoundEffectIndex].Play();
			PearlSoundEffectIndex++;
			if (PearlSoundEffectIndex >= pearlSFXList.Count)
				PearlSoundEffectIndex = pearlSFXList.Count - 1;

			float volume = ((PearlSoundEffectIndex - 1f) / pearlSFXList.Count) * PEARL_AUDIO_DUCK_STRENGTH;
			volume = ExtensionMethods.LinearToDB(1 - volume);

			for (int i = 0; i < pearlSFXList.Count; i++) //Audio Ducking
			{
				pearlSFXList[i].VolumeDb = volume;
			}

			pearlTimer.WaitTime = 3f; //Reset pearl sfx after 3 seconds
			pearlTimer.Start();
		}

		public void PlayRichPearlSFX() => richPearlSFX.Play();
		#endregion
	}
}
