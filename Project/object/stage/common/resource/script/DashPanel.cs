using Godot;

namespace Project.Gameplay
{
	public class DashPanel : Area
	{
		[Export(PropertyHint.Range, "0, 2")]
		public float speedRatio;
		[Export]
		public float length; //How long for the boost pad to last

		private bool isQueued;

		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(float _)
		{
			if (isQueued && Character.IsOnGround)
				Activate();
		}

		public void OnEntered(Area _) => isQueued = true;

		private void Activate()
		{
			isQueued = false;
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
