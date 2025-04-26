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
	[Export] public PathMode pathMode;
	public enum PathMode
	{
		Forward,
		Reverse,
		Autodetect
	}
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
		bool reversePath = IsReversePath();
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
		bool reversePath = IsReversePath();
		// Ensure player's path hasn't already been changed
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

	private bool IsReversePath()
	{
		if (pathMode == PathMode.Forward)
			return false;

		if (pathMode == PathMode.Reverse)
			return true;

		// Decide whether this is a reverse path based on the player's forward direction
		// Figure out the "forward" angle by sampling two nearby points on the curve.
		float samplePoint = path.Curve.GetClosestOffset(path.GlobalBasis.Inverse() * (Player.GlobalPosition - path.GlobalPosition));
		float sampleOffsetPoint = samplePoint + path.Curve.BakeInterval * 2f;
		// Calculate the direction of the path at the player's current position
		Vector3 dir = path.Curve.SampleBaked(sampleOffsetPoint) - path.Curve.SampleBaked(samplePoint);
		// Reverse path if the player's current forward direction is different by over 90 degrees
		return ExtensionMethods.DeltaAngleRad(ExtensionMethods.CalculateForwardAngle(dir), Player.MovementAngle) > Mathf.Pi * .5f;
	}
}