using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Activates a dialog, with EN/JA/Subtitle support
	/// </summary>
	public partial class DialogTrigger : StageTriggerModule
	{
		public override void Activate() => SoundManager.instance.PlayDialog(this);

		public bool isCutscene;
		public int DialogCount => textKeys.Count;
		public bool HasDelay(int index) => delays != null && delays.Count > index && !Mathf.IsZeroApprox(delays[index]);
		public bool HasLength(int index) => displayLength != null && displayLength.Count > index && !Mathf.IsZeroApprox(displayLength[index]);

		[Export(PropertyHint.Range, "0, 10")]
		public Array<float> delays;
		[Export(PropertyHint.Range, "0, 10")]
		public Array<float> displayLength; //Leave at (0) to use the raw audio length
		[Export]
		public Array<string> textKeys;
	}
}
