using Godot;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	//When the player enters this trigger, they will skid to a stop before dashing in a 90 degree angle
	public class DriftTrigger : Area
	{
		[Export]
		public float slideDistance = 10;
		[Export]
		public bool isRightTurn;

		public float entrySpeed;
		public bool cornerCleared;
		private bool bonusObtained;

		public Vector3 TargetPosition => cornerCleared ? EndPosition : MiddlePosition;
		public Vector3 EndPosition => MiddlePosition + this.Right() * (isRightTurn ? 1 : -1) * slideDistance;
		public Vector3 MiddlePosition => GlobalTransform.origin + this.Back() * slideDistance;
		private const float MINIMUM_ENTRANCE_SPEED_RATIO = .8f;

		private CameraTrigger _cameraTrigger;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			if(GetChildCount() != 0)
				_cameraTrigger = GetChildOrNull<CameraTrigger>(0);
		}

		public void OnEntered(Area _)
		{
			if (Character.IsUsingBreakSkills) return; //Can't drift during speed/time break :\

			if (!Character.IsOnGround || Character.SpeedRatio < MINIMUM_ENTRANCE_SPEED_RATIO) return;

			if (_cameraTrigger != null)
				_cameraTrigger.Activate();

			cornerCleared = false;
			entrySpeed = Character.MoveSpeed;
			Character.StartDrift(this);
		}

		public void CompleteDrift(bool wasSuccessful)
		{
			if (!bonusObtained)
			{
				bonusObtained = true;
				if (wasSuccessful)
					GameplayInterface.instance.AddBonus(GameplayInterface.BonusTypes.Drift);
			}

			if (_cameraTrigger != null)
				_cameraTrigger.Deactivate(wasSuccessful);
		}
	}
}