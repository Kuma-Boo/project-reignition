using Godot;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Sets the player's active path to <see cref="path"/>
/// </summary>
public partial class PathTrigger : StageTriggerModule
{
	[Export(PropertyHint.NodePathValidTypes, "Path3D")]
	private Path3D path;

	private bool playerPathDeactivateReverse;
	private bool cameraPathDeactivateReverse;
	private Path3D playerDeactivatePath;
	private Path3D cameraDeactivatePath;
	/// <summary> Did the previous path limit the camera's distance? </summary>
	private bool deactivateLimitCameraDistance;

	/// <summary> Should this path be run in reverse? </summary>
	[Export] public bool reversePath;
	/// <summary> Should the path be assigned to the player? </summary>
	[Export] public bool affectPlayer = true;
	/// <summary> Should the path be assigned to the camera? </summary>
	[Export] public bool affectCamera = true;
	/// <summary> How much should the camera's path transition be smoothed? </summary>
	[Export(PropertyHint.Range, "0, 2, 0.1")] private float cameraPathBlendTime;
	/// <summary> Should this path limit the camera's maximum distance? </summary>
	[Export] public bool limitCameraDistanceToPath;

	public override void Activate()
	{
		if (affectPlayer)
		{
			playerDeactivatePath ??= Player.PathFollower.ActivePath;
			playerPathDeactivateReverse = Player.PathFollower.IsReversingPath;
			if (Player.PathFollower.SetActivePath(path, reversePath) &&
				playerPathDeactivateReverse != reversePath &&
				playerDeactivatePath == Player.PathFollower.ActivePath)
			{
				// Only enter reverse path state when reversing the current path
				Player.StartReversePath();
			}
		}

		if (!affectCamera)
			return;

		cameraDeactivatePath ??= Player.Camera.PathFollower.ActivePath;
		cameraPathDeactivateReverse = Player.Camera.PathFollower.IsReversingPath;
		deactivateLimitCameraDistance = Player.Camera.LimitToPathDistance;

		if (!Player.Camera.PathFollower.SetActivePath(path, reversePath))
			return;
		Player.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		Player.Camera.UpdatePathBlendSpeed(Mathf.IsZeroApprox(cameraPathBlendTime) ? 0.0f : 1.0f / cameraPathBlendTime);
	}

	public override void Deactivate()
	{
		//Ensure player's path hasn't already been changed
		if (affectPlayer &&
			Player.PathFollower.ActivePath == path &&
			Player.PathFollower.IsReversingPath == reversePath)
		{
			Player.PathFollower.SetActivePath(playerDeactivatePath, playerPathDeactivateReverse);
		}

		if (affectCamera &&
			Player.Camera.PathFollower.ActivePath == path &&
			Player.Camera.PathFollower.IsReversingPath == cameraPathDeactivateReverse)
		{
			Player.Camera.PathFollower.SetActivePath(cameraDeactivatePath, cameraPathDeactivateReverse);
			Player.Camera.LimitToPathDistance = limitCameraDistanceToPath;
		}
	}
}