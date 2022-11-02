using Godot;
using Godot.Collections;

namespace Project.Interface.Menu
{
	public partial class LevelSelect : Menu
	{
		[Export]
		private VideoStreamPlayer video;
		[Export]
		private Array<VideoStreamTheora> areaDemos;

		protected override void ProcessMenu()
		{
			if (!video.IsPlaying())
				video.CallDeferred("play");
		}
	}
}
