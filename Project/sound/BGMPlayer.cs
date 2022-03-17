using Godot;

public class BGMPlayer : AudioStreamPlayer
{
	[Export]
	public float loopStartPosition;
	[Export]
	public float loopEndPosition;
	private float LoopLength => loopEndPosition - loopStartPosition;

	public override void _Ready()
	{
		if (loopEndPosition > loopStartPosition)
			Play();
	}

	public override void _Process(float delta)
	{
		if (!Playing) return;

		float currentPosition = GetPlaybackPosition();
		if (currentPosition >= loopEndPosition)
			Seek(currentPosition - LoopLength);
	}
}
