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
		private Path3D previousPath;

		public override void Activate()
		{
			previousPath = Character.PathFollower.ActivePath;
			Character.PathFollower.CallDeferred(CharacterPathFollower.MethodName.SetActivePath, path);
		}

		public override void Deactivate()
		{
			if (Character.PathFollower.ActivePath != path) return; //Already changed
			Character.PathFollower.CallDeferred(CharacterPathFollower.MethodName.SetActivePath, previousPath);
		}
	}
}
