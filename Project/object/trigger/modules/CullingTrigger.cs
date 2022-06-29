using Godot;

namespace Project.Gameplay
{
	public class CullingTrigger : StageTriggerObject
	{
		/*
		May become obsolete when Godot 4.0 comes out with built-in Occlusion Culling,
		but until then here's a simple trigger system for enabling/disabling objects
		*/

		[Export]
		public bool isExit; //Determines which way is an unload, exiting forward or backwards.
		[Export]
		public bool modifySceneTree; //If true, then unloaded objects will be removed from the tree entirely. Otherwise they're simply hidden.

		[Export]
		public NodePath targetNode;
		private Spatial _targetNode;

		private Node parentNode;
		private Transform spawnTransform;

		public override void _Ready()
		{
			_targetNode = GetNode<Spatial>(targetNode);
			parentNode = _targetNode.GetParent();
			spawnTransform = _targetNode.GlobalTransform;
		}

		public override void Activate()
		{
			//Figure out whether the player is moving backwards and unload.
			//Load();
		}

		private void ActivateNode()
		{
			if (!Visible) return; //Disabled

			if (modifySceneTree)
			{
				if (!_targetNode.IsInsideTree())
					parentNode.AddChild(_targetNode);

				_targetNode.GlobalTransform = spawnTransform;
			}
			else if (!_targetNode.Visible)
				_targetNode.Visible = true;
		}

		private void DeactivateNode()
		{
			if (!Visible) return; //Disabled

			if (modifySceneTree)
			{
				if (IsInsideTree())
					_targetNode.GetParent().CallDeferred("remove_child", this);
			}
			else if (_targetNode.Visible)
				_targetNode.Visible = false;
		}
	}
}
