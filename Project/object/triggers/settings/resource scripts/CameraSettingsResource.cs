using Godot;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	public class CameraSettingsResource : Resource
	{
		[Export]
		public Modes mode;
		public enum Modes
		{
			Default, //Automatic state machine
			Static, //Single point
			Offset, //Global offset position
			LocalOffset, //Handy for rotated around the stage
		}

		[Export]
		public bool isInstantTransition;
		[Export]
		public bool precisionMode; //Only in DEFAULT camera mode. Better platforming functionality
		[Export]
		public NodePath staticTarget;

		public CameraSettingsResource()
		{
			mode = Modes.Default;
			isInstantTransition = false;
			precisionMode = false;
			staticTarget = null;
		}
	}
}
