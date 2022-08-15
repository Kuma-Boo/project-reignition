using Godot;

/// <summary>
/// Loops an audio stream seamlessly
/// </summary>
[Tool]
public class BGMPlayer : AudioStreamPlayer
{
	public static BGMPlayer instance;
	[Export]
	public float loopStartPosition;
	[Export]
	public float loopEndPosition;
	[Export]
	public bool isStageMusic; //Override singleton?
	private bool canLoop;
	private float LoopLength => loopEndPosition - loopStartPosition;

	public override void _EnterTree()
	{
		canLoop = loopEndPosition > loopStartPosition;
		if (!canLoop)
			GD.PrintErr("BGM loop points are set up incorrectly. Looping is disabled.");

		if (isStageMusic)
			instance = this;
	}

	public override void _ExitTree()
	{
		if (instance == this) //Unreference
			instance = null;
	}

	public void Play()
	{
		if (Playing) return; //Already playing

		if (instance != null && instance.Playing)
			if (instance.Stream == Stream) return; //already playing current song

		Play(0f);
		instance = this;
	}

	public override void _Process(float delta)
	{
		if (!canLoop) return;
		if (!Playing) return;

		float currentPosition = GetPlaybackPosition();
		if (currentPosition >= loopEndPosition)
			Seek(currentPosition - LoopLength);
	}

	public void RestartLoop()
	{
		if(GetPlaybackPosition() >= loopEndPosition)
			Play(loopStartPosition);
	}
}
