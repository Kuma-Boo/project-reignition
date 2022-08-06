using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	[Tool]
	//When the player enters this trigger, they will skid to a stop before dashing in a 90 degree angle
	public class DriftTrigger : Area
	{
		public const float SPEED_RATIO = 1.5f; //Speed ratio for a successful drift

		[Export]
		public float slideDistance = 10;
		[Export]
		public bool isRightTurn;

		private float entrySpeed;
		private Vector3 driftVelocity;
		public bool cornerCleared;
		private bool bonusObtained;

		public Vector3 TargetPosition => cornerCleared ? EndPosition : MiddlePosition;
		public Vector3 EndPosition => MiddlePosition + this.Right() * (isRightTurn ? 1 : -1) * slideDistance;
		public Vector3 MiddlePosition => GlobalTranslation + this.Back() * slideDistance;
		private const float MINIMUM_ENTRANCE_SPEED_RATIO = .8f;
		private const float DRIFT_SMOOTHING = .25f;

		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area _)
		{
			if (Character.IsUsingBreakSkills) return; //Can't drift during speed/time break :\
			if (!Character.IsOnGround || Character.SpeedRatio < MINIMUM_ENTRANCE_SPEED_RATIO) return;

			cornerCleared = false;
			entrySpeed = Character.MoveSpeed;
			driftVelocity = Character.Velocity;
			Character.StartDrift(this);
		}

		public Vector3 Interpolate(Vector3 playerPosition) => playerPosition.SmoothDamp(TargetPosition, ref driftVelocity, DRIFT_SMOOTHING, entrySpeed);

		public void CompleteDrift(bool wasSuccessful)
		{
			if (!bonusObtained)
			{
				bonusObtained = true;

				if (wasSuccessful)
					GameplayInterface.instance.AddBonus(GameplayInterface.BonusTypes.Drift);
			}

			Character.CancelMovementState(CharacterController.MovementStates.Drift);
		}
	}
}