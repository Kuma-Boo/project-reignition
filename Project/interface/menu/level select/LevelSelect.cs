using Godot;
using Godot.Collections;

namespace Project.Interface.Menu
{
	public class LevelSelect : Menu
	{
		[Export]
		public NodePath video;
		private VideoPlayer _video;
		[Export]
		public Array<VideoStreamTheora> areaDemos = new Array<VideoStreamTheora>();

		protected override void SetUp()
		{
			_video = GetNode<VideoPlayer>(video);
		}

		protected override void ProcessMenu()
		{
			if (!_video.IsPlaying())
				_video.CallDeferred("play");
		}
	}
}
