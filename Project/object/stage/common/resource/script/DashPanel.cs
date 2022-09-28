using Godot;

namespace Project.Gameplay.Objects
{
	public partial class DashPanel : Area3D
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

		public override void _PhysicsProcess(double _)
		{
			if (isQueued && Character.IsOnGround)
				Activate();
		}

		public void OnEntered(Area3D _) => isQueued = true;

		private void Activate()
		{
			_sfxPlayer.Play();
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
			Vector3 forwardDirection = this.Forward();
			float dot = forwardDirection.Dot(Vector3.Up);
			if (Mathf.Abs(dot) > .9f)
				forwardDirection = Mathf.Sign(dot) * this.Up();
			return forwardDirection.Flatten().AngleTo(Vector2.Down);
		}
	}
}
