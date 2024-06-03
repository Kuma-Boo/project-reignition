using Godot;

namespace Project.Interface.Menus
{
	public partial class VideoStreamFileLoadPlayer : VideoStreamPlayer
	{
		[Export(PropertyHint.File)]
		private string videoFilePath;

		public override void _Ready()
		{
			if (!ResourceLoader.Exists(videoFilePath, "VideoStreamTheora"))
			{
				GD.PushWarning($"Couldn't load video file {videoFilePath}!");
				return;
			}

			Stream = ResourceLoader.Load<VideoStreamTheora>(videoFilePath, "VideoStreamTheora");
		}
	}
}
