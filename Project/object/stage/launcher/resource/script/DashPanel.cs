using Godot;

namespace Project.Gameplay.Objects
{
	public class DashPanel : Area
	{
		[Export]
		public NodePath sfxPlayer;
		private AudioStreamPlayer _sfxPlayer;
		[Export(PropertyHint.Range, "0, 2")]
		public float speedRatio;
		[Export]
		public float length; //How long for the boost pad to last

		private bool isQueued;

		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			_sfxPlayer = GetNode<AudioStreamPlayer>(sfxPlayer);
		}

		public override void _PhysicsProcess(float _)
		{
			if (isQueued && Character.IsOnGround)
				Activate();
		}

		public void OnEntered(Area _) => isQueued = true;

		private void Activate()
		{
			_sfxPlayer.Play();
			isQueued = false;
			Character.CancelBackflip();
			Character.StartControlLockout(new ControlLockoutResource()
			{
				strafeSettings = ControlLockoutResource.StrafeSettings.KeepPosition,
				speedRatio = speedRatio,
				disableJumping = true,
				overrideSpeed = true,
				tractionRatio = 8f,
				length = length,
			});
		}
	}
}
