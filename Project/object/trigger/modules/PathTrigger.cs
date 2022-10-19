using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Sets the player's active path to <see cref="path"/>
	/// </summary>
	public partial class PathTrigger : StageTriggerModule
	{
		[Export(PropertyHint.NodePathValidTypes, "Path3D")]
		public NodePath path;
		private Path3D _path;
		[Export]
		public bool isSideScrollingPath; //Is the target path a 2d sidescroller?
		[Export]
		public bool isFacingRight;
		[Export]
		public bool loopPath;

		public override void _Ready()
		{
			_path = GetNode<Path3D>(path);
		}

		public override void Activate()
		{
			Character.PathFollower.SetActivePath(_path);
			Character.PathFollower.Loop = loopPath;
			Character.isSideScroller = isSideScrollingPath;
			Character.isFacingRight = isFacingRight;
		}
	}
}
