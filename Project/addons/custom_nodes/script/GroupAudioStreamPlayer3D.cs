using Godot;
using Project.Core;

namespace Project.CustomNodes;

public partial class GroupAudioStreamPlayer3D : AudioStreamPlayer3D
{
	[Export] public StringName groupKey;
	[Export] public float audioLengthOverride;
	private Callable SignalCallable => Callable.From(() => SoundManager.instance.RemoveGroupSfx(groupKey));
	private Timer timer;

	public override void _Ready()
	{
		timer = new()
		{
			OneShot = true
		};
		timer.Timeout += () => SoundManager.instance.RemoveGroupSfx(groupKey);
		AddChild(timer);
	}

	public void PlayInGroup()
	{
		// Don't play multiple sounds at the same time--prevent sudden volume spikes
		if (!SoundManager.instance.CanPlaySfxInGroup(groupKey, MaxPolyphony))
			return;

		if (Playing)
			SoundManager.instance.RemoveGroupSfx(groupKey);

		timer.Start(Mathf.IsZeroApprox(audioLengthOverride) ? (float)Stream.GetLength() : audioLengthOverride);
		MaxDb = SoundManager.instance.AddGroupSfx(groupKey);
		Play();
	}

	public void Play() => Play(0.0f);
}
