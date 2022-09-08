using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
    /// <summary>
    /// Activates a dialog, with EN/JA/Subtitle support
    /// </summary>
    public class DialogTrigger : StageTriggerModule
    {
        public override void Activate() => SoundManager.instance.PlayDialog(this);

        public int DialogCount => textKeys.Count;
        public bool HasAudio(int index) => englishVoiceClips != null && englishVoiceClips.Count > index && englishVoiceClips[index] != null;
        public bool HasDelay(int index) => delays != null && delays.Count > index && !Mathf.IsZeroApprox(delays[index]);
        public bool HasLength(int index) => displayLength != null && displayLength.Count > index && !Mathf.IsZeroApprox(displayLength[index]);

        [Export(PropertyHint.Range, "0, 10")]
        public Array<float> delays;
        [Export(PropertyHint.Range, "0, 10")]
        public Array<float> displayLength; //Leave at (0) to use the raw audio length
        [Export]
        public Array<string> textKeys;
        [Export]
        public Array<AudioStream> englishVoiceClips;
        [Export]
        public Array<AudioStream> japaneseVoiceClips;

        public bool IsInvalid()
		{
            if (englishVoiceClips != null && englishVoiceClips.Count != japaneseVoiceClips.Count)
			{
                GD.PrintErr($"Dialog trigger {Name} isn't configured properly and cannot be played.");
                return true;
			}

            return false;
		}
    }
}
