using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Sets the player's active path to <see cref="path"/>
/// </summary>
public partial class PathTrigger : StageTriggerModule
{
	[Export(PropertyHint.NodePathValidTypes, "Path3D")]
	private Path3D path;

	private Path3D playerDeactivatePath;
	private Path3D cameraDeactivatePath;

	[Export]
	/// <summary> Should the path be assigned to the player? </summary>
	public bool affectPlayer = true;
	[Export]
	/// <summary> Should the path be assigned to the camera? </summary>
	public bool affectCamera = true;

	public override void Activate()
	{
		if (affectPlayer)
		{
			playerDeactivatePath ??= Character.PathFollower.ActivePath;
			Character.PathFollower.SetActivePath(path);
		}

		if (affectCamera)
		{
			cameraDeactivatePath ??= Character.Camera.PathFollower.ActivePath;
			Character.Camera.PathFollower.SetActivePath(path);
		}
	}

	public override void Deactivate()
	{
		//Ensure player's path hasn't already been changed
		if (affectPlayer && Character.PathFollower.ActivePath == path)
			Character.PathFollower.SetActivePath(playerDeactivatePath);

		if (affectCamera && Character.Camera.PathFollower.ActivePath == path)
			Character.Camera.PathFollower.SetActivePath(cameraDeactivatePath);
	}
}