using Godot;
using Project.Core;

public partial class GroupAudioStreamPlayer3D : AudioStreamPlayer3D
{
	[Export]
	public StringName groupKey;
	private Callable SignalCallable => Callable.From(() => SoundManager.instance.RemoveGroupSFX(groupKey));

	public void PlayInGroup()
	{
		if (Playing)
			SoundManager.instance.RemoveGroupSFX(groupKey);

		if (!IsConnected(SignalName.Finished, SignalCallable))
			Connect(SignalName.Finished, SignalCallable, (uint)ConnectFlags.OneShot);

		MaxDb = SoundManager.instance.AddGroupSFX(groupKey);
		Play();
	}
}
