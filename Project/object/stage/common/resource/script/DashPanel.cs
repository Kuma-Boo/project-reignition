using Godot;

namespace Project.Gameplay.Objects
{
	public partial class DashPanel : Area3D
	{
		[Export(PropertyHint.Range, "0, 2")]
		private float speedRatio;
		[Export]
		private float length; //How long for the boost pad to last
		private bool isQueued; //For when the player collides with the dash panel from the air
		[Export]
		private bool alignToPath; //Forces the player to stay aligned to the path. Useful when a dash panel is right before a corner.

		[Export]
		private AudioStreamPlayer3D sfxPlayer;
		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (!isQueued) return;

			Activate();
		}

		public void OnEntered(Area3D _) => isQueued = true;

		private void Activate()
		{
			if (!Character.IsOnGround) return; //Can't activate when player is in the air

			sfxPlayer.Play();
			isQueued = false;

			//Only apply speed boost when player is moving slower. Don't slow them down!
			bool applyBoost = Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < speedRatio;

			//Apply boost during automation
			if (Character.MovementState == CharacterController.MovementStates.External &&
			 Character.ExternalController != null && Character.ExternalController is Triggers.AutomationTrigger)
			{
				if (applyBoost)
					Character.MoveSpeed = Character.GroundSettings.speed * speedRatio;

				return;
			}

			LockoutResource lockout = new LockoutResource()
			{
				overrideMode = LockoutResource.OverrideMode.Replace,
				spaceMode = LockoutResource.SpaceMode.Camera,
				overrideAngle = CharacterController.CalculateForwardAngle(this.Back()),
				speedRatio = speedRatio,
				disableActions = true,
				overrideSpeed = applyBoost,
				tractionMultiplier = 0f,
				length = length,
				priority = -1, //Not using priority
			};

			if (alignToPath)
			{
				lockout.overrideAngle = 0f;
				lockout.spaceMode = LockoutResource.SpaceMode.PathFollower;
				Character.MovementAngle = Character.PathFollower.ForwardAngle;
			}
			else
				Character.MovementAngle = lockout.overrideAngle;

			Character.AddLockoutData(lockout);
		}
	}
}
