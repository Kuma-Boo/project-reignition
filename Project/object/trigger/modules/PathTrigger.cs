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
		private bool loopPath;

		public override void Activate()
		{
			Character.PathFollower.SetActivePath(path);
			Character.PathFollower.Loop = loopPath;
		}
	}
}
