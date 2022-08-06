using Godot;

namespace Project.Gameplay.Triggers
{
	public class CullingTrigger : StageTriggerModule //Also disables nodes.
	{
		[Export]
		public NodePath targetNode;
		private Spatial _targetNode;
		[Export]
		public bool modifyTree; //Modifies the tree instead of just hiding/showing the node. Useful for one way collisions.
		private Node originalParent; //Data for tree modification
		private Transform originalTransform;

		public override void _Ready()
		{
			_targetNode = GetNode<Spatial>(targetNode);

			if (modifyTree)
			{
				originalParent = _targetNode.GetParent();
				originalTransform = _targetNode.GlobalTransform;
			}

			//Start disabled. Use a StageTrigger set to "OnStay" if you need this enabled at the start of the level.
			CallDeferred(nameof(DeactivateNode));
		}

		public override void Activate() => ActivateNode();
		public override void Deactivate(bool isMovingForward) => DeactivateNode();


		public override void _ExitTree()
		{
			if (modifyTree && !_targetNode.IsQueuedForDeletion())
				_targetNode.QueueFree();
		}

		private void ActivateNode()
		{
			if (modifyTree)
			{
				if (_targetNode.IsInsideTree()) return;

				originalParent.CallDeferred("add_child", _targetNode);
				_targetNode.CallDeferred("set_global_transform", originalTransform);
				return;
			}

			_targetNode.Visible = true;
			_targetNode.SetProcess(true);
			_targetNode.SetPhysicsProcess(true);
		}

		private void DeactivateNode()
		{
			if(modifyTree)
			{
				if (!_targetNode.IsInsideTree()) return;

				originalParent.RemoveChild(_targetNode);
				return;
			}

			_targetNode.Visible = false;
			_targetNode.SetProcess(false);
			_targetNode.SetPhysicsProcess(false);
		}
	}
}
