using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Force the player to jump to the StageTriggerModule's position.
	/// </summary>
	[Tool]
	public partial class JumpTrigger : StageTriggerModule
	{
		[Export]
		public float jumpHeight; //How high to jump.

		public LaunchSettings GetLaunchSettings() => JumpSettings.CreateLaunchSettings(GetParent<Node3D>().GlobalPosition);
		private JumpSettings JumpSettings => new JumpSettings()
		{
			destination = GlobalPosition,
			jumpHeight = jumpHeight,
			isJump = true
		};

		public override void Activate()
		{
			Character.JumpTo(JumpSettings);
			Character.CanJumpDash = false;
		}
	}
}


namespace Project.Gameplay
{
	/// <summary>
	/// Jump settings used in CharacterController.JumpTo()
	/// </summary>
	public struct JumpSettings
	{
		/// <summary> Jump's destination. </summary>
		public Vector3 destination;
		/// <summary> Jump's height. </summary>
		public float jumpHeight;
		/// <summary> Is jump height relative to the starting position or the ending position? </summary>
		public bool relativeToEnd;

		/// <summary> CharacterController will play jump animations/sfx when this is True. </summary>
		public bool isJump;

		public LaunchSettings CreateLaunchSettings(Vector3 startPosition) => LaunchSettings.Create(startPosition, destination, jumpHeight, relativeToEnd);
	}
}