using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Activates a dialog, with EN/JA/Subtitle support
/// </summary>
public partial class DialogTrigger : StageTriggerModule
{
	[Export]
	public bool isOneShot = true;
	[Export]
	public bool allowRespawn;
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

		isTriggered = isOneShot;
		SoundManager.instance.PlayDialog(this);
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