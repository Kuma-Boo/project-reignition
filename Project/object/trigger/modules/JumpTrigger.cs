using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Force the player to jump to a point, specified by <see cref="targetNode"/> or <see cref="targetPosition"/>
	/// </summary>
	[Tool]
	public partial class JumpTrigger : StageTriggerModule
	{
		[Export]
		private Node3D targetNode; //Leaving this empty will use targetPosition exclusively.
		[Export]
		private Vector3 targetPosition; //Position to jump to. (Added to targetNode's position)
		[Export]
		public float peakHeight; //How high to jump.

		private Vector3 GetTargetPosition()
		{
			Vector3 returnPosition = targetPosition;
			if (targetNode != null)
				returnPosition += targetNode.GlobalPosition;
			return returnPosition;
		}

		public override void Activate()
		{
			Character.JumpTo(GetTargetPosition(), peakHeight);
			Character.CanJumpDash = false;
		}

		public Objects.LaunchData GetLaunchData() => Objects.LaunchData.Create(GlobalPosition, GetTargetPosition(), peakHeight);
	}
}
