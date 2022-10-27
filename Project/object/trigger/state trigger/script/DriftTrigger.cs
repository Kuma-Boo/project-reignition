using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Makes the player do a 90 degree turn when entering fast enough.
	/// </summary>
	[Tool]
	public partial class DriftTrigger : Area3D
	{
		[Export]
		public bool isRightTurn; //Which way is the corner?

		//Public for the editor
		public Vector3 EndPosition => MiddlePosition + this.Right() * (isRightTurn ? 1 : -1) * SLIDE_DISTANCE;
		public Vector3 MiddlePosition => GlobalPosition + this.Forward() * SLIDE_DISTANCE;

		private bool isProcessing; //Is this drift trigger currently processing?
		private bool isCornerCleared; //Has the corner been cleared?
		private float entrySpeed; //Entry speed
		private Vector3 driftVelocity; //For smooth damp
		private CharacterController Character => CharacterController.instance;

		private const float SLIDE_DISTANCE = 10; //How far to slide
		private const float DRIFT_SMOOTHING = .25f;
		private const float ENTRANCE_SPEED_RATIO = .8f; //Entrance speed (ratio) required to start a drift
		private const float EXIT_SPEED_RATIO = 1.5f; //Exit speed (ratio) from a successful drift

		public override void _PhysicsProcess(double _)
		{
			if (!isProcessing) return; //Inactive
			ProcessDrift();
		}

		private bool IsDriftValid() //Checks whether the player is in a state where a drift is possible
		{
			if (Character.Skills.IsUsingBreakSkills) return false; //Can't drift during speed/time break :\
			if (!Character.IsOnGround || Character.groundSettings.GetSpeedRatio(Character.MoveSpeed) < ENTRANCE_SPEED_RATIO) return false; //In air/too slow

			return true; //Valid drift
		}

		private void StartDrift() //Initialize drift
		{
			isCornerCleared = false;
			entrySpeed = Character.MoveSpeed;
			driftVelocity = Vector3.Zero;

			Character.StartExternal();
			isProcessing = true;
		}

		private void ProcessDrift()
		{
			Vector3 targetPosition = isCornerCleared ? EndPosition : MiddlePosition;

			//Process drift
			float distance = Character.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());

			if (isCornerCleared)
			{
				Character.MoveSpeed = Character.groundSettings.speed * EXIT_SPEED_RATIO;
				Character.GlobalPosition = Character.GlobalPosition.MoveToward(targetPosition, Character.MoveSpeed * PhysicsManager.physicsDelta);

				if (distance < SLIDE_DISTANCE * .1f) //Drift successful
					CompleteDrift();
			}
			else
			{
				Character.GlobalPosition = Character.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DRIFT_SMOOTHING, entrySpeed);

				if (distance < .5f)
				{
					if (Character.Controller.actionButton.wasPressed)
					{
						ApplyBonus(true);
						isCornerCleared = true;
						Character.GlobalPosition = new Vector3(targetPosition.x, Character.GlobalPosition.y, targetPosition.z); //Snap to target position
					}
					else if (distance < .1f) //Drift failed
					{
						Character.MoveSpeed = 0f; //Reset Movespeed
						ApplyBonus(false);
						CompleteDrift();
					}
				}
			}

			Character.PathFollower.Resync(); //Resync
		}

		private void CompleteDrift()
		{
			isProcessing = false;
			Character.ResetMovementState();
		}

		private bool wasBonusApplied; //Was this corner attempted before?
		private void ApplyBonus(bool isSuccess)
		{
			if (wasBonusApplied) return; //Bonus was already applied

			wasBonusApplied = true;
			/*
			if (isSuccess)
				HeadsUpDisplay.instance.AddBonus(HeadsUpDisplay.BonusTypes.Drift);
			*/
		}

		public void OnEntered(Area3D _)
		{
			if (!IsDriftValid())
			{
				ApplyBonus(false); //Invalid drift, skip bonus
				return;
			}

			StartDrift(); //Drift started successfully
		}
	}
}