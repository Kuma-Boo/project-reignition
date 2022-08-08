using Godot;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	public class CameraSettingsResource : Resource
	{
		[Export]
		public float distance;
		[Export]
		public FollowMode followMode;
		public enum FollowMode
		{
			Pathfollower,
			Character,
			Static
		}

		[Export]
		public float height;
		[Export(PropertyHint.Range, "0f, 1f")]
		public float heightTrackingStrength; //How much to follow the player's height

		[Export]
		public HeightMode heightMode;
		public enum HeightMode
		{
			PathFollower,
			Camera,
			GlobalUp
		}

		[Export(PropertyHint.Range, "0f, 1f")]
		public float strafeTrackingStrength; //How much to follow the player's strafe
		[Export]
		public StrafeMode strafeMode;
		[Export]
		public float strafeDeadzone;
		public enum StrafeMode
		{
			Rotate,
			Move,
			Disable,
		}

		[Export]
		public TiltMode tiltMode;
		public enum TiltMode
		{
			Disable,
			Path
		}

		[Export]
		public OverrideMode pitchMode;
		[Export]
		public OverrideMode yawMode;
		[Export]
		public Vector2 viewAngle; //View angle, in degrees

		public enum OverrideMode
		{
			Add,
			Override,
		}

		[Export]
		public Vector3 viewPosition;

		public void CopyFrom(CameraSettingsResource from) //For runtime camera data
		{
			distance = from.distance;
			followMode = from.followMode;

			height = from.height;
			heightTrackingStrength = from.heightTrackingStrength;
			heightMode = from.heightMode;

			strafeDeadzone = from.strafeDeadzone;
			strafeTrackingStrength = from.strafeTrackingStrength;
			strafeMode = from.strafeMode;

			tiltMode = from.tiltMode;

			viewPosition = from.viewPosition;
		}

		public bool IsStaticCamera => followMode == FollowMode.Static;
	}
}
