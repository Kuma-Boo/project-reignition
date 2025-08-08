using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Activates a dialog, with EN/JA/Subtitle support
/// </summary>
public partial class DialogTrigger : StageTriggerModule
{
	[Export] public bool isOneShot = true;
	[Export] public bool allowRespawn;

	[Export] private PlaybackMode playbackType;
	private enum PlaybackMode
	{
		Queue, // Queue dialog to play after the current dialog is finished
		Always, // Always play dialog immediately
		NoDialog, // Only play dialog when nothing else is playing
	}

	private bool isTriggered;

	public override void Respawn()
	{
		if (allowRespawn)
			isTriggered = false;
	}

	public override void Activate()
	{
		if (isTriggered)
			return;

		if (playbackType == PlaybackMode.NoDialog && SoundManager.instance.IsDialogActive)
			return;

		isTriggered = isOneShot;

		if (playbackType == PlaybackMode.Always || !SoundManager.instance.IsDialogActive)
		{
			SoundManager.instance.PlayDialog(this);
			return;
		}

		SoundManager.instance.QueueDialog(this);
	}

	public override void Deactivate() => SoundManager.instance.CancelDialog();

	public bool IsCutscene { get; set; }
	public int DialogCount => textKeys.Count;
	public bool HasDelay(int index) => delays != null && delays.Count > index && !Mathf.IsZeroApprox(delays[index]);
	public bool HasLength(int index) => displayLength != null && displayLength.Count > index && !Mathf.IsZeroApprox(displayLength[index]);

	[Export]
	public bool randomize;
	[Export(PropertyHint.Range, "0, 10")]
	public Array<float> delays;
	[Export(PropertyHint.Range, "0, 10")]
	public Array<float> displayLength; //Leave at (0) to use the raw audio length
	[Export]
	public Array<string> textKeys;
}