using Godot;

namespace Project.Gameplay
{
	public class BoostPad : StageObject
	{
		[Export]
		public float speedRatio;
		[Export]
		public float length; //How long for the boost pad to last

		public override bool IsRespawnable() => false;

		public override void OnEnter()
		{
			Character.SetControlLockout(new ControlLockoutResource()
			{
				speedRatio = speedRatio,
				disableJumping = true,
				length = length,
			});
		}
	}
}
