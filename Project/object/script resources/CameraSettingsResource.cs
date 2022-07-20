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

		[Export]
		public FollowMode followMode;
		public enum FollowMode
		{
			Pathfollower,
			Character,
		}
		[Export]
		public HeightMode heightMode;
		public enum HeightMode
		{
			Character,
			Camera,
			GlobalUp
		}

		[Export]
		public float transitionSpeed = 1f;

		[Export]
		public bool overridePitch;
		[Export]
		public bool overrideYaw;
		[Export]
		public Vector2 viewAngle; //View angle, in degrees

		public void CopyFrom(CameraSettingsResource from) //For runtime camera data
		{
			distance = from.distance;
			height = from.height;
			heightTrackingStrength = from.heightTrackingStrength;

			followMode = from.followMode;
			transitionSpeed = from.transitionSpeed;
		}
	}
}
