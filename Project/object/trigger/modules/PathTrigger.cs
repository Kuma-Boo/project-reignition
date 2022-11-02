using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Sets the player's active path to <see cref="path"/>
	/// </summary>
	public partial class PathTrigger : StageTriggerModule
	{
		[Export(PropertyHint.NodePathValidTypes, "Path3D")]
		private Path3D path;
		[Export]
		private bool isSideScrollingPath; //Is the target path a 2d sidescroller?
		[Export]
		private bool isFacingRight;
		[Export]
		private bool loopPath;

		public override void Activate()
		{
			Character.PathFollower.SetActivePath(path);
			Character.PathFollower.Loop = loopPath;
			Character.isSideScroller = isSideScrollingPath;
			Character.isFacingRight = isFacingRight;
		}
	}
}
