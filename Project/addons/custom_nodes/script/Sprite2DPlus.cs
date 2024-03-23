using Godot;

namespace Project.CustomNodes
{
	[Tool]
	/// <summary>
	/// Provides more control over default animated sprite
	/// </summary>
	public partial class Sprite2DPlus : Sprite2D
	{
		[Export]
		public bool playing;
		[Export]
		public bool paused;
		[Export]
		public LoopMode loopMode;

		public enum LoopMode
		{
			STOP, // Stop after the final frame
			LOOP, // Loop indefinitely
			RETURN // Return to the first frame
		}

		[Export(PropertyHint.Range, "0, 60")]
		public int fps = 30;

		[Export]
		public float timeScale = 1;

		private double timer;

		public override void _Process(double delta)
		{
			if (!playing)
			{
				timer = 0;
				Frame = 0;
				paused = false;

				return;
			}

			if (paused) return;

			timer += delta * timeScale;
			if (timer <= 1.0f / fps) return;

			timer = 0;
			if (Frame >= (Hframes * Vframes) - 1) // Reached end of animation
			{
				switch (loopMode)
				{
					case LoopMode.LOOP: // Loop back to the beginning
						Frame = 0;
						break;
					case LoopMode.RETURN: // Return to the first frame
						Frame = 0;
						playing = false;
						break;
					default: // Stop
						playing = false;
						break;
				}

				return;
			}

			// Advance frame
			Frame++;
		}
	}
}
