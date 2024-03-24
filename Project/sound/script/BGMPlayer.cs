using Godot;

/// <summary>
/// Loops an audio stream seamlessly
/// </summary>
namespace Project
{
	[Tool]
	public partial class BGMPlayer : AudioStreamPlayer
	{
		private static BGMPlayer stageMusicInstance;
		public static bool StageMusicPaused
		{
			get => stageMusicInstance == null || stageMusicInstance.StreamPaused;
			set
			{
				if (stageMusicInstance == null) return;
				stageMusicInstance.StreamPaused = value;
			}
		}

		public static void SetStageMusicVolume(float db)
		{
			if (stageMusicInstance != null)
				stageMusicInstance.VolumeDb = db;
		}

		public static void StartStageMusic() // Called when countdown starts to keep things in sync, regardless of load times.
		{
			if (stageMusicInstance != null)
				stageMusicInstance.Play();
		}

		[Export]
		public float loopStartPosition;
		[Export]
		public float loopEndPosition;
		[Export]
		public float debugSeek = -1; // Editor debug. Seeks to the specified point (in seconds)
		[Export]
		public bool isStageMusic;

		private bool canLoop;
		private float LoopLength => loopEndPosition - loopStartPosition;


		public override void _EnterTree()
		{
			canLoop = loopEndPosition > loopStartPosition;
			if (!canLoop)
				GD.PrintErr("BGM loop points are set up incorrectly. Looping is disabled.");

			// Only one stage music can be playing at a time
			if (isStageMusic)
				stageMusicInstance = this;
		}


		public override void _ExitTree()
		{
			if (stageMusicInstance == this) // Unreference
				stageMusicInstance = null;
		}


		public override void _Process(double _)
		{
			if (!canLoop) return;
			if (!Playing) return;

			float currentPosition = GetPlaybackPosition();
			if (currentPosition >= loopEndPosition)
				Seek(currentPosition - LoopLength);

			if (Engine.IsEditorHint() && !Mathf.IsEqualApprox(debugSeek, -1))
			{
				Seek(debugSeek);
				debugSeek = -1;
			}
		}


		public void RestartLoop()
		{
			if (GetPlaybackPosition() >= loopEndPosition)
				Play(loopStartPosition);
		}


		public void Play() => Play(0);
	}
}
