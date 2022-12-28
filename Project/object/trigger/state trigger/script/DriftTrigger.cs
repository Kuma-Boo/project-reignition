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
		private bool isRightTurn; //Which way is the corner?

		//Public for the editor
		public Vector3 EndPosition => MiddlePosition + ExitDirection * slideDistance;
		public Vector3 MiddlePosition => GlobalPosition + this.Back() * slideDistance;
		public Vector3 ExitDirection => this.Right() * (isRightTurn ? 1 : -1);

		private bool isProcessing; //Is this drift trigger currently processing?
		private bool wasDriftAttempted; //Did the player already press the action button and fail?
		private float entrySpeed; //Entry speed
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		/// <summary> For smooth damp </summary>
		private Vector3 driftVelocity;
		/// <summary> Positional smoothing </summary>
		private const float DRIFT_SMOOTHING = .25f;

		/// <summary> How far to slide. </summary>
		[Export(PropertyHint.Range, "1, 10")]
		private int slideDistance = 10;
		/// <summary> Entrance speed (ratio) required to start a drift. </summary>
		private const float ENTRANCE_SPEED_RATIO = .5f;

		[ExportSubgroup("Components")]
		[Export]
		private AudioStreamPlayer sfx;
		[Export]
		private LockoutResource lockout;
		private float startingVolume;
		private bool isFadingSFX;
		private float MIN_STARTING_VOLUME = -6f; //SFX volume when player enters slowly

		public override void _PhysicsProcess(double _)
		{
			if (!isProcessing)
			{
				if (isFadingSFX)
					isFadingSFX = SoundManager.instance.FadeSFX(sfx);

				return; //Inactive
			}
			UpdateDrift();
		}

		private bool IsDriftValid() //Checks whether the player is in a state where a drift is possible
		{
			if (Character.IsMovingBackward) return false; //Can't drift backwards
			if (Character.Skills.IsUsingBreakSkills) return false; //Can't drift during speed/time break :\
			if (!Character.IsOnGround || Character.GroundSettings.GetSpeedRatio(Character.MoveSpeed) < ENTRANCE_SPEED_RATIO) return false; //In air/too slow
			if (Character.MovementState == CharacterController.MovementStates.External) return false; //Player is already busy

			return true; //Valid drift
		}

		private void StartDrift() //Initialize drift
		{
			isProcessing = true;

			entrySpeed = Character.MoveSpeed;
			driftVelocity = Vector3.Zero;

			wasDriftAttempted = false;

			//Reset sfx volume
			float speedRatio = (Character.GroundSettings.GetSpeedRatioClamped(entrySpeed)) - ENTRANCE_SPEED_RATIO / (1 - ENTRANCE_SPEED_RATIO);
			startingVolume = Mathf.Lerp(MIN_STARTING_VOLUME, 0, speedRatio);
			isFadingSFX = false;
			sfx.VolumeDb = startingVolume;
			sfx.Play();

			Character.StartExternal(this);
			Character.Animator.ExternalAngle = Character.MovementAngle;
		}

		private void UpdateDrift()
		{
			Vector3 targetPosition = MiddlePosition;

			//Process drift
			float distance = Character.GlobalPosition.Flatten().DistanceTo(targetPosition.Flatten());
			Character.GlobalPosition = Character.GlobalPosition.SmoothDamp(targetPosition, ref driftVelocity, DRIFT_SMOOTHING, entrySpeed);

			//Fade out sfx based on distance
			float volume = distance / slideDistance;
			sfx.VolumeDb = Mathf.SmoothStep(startingVolume, -80f, volume);

			if (Controller.jumpButton.wasPressed) //Allow character to jump out of drift at any time
			{
				FinishDrift();

				ApplyBonus(false);
				Character.Jump();
				Character.MoveSpeed = driftVelocity.Length(); //Keep speed from drift
			}
			else if (!wasDriftAttempted && Controller.actionButton.wasPressed)
			{
				wasDriftAttempted = true;

				if (distance < 1f)
				{
					ApplyBonus(true);

					//Turn 90 degrees
					Character.MovementAngle = Character.CalculateForwardAngle(ExitDirection);
					Character.Animator.ExternalAngle = Character.MovementAngle;

					//Snap to target position (Except on the Y-axis)
					Character.GlobalPosition = new Vector3(targetPosition.x, Character.GlobalPosition.y, targetPosition.z);
					Character.AddLockoutData(lockout); //Apply lockout

					FinishDrift();
				}
			}
			else if (distance < .1f) //Drift was failed
			{
				Character.MoveSpeed = 0f; //Reset Movespeed
				ApplyBonus(false);
				FinishDrift();
			}

			Character.PathFollower.Resync(); //Resync
		}

		private void FinishDrift()
		{
			isProcessing = false;
			Character.ResetMovementState();
			isFadingSFX = true; //Fade sound effect
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