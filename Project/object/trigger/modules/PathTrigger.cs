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
	/// <summary> Did the previous path limit the camera's distance? </summary>
	private bool deactivateLimitCameraDistance;

	/// <summary> Should the path be assigned to the player? </summary>
	[Export] public bool affectPlayer = true;
	/// <summary> Should the path be assigned to the camera? </summary>
	[Export] public bool affectCamera = true;
	/// <summary> How much should the camera's path transition be smoothed? </summary>
	[Export] private float cameraPathBlendTime = .5f;
	/// <summary> Should this path limit the camera's maximum distance? </summary>
	[Export] public bool limitCameraDistanceToPath;

	public override void Activate()
	{
		if (affectPlayer)
		{
			playerDeactivatePath ??= Player.PathFollower.ActivePath;
			Player.PathFollower.SetActivePath(path);
		}

		if (!affectCamera)
			return;

		cameraDeactivatePath ??= Player.Camera.PathFollower.ActivePath;
		Player.Camera.PathFollower.SetActivePath(path);

		deactivateLimitCameraDistance = Player.Camera.LimitToPathDistance;
		Player.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		Player.Camera.UpdatePathBlendSpeed(Mathf.IsZeroApprox(cameraPathBlendTime) ? 0.0f : 1.0f / cameraPathBlendTime);
	}

	public override void Deactivate()
	{
		//Ensure player's path hasn't already been changed
		if (affectPlayer && Player.PathFollower.ActivePath == path)
			Player.PathFollower.SetActivePath(playerDeactivatePath);

		if (affectCamera && Player.Camera.PathFollower.ActivePath == path)
		{
			Player.Camera.PathFollower.SetActivePath(cameraDeactivatePath);
			Player.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		}
	}
}