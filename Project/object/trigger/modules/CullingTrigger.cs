using Godot;

namespace Project.Gameplay.Triggers
{
	public class CullingTrigger : StageTriggerModule
	{
		[Export]
		public bool isExit; //Determines which way is an unload, exiting forward or backwards.

		[Export]
		public NodePath targetNode;
		private Spatial _targetNode;

		public override void _Ready()
		{
			//Disable target node completely
			_targetNode = GetNode<Spatial>(targetNode);
			DeactivateNode();
		}

		public override void Activate() => ActivateNode();
		public override void Deactivate(bool isMovingForward) => DeactivateNode();

		private void ActivateNode()
		{
			_targetNode.Visible = true;
			_targetNode.SetProcess(true);
			_targetNode.SetPhysicsProcess(true);
		}

		private void DeactivateNode()
		{
			_targetNode.Visible = false;
			_targetNode.SetProcess(false);
			_targetNode.SetPhysicsProcess(false);
		}
	}
}
