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
		private AudioStreamPlayer sfxPlayer;
		private CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(double _)
		{
			if (isQueued && Character.IsOnGround)
				Activate();
		}

		public void OnEntered(Area3D _) => isQueued = true;

		private void Activate()
		{
			sfxPlayer.Play();
			isQueued = false;
			//Character.CancelBackflip();

			Character.AddLockoutData(new LockoutResource()
			{
				directionOverrideMode = LockoutResource.DirectionOverrideMode.Replace,
				overrideAngle = GetForwardAngle(),
				speedRatio = speedRatio,
				disableActions = true,
				overrideSpeed = true,
				tractionMultiplier = -1f,
				length = length,
			});
		}

		private float GetForwardAngle()
		{
			GD.Print("DashPanel.cs GetFowardAngle() is untested code. This is a reminder to check for bugs.");
			Vector3 forwardDirection = this.Back();
			float dot = forwardDirection.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > .9f)
				forwardDirection = Mathf.Sign(dot) * this.Up();
			return forwardDirection.Flatten().AngleTo(Vector2.Down);
		}
	}
}
