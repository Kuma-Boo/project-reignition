using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Force the player to jump to a point, specified by <see cref="targetNode"/> or <see cref="targetPosition"/>
	/// </summary>
	[Tool]
	public class JumpTrigger : StageTriggerModule
	{
		[Export]
		public NodePath targetNode; //Leaving this empty will use targetPosition exclusively.
		private Spatial _targetNode;
		[Export]
		public Vector3 targetPosition; //Position to jump to. (Added to targetNode's position)
		[Export]
		public float peakHeight; //How high to jump.

		private Vector3 GetTargetPosition()
		{
			Vector3 returnPosition = targetPosition;
			if (_targetNode != null)
				returnPosition += _targetNode.GlobalTranslation;

			return returnPosition;
		}

		public override void _EnterTree()
		{
			if (targetNode != null)
				_targetNode = GetNodeOrNull<Spatial>(targetNode);
		}

		public override void Activate()
		{
			Character.JumpTo(GetTargetPosition(), peakHeight);
			Character.CanJumpDash = false;
		}

		public Launcher.LaunchData GetData()
		{
			if (targetNode != null)
				_targetNode = GetNodeOrNull<Spatial>(targetNode);
			return Launcher.CreateData(GlobalTranslation, GetTargetPosition(), peakHeight);
		}
	}
}
