using Godot;

namespace Project.Gameplay.Triggers
{
	[Tool]
	public class JumpTrigger : StageTriggerModule
	{
		[Export]
		public NodePath targetNode;
		private Spatial _targetNode;
		[Export]
		public Vector3 targetPosition;
		[Export]
		public float peakHeight;
		private Vector3 TargetPosition => _targetNode != null ? _targetNode.GlobalTranslation : targetPosition;

		public override void _EnterTree()
		{
			if (targetNode != null)
				_targetNode = GetNodeOrNull<Spatial>(targetNode);
		}

		public override void Activate()
		{
			Character.JumpTo(TargetPosition, peakHeight);
			Character.CanJumpDash = false;
		}

		public Launcher.LaunchData GetData()
		{
			if (targetNode != null)
				_targetNode = GetNodeOrNull<Spatial>(targetNode);
			return Launcher.CreateData(GlobalTranslation, TargetPosition, peakHeight);
		}
	}
}
