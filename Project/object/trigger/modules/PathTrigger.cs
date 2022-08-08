using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Sets the player's active path to <see cref="path"/>
	/// </summary>
	public class PathTrigger : StageTriggerModule
	{
		[Export]
		public NodePath path;
		private Path _path;
		[Export]
		public bool isSideScrollingPath; //Is the target path a 2d sidescroller?
		[Export]
		public bool isFacingRight;

		public override void _Ready()
		{
			_path = GetNode<Path>(path);
		}

		public override void Activate()
		{
			Character.PathFollower.SetActivePath(_path);
			Character.isSideScroller = isSideScrollingPath;
			Character.isFacingRight = isFacingRight;
		}
	}
}
