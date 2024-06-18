using Godot;
using Godot.Collections;
using Project.Gameplay.Triggers;

namespace Project.Core;

public partial class SoundManager : Node
{
	public static SoundManager instance;
	public static int LanguageIndex => SaveManager.UseEnglishVoices ? 0 : 1;

	public override void _Ready()
	{
		instance = this;
		subtitleAnimator.Play("RESET");

		InitializePearlSFX();

		// Cancel Dialog when switching to a new scene
		TransitionManager.instance.Connect(TransitionManager.SignalName.SceneChanged, new(this, MethodName.CancelDialog));
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
		if (dialog.DialogCount == 0 || DebugManager.Instance.DisableDialog) return; // No dialog

		IsDialogActive = true;
		subtitleLabel.Text = string.Empty;

		// Show background during cutscenes, disable during in-game dialog
		subtitleLetterbox.SelfModulate = dialog.isCutscene ? Colors.White : Colors.Transparent;

		currentDialog = dialog;
		currentDialogIndex = 0;
		if (currentDialog.randomize)
			currentDialogIndex = Runtime.randomNumberGenerator.RandiRange(0, currentDialog.DialogCount);
		UpdateDialog(true);
	}

	public void CancelDialog()
	{
		if (!IsDialogActive) return;

		delayTimer.Stop();
		dialogChannel.Stop();

		if (isSonicSpeaking)
		{
			isSonicSpeaking = false;
			EmitSignal(SignalName.SonicSpeechEnd);
		}

		CallDeferred(MethodName.DisableDialog);
	}
	public void OnDialogDelayComplete() => UpdateDialog(false);

	public void OnDialogFinished()
	{
		currentDialogIndex++;
		if (!currentDialog.randomize && currentDialogIndex < currentDialog.DialogCount) // Start next dialog line
		{
			subtitleAnimator.Play("deactivate-text");
			CallDeferred(MethodName.UpdateDialog, true);
		}
		else
		{
			CallDeferred(MethodName.DisableDialog);
		}
	}

