using Godot;
using System.Collections.Generic;

namespace Project.Gameplay
{
	public class PathSwitcher : StageObject
	{
		[Export]
		public bool oneTime;
		private bool wasTriggered;

		[Export]
		public List<NodePath> pathNodes = new List<NodePath>();
		private List<Path> paths = new List<Path>();

		public override void SetUp()
		{
			wasTriggered = false;
			if (pathNodes.Count == 0)
			{
				GD.Print($"Path switcher {Name} has no paths assigned.");
				return;
			}

			//Get all paths
			for (int i = 0; i < pathNodes.Count; i++)
				paths.Add(GetNode<Path>(pathNodes[i]));
		}

		public override void OnEnter()
		{
			if (wasTriggered)
				return;

			wasTriggered = oneTime;

			if (paths.Count == 1)
				Character.SetActivePath(paths[0]);
		}
		public override void OnStay()
		{
			if (paths.Count <= 1)
				return;

			{
				//TODO Set the player's active path to the closest path
			}

		}

		public override bool IsRespawnable() => oneTime;
	}
}
