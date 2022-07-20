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

		//On -> Reset any action the player may be doing
		[Export]
		public bool resetActionState;

		//ON -> Defeats any enemy that happens to be in the way.
		//Off -> Allow damage.
		[Export]
		public bool invincible;

		//ON -> Ignore any jumping inputs
		[Export]
		public bool disableJumping;

		[Export]
		public StrafeSettings strafeSettings;
		public enum StrafeSettings
		{
			Default, //Allow strafing movemet
			KeepSpeed, //Keeps the player's horizontal speed
			KeepPosition, //Keeps the player's horizontal position
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
