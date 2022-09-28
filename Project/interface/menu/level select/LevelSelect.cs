using Godot;
using Godot.Collections;

namespace Project.Interface.Menu
{
	public partial class LevelSelect : Menu
	{
		[Export]
		public NodePath video;
		private VideoStreamPlayer _video;
		[Export]
		public Array<VideoStreamTheora> areaDemos;

		protected override void SetUp()
		{
			_video = GetNode<VideoStreamPlayer>(video);
		}

		protected override void ProcessMenu()
		{
			if (!_video.IsPlaying())
				_video.CallDeferred("play");
		}
	}
}
