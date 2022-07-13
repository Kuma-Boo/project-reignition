using Godot;

namespace Project.Gameplay.Triggers
{
	public class PathTrigger : StageTriggerModule
	{
		[Export]
		public NodePath path;
		private Path _path;
		public enum TargetObject
		{
			Player,
			Camera,
			Both
		}
		[Export]
		public TargetObject targetObject;
		[Export]
		public bool isSideScrollingPath;
		[Export]
		public bool isFacingRight;

		public override void _Ready()
		{
			_path = GetNode<Path>(path);
		}

		public override void Activate()
		{
			if (targetObject != TargetObject.Camera)
				Character.SetActivePath(_path);
			if (targetObject != TargetObject.Player)
				CameraController.instance.SetActivePath(_path);

			Character.isSideScroller = isSideScrollingPath;
			Character.isFacingRight = isFacingRight;
		}
	}
}
