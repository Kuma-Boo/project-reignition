using Godot;

namespace Project.Gameplay
{
	public class PathTrigger : StageTriggerObject
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

		public override void _Ready()
		{
			_path = GetNode<Path>(path);
		}

		public override void Activate()
		{
			if (targetObject != TargetObject.Camera)
				CharacterController.instance.SetActivePath(_path);
			if (targetObject != TargetObject.Player)
				CameraController.instance.SetActivePath(_path);
		}
	}
}
