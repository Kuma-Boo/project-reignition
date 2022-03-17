using Godot;

//NOTE FOR THE FUTURE. To create a custom resource in c#, create a new resource type in the file system and set the script to this c# script.
namespace Project.Gameplay
{
	public class ControlLockoutResource : Resource
	{
		//Length of the lockout. Use a length of 0 for lockouts to be determined via trigger modes
		[Export(PropertyHint.Range, "0, 10f")]
		public float length;

		//ON -> Regain control when landing on the ground,
		//OFF -> Regain control based on a timer
		[Export]
		public bool resetOnLand;

		//ON -> Defeats any enemy that happens to be in the way.
		//Off -> Allow damage. Note that Being damaged will always re-enable controls
		[Export]
		public bool invincible;

		//ON -> Ignore any jumping inputs
		//TODO add an option for jumping to a point.
		[Export]
		public bool disableJumping;

		[Export]
		public StrafeSettings strafeSettings;
		public enum StrafeSettings
		{
			Default, //Allow strafing movemet
			Keep, //Don't allow strafing. Whatever horizontal position the player entered with is what they're stuck with
			Recenter, //Return to the center of the path
		}

		[Export(PropertyHint.Range, "0, 2f")]
		public float speedRatio; //Ratio compared to character's normal top speed. Character will move to this speed ratio if not set to zero.
		[Export(PropertyHint.Range, "0f, 8f")]
		public float tractionRatio; //Ratio to change speed by. Only applies when speedRatio isn't zero.

		public ControlLockoutResource()
		{
			length = 0;
			speedRatio = 0;
			tractionRatio = 1f;
			resetOnLand = false;
			invincible = false;
			disableJumping = false;
			strafeSettings = StrafeSettings.Default;
		}
	}
}
