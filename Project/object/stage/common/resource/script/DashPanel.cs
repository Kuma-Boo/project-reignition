using Godot;

namespace Project.Gameplay
{
	public class DashPanel : Area
	{
		[Export(PropertyHint.Range, "0, 2")]
		public float speedRatio;
		[Export]
		public float length; //How long for the boost pad to last

		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area _)
		{
			Character.CancelBackflip();
			Character.SetControlLockout(new ControlLockoutResource()
			{
				strafeSettings = ControlLockoutResource.StrafeSettings.KeepPosition,
				speedRatio = speedRatio,
				disableJumping = true,
				tractionRatio = 0,
				length = length,
			});
		}
	}
}
