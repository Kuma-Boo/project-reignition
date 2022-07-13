using Godot;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	public class CameraSettingsResource : Resource
	{
		[Export]
		public float distance;
		[Export]
		public float height;

		[Export(PropertyHint.Range, "0f, 1f")]
		public float heightTrackingStrength; //How much to follow the player's height
		[Export(PropertyHint.Range, "0, 1")]
		public float heightTrackingSmoothness = 1f;

		[Export]
		public RotationMode rotationMode;
		public enum RotationMode
		{
			FollowPath,
			FollowY,
			Ignore,
		}

		[Export]
		public Vector3 positionOffset; //IMPORTANT offset to the player's center position.
		public bool IsOverridingPosition => !positionOffset.IsEqualApprox(Vector3.Zero);
	}
}
