using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Hides/Shows nodes.
	/// Use <see cref="modifyTree"/> to enable/disable objects completely. (i.e. one way collisions)
	/// </summary>
	public partial class CullingTrigger : StageTriggerModule
	{
		[Export]
		public NodePath targetNode;
		private Node3D _targetNode;
		[Export]
		public bool modifyTree; //Can cause stuttering for larger objects. Primary use is for one-way collisions
		private Node originalParent; //Data for tree modification
		private Transform3D originalTransform;
		[Export]
		public bool startEnabled; //Generally things should start culled

		public override void _Ready()
		{
			_targetNode = GetNode<Node3D>(targetNode);

			if (modifyTree)
			{
				originalParent = _targetNode.GetParent();
				originalTransform = _targetNode.Transform;
			}

			Respawn();
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public override void _ExitTree()
		{
			if (modifyTree && !_targetNode.IsQueuedForDeletion())
				_targetNode.QueueFree();
		}

		private void Respawn()
		{
			//Disable the node on startup?
			if (!startEnabled)
				CallDeferred(nameof(DeactivateNode));
		}

		private void ActivateNode()
		{
			if (modifyTree)
			{
				if (_targetNode.IsInsideTree()) return;

				originalParent.CallDeferred("add_child", _targetNode);
				_targetNode.CallDeferred("set_transform", originalTransform);
				return;
			}

			_targetNode.Visible = true;
			_targetNode.SetProcess(true);
			_targetNode.SetPhysicsProcess(true);
		}

		private void DeactivateNode()
		{
			if (modifyTree)
			{
				if (!_targetNode.IsInsideTree()) return;

				originalParent.CallDeferred("remove_child", _targetNode);
				return;
			}

			_targetNode.Visible = false;
			_targetNode.SetProcess(false);
			_targetNode.SetPhysicsProcess(false);
		}
	}
}
