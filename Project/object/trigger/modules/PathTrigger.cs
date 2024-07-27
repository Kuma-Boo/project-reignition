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
	private bool deactivateLimitCameraDistance;

	/// <summary> Should the path be assigned to the player? </summary>
	[Export]
	public bool affectPlayer = true;
	/// <summary> Should the path be assigned to the camera? </summary>
	[Export]
	public bool affectCamera = true;
	/// <summary> Should this path limit the camera's maximum distance? </summary>
	[Export]
	public bool limitCameraDistanceToPath;

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
			deactivateLimitCameraDistance = Character.Camera.LimitToPathDistance;

			Character.Camera.PathFollower.SetActivePath(path);
			Character.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		}
	}

	public override void Deactivate()
	{
		//Ensure player's path hasn't already been changed
		if (affectPlayer && Character.PathFollower.ActivePath == path)
			Character.PathFollower.SetActivePath(playerDeactivatePath);

		if (affectCamera && Character.Camera.PathFollower.ActivePath == path)
		{
			Character.Camera.PathFollower.SetActivePath(cameraDeactivatePath);
			Character.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		}
	}
}