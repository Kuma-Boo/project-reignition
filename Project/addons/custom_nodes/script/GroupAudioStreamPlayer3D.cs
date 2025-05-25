using Godot;
using Project.Core;

public partial class GroupAudioStreamPlayer3D : AudioStreamPlayer3D
{
	[Export]
	public StringName groupKey;
	private Callable SignalCallable => Callable.From(() => SoundManager.instance.RemoveGroupSfx(groupKey));

	public void PlayInGroup()
	{
		// Don't play multiple sounds at the same time--prevent sudden volume spikes
		if (!SoundManager.instance.CanPlaySfxInGroup(groupKey))
			return;

		if (Playing)
			SoundManager.instance.RemoveGroupSfx(groupKey);

		if (!IsConnected(SignalName.Finished, SignalCallable))
			Connect(SignalName.Finished, SignalCallable, (uint)ConnectFlags.OneShot);

		MaxDb = SoundManager.instance.AddGroupSfx(groupKey);
		Play();
	}

	public void Play() => Play(0.0f);
}
