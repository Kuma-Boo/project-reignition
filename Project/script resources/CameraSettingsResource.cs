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

		[Export]
		public Vector2 cameraOffset;

		[Export(PropertyHint.Range, "0f, 1f")]
		public float heightTrackingStrength; //How much to follow the player's height

		[Export]
		public RotationMode rotationMode;
		public enum RotationMode
		{
			FollowPath,
			FollowY,
			Ignore,
		}

		[Export]
		public Vector3 constantOffset; //Leave at Zero to follow the active path.
		[Export]
		public bool useCrossfade; //Use a crossfade transition?
	}
}