	private void DisableDialog()
	{
		IsDialogActive = false;
		subtitleAnimator.Play("deactivate");

		UpdateSonicDialog();
		UpdateShahraDialog();

		// Disconnect signals
		if (delayTimer.IsConnected(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogDelayComplete)))
			delayTimer.Disconnect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogDelayComplete));

		if (delayTimer.IsConnected(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished)))
			delayTimer.Disconnect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished));

		if (dialogChannel.IsConnected(AudioStreamPlayer.SignalName.Finished, new Callable(this, MethodName.OnDialogFinished)))
			dialogChannel.Disconnect(AudioStreamPlayer.SignalName.Finished, new Callable(this, MethodName.OnDialogFinished));
	}

	private void UpdateDialog(bool processDelay)
	{
		// Must have been interrupted
		if (dialogChannel.IsConnected(AudioStreamPlayer.SignalName.Finished, new Callable(this, MethodName.OnDialogFinished)))
			dialogChannel.Disconnect(AudioStreamPlayer.SignalName.Finished, new Callable(this, MethodName.OnDialogFinished));

		UpdateSonicDialog();
		UpdateShahraDialog();

		if (processDelay && currentDialog.HasDelay(currentDialogIndex)) // Wait for dialog delay (if applicable)
		{
			delayTimer.Start(currentDialog.delays[currentDialogIndex]);
			delayTimer.Connect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogDelayComplete), (uint)ConnectFlags.OneShot);
			return;
		}

		if (currentDialogIndex == 0)
			subtitleAnimator.Play("activate");
		else
			subtitleAnimator.Play("activate-text");

		string key = currentDialog.textKeys[currentDialogIndex];
		AudioStream targetStream = null;
		if (IsInstanceValid(Gameplay.StageSettings.instance))
			targetStream = Gameplay.StageSettings.instance.dialogLibrary.GetStream(key, LanguageIndex);

		if (targetStream != null) // Using audio
		{
			dialogChannel.Stream = targetStream;
			subtitleLabel.Text = Tr(currentDialog.textKeys[currentDialogIndex]);
			dialogChannel.Play();
			if (!currentDialog.HasLength(currentDialogIndex))// Use audio length
			{
				dialogChannel.Connect(AudioStreamPlayer.SignalName.Finished, new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
				return;
			}
		}
		else  // Text-only keys
		{
			if (!currentDialog.HasLength(currentDialogIndex)) // Skip
			{
				GD.PushWarning("Text-only dialog doesn't have a specified length. Skipping.");
				OnDialogFinished();
				return;
			}

			dialogChannel.Stream = null; // Disable dialog channel

			if (string.IsNullOrEmpty(key) || key.EndsWith("*")) // Cutscene Support - To avoid busywork in editor
				key = currentDialog.textKeys[0].Replace("*", (currentDialogIndex + 1).ToString());
			subtitleLabel.Text = Tr(key); // Update subtitles
		}

		// If we've made it this far, we're using the custom specified time
		if (!delayTimer.IsConnected(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished)))
			delayTimer.Connect(Timer.SignalName.Timeout, new Callable(this, MethodName.OnDialogFinished), (uint)ConnectFlags.OneShot);
		delayTimer.Start(currentDialog.displayLength[currentDialogIndex]);
	}

	private bool isSonicSpeaking;
	[Signal]
	public delegate void SonicSpeechStartEventHandler();
	[Signal]
	public delegate void SonicSpeechEndEventHandler();
	private const string SONIC_VOICE_SUFFIX = "so"; // Any dialog key that ends with this will be Sonic speaking
	private void UpdateSonicDialog() // Checks whether Sonic is the one speaking, and mutes his gameplay audio.
	{
		bool wasSonicSpeaking = isSonicSpeaking;
		isSonicSpeaking = IsDialogActive && currentDialog.textKeys[currentDialogIndex].EndsWith(SONIC_VOICE_SUFFIX);
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
	private const string SHAHRA_VOICE_SUFFIX = "sh"; // Any dialog key that ends with this will be Shahra speaking
	private void UpdateShahraDialog() // Checks whether Shahra is the one speaking, and mutes his gameplay audio.
	{
		bool wasShahraSpeaking = isShahraSpeaking;
		isShahraSpeaking = IsDialogActive && currentDialog.textKeys[currentDialogIndex].EndsWith(SHAHRA_VOICE_SUFFIX);
		if (isShahraSpeaking && !wasShahraSpeaking)
			EmitSignal(SignalName.ShahraSpeechStart);
		else if (!isShahraSpeaking && wasShahraSpeaking)
			EmitSignal(SignalName.ShahraSpeechEnd);
	}
	#endregion

	#region SFX
	/// <summary>
	/// Fade a sound to -80f, then stop the audio player. Returns true if audio player is still playing.
	/// </summary>
	public static bool FadeAudioPlayer(AudioStreamPlayer audioPlayer, float fadeTime = 1.0f)
	{
		if (audioPlayer.Playing) // Already stopped playing
		{
			if (Mathf.IsZeroApprox(fadeTime))
			{
				audioPlayer.Stop();
			}
			else
			{
				audioPlayer.VolumeDb = Mathf.MoveToward(audioPlayer.VolumeDb, -80, 80 * (1.0f / fadeTime) * PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(audioPlayer.VolumeDb, -80))
					audioPlayer.Stop();
			}
		}

		return audioPlayer.Playing;
	}

	/// <summary>
	/// Fade a sound to -80f, then stop the audio player. Returns true if audio player is still playing.
	/// </summary>
	public static bool FadeAudioPlayer(AudioStreamPlayer3D audioPlayer, float fadeTime = 1.0f)
	{
		if (audioPlayer.Playing) // Already stopped playing
		{
			if (Mathf.IsZeroApprox(fadeTime))
			{
				audioPlayer.Stop();
			}
			else
			{
				audioPlayer.VolumeDb = Mathf.MoveToward(audioPlayer.VolumeDb, -80, 80 * (1.0f / fadeTime) * PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(audioPlayer.VolumeDb, -80))
					audioPlayer.Stop();
			}
		}

		return audioPlayer.Playing;
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
	private readonly Array<AudioStreamPlayer> pearlSFXList = new();
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

		float volume = (PearlSoundEffectIndex - 1f) / pearlSFXList.Count * PEARL_AUDIO_DUCK_STRENGTH;
		volume = Mathf.LinearToDb(1 - volume);

		for (int i = 0; i < pearlSFXList.Count; i++) // Audio Ducking
		{
			pearlSFXList[i].VolumeDb = volume;
		}

		pearlTimer.WaitTime = 3f; // Reset pearl sfx after 3 seconds
		pearlTimer.Start();
	}

	public void PlayRichPearlSFX() => richPearlSFX.Play();


	private readonly Dictionary<StringName, int> sfxGroups = [];
	public float AddGroupSFX(StringName key)
	{
		if (!sfxGroups.ContainsKey(key))
			sfxGroups.Add(key, 0);
		sfxGroups[key]++;
		return CalculateGroupSFXVolumeDB(key);
	}

	public float RemoveGroupSFX(StringName key)
	{
		if (sfxGroups.TryGetValue(key, out int value))
		{
			sfxGroups[key] = --value;
			if (value < 0)
				sfxGroups.Remove(key);
		}

		return CalculateGroupSFXVolumeDB(key);
	}

	public float CalculateGroupSFXVolumeDB(StringName key)
	{
		if (sfxGroups.TryGetValue(key, out int value)) // Calculate target db volume
			return Mathf.LinearToDb(1.0f / value);

		return 0.0f; // Don't modify db
	}

	/// <summary>
	/// Sets whether the break channel is muted or not (for muting environments)
	/// </summary>
	public static bool IsBreakChannelMuted
	{
		set => AudioServer.SetBusMute(AudioServer.GetBusIndex("BREAK"), value);
	}
	#endregion
}
