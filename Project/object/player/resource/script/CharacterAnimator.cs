using Godot;

//TODO Rewrite this entire script
namespace Project.Gameplay
{
	/// <summary>
	/// Responsible for playing the player's animations and visual effects.
	/// </summary>
	public partial class CharacterAnimator : Node3D
	{
		[Export]
		private AnimationTree animator;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			animator.Active = true;
		}

		private const string COUNTDOWN_PARAMETER = "parameters/countdown/active";
		public void Countdown()
		{
			animator.Set(COUNTDOWN_PARAMETER, true);
		}

		public void PlayAnimation(string anim) //Play a specific animation
		{

		}

		#region Grinding Animation
		private const string IS_BALANCING_STATE = "parameters/balancing/current";
		private const string BALANCE_STATE_PARAMETER = "parameters/balance_state/active";
		private const string BALANCE_LEFT_STATE_PARAMETER = "parameters/balance_state/balance_left/blend_position";
		private const string BALANCE_RIGHT_STATE_PARAMETER = "parameters/balance_state/balance_right/blend_position";
		public void StartGrinding()
		{
			animator.Set(IS_BALANCING_STATE, 1); //Turn on grinding animations
			animator.Set(BALANCE_STATE_PARAMETER, true);
			animator.Set(BALANCE_LEFT_STATE_PARAMETER, 0);
			animator.Set(BALANCE_RIGHT_STATE_PARAMETER, 0);
		}

		//public AnimationNodeStateMachinePlayback GrindingState => _animator.Get("parameters/balance_state/playback") as AnimationNodeStateMachinePlayback;

		public void StopGrinding()
		{
			animator.Set("parameters/balancing/current", 0); //Turn off grinding animations
		}
		#endregion

		#region Normal Animation
		private float strafeTilt;
		private const string CROUCH_PARAMETER = "parameters/ground_state/IsCrouching/current";

		private const string JUMP_TRIGGER_PARAMETER = "parameters/air_state/jump/active";
		private const string JUMPING_PARAMETER = "parameters/air_state/IsJumping/current";
		private const string JUMPDASH_PARAMETER = "parameters/air_state/IsJumpDashing/current";
		private const string FALL_TRIGGER_PARAMETER = "parameters/air_state/fall/active";
		private const string FALL_RESET_PARAMETER = "parameters/air_state/fall_reset/seek_position";
		private const string JUMP_CANCEL_TIME_PARAMETER = "parameters/air_state/jump_cancel_time/scale";

		public void Jump()
		{
			animator.Set(JUMPING_PARAMETER, 1);
			animator.Set(JUMP_TRIGGER_PARAMETER, true);
		}

		private const string BACKFLIP_TRIGGER_PARAMETER = "parameters/air_state/backflip/active";
		public void Backflip()
		{
			animator.Set(BACKFLIP_TRIGGER_PARAMETER, true);
			animator.Set(FALL_TRIGGER_PARAMETER, false);
			animator.Set(FALL_RESET_PARAMETER, 0);
			//ResetLocalRotation();
		}

		public void JumpAccel()
		{

		}

		public void Stomp()
		{
			FallAnimation();
			animator.Set(JUMP_CANCEL_TIME_PARAMETER, 1.5f);
		}

		public void FallAnimation()
		{
			animator.Set(JUMPING_PARAMETER, 0);
			animator.Set(JUMP_CANCEL_TIME_PARAMETER, 1f);
			animator.Set(FALL_TRIGGER_PARAMETER, true);
			animator.Set(FALL_RESET_PARAMETER, 0);
		}

		private const string GROUND_PARAMETER = "parameters/IsGrounded/current";
		private const float MOVEMENT_DEADZONE = .2f;
		public void UpdateAnimation()
		{
			animator.Set(GROUND_PARAMETER, Character.IsOnGround ? 0 : 1);
			if (Character.IsOnGround)
				GroundAnimations();
			else
				AirAnimations();

			UpdateRotation();
		}

		#region Visual Rotation
		/// <summary> Angle to use when character's MovementState is CharacterController.MovementStates.External. </summary>
		public float ExternalAngle { get; set; }
		/// <summary> Rotation (in radians) currently applied to Transform. </summary>
		private float VisualAngle { get; set; }
		private float rotationVelocity;

		/// <summary> Rotation smoothing amount for movement. </summary>
		private readonly float MOVEMENT_ROTATION_SMOOTHING = .1f;

		/// <summary>
		/// Snaps visual rotation, without any smoothing applied.
		/// </summary>
		public void SnapRotation(float angle)
		{
			VisualAngle = angle;
			rotationVelocity = 0;
			ApplyRotation();
		}

		/// <summary>
		/// Calculates the target visual rotation and applies it.
		/// </summary>
		private void UpdateRotation()
		{
			//Don't update directions when externally controlled or on launchers
			float targetRotation = Character.MovementAngle;
			float smoothing = MOVEMENT_ROTATION_SMOOTHING;

			if (Character.Lockon.IsHomingAttacking) //Face target
				targetRotation = Character.CalculateForwardAngle(Character.Lockon.HomingAttackDirection);
			else if (Character.Skills.IsSpeedBreakActive) //Speed break, use path's forward direction
				targetRotation = Character.PathFollower.ForwardAngle;
			else if (Character.MovementState == CharacterController.MovementStates.External)
				targetRotation = ExternalAngle;

			VisualAngle = ExtensionMethods.SmoothDampAngle(VisualAngle, targetRotation, ref rotationVelocity, MOVEMENT_ROTATION_SMOOTHING);
			ApplyRotation();
		}

		/// <summary>
		/// Apply currentRotation on Transform.
		/// </summary>
		private void ApplyRotation() => Rotation = Vector3.Up * VisualAngle;
		#endregion

		private float strafeVelocity;
		private const string MOVEMENT_STATE_PARAMETER = "parameters/ground_state/MoveState/current";
		private void GroundAnimations()
		{
			/*
			int transition = 1; //Idle
			if (Character.MoveSpeed < 0) //Backstep
				transition = 0;
			else if (Character.SpeedRatio >= 1) //Running
				transition = 3;
			else if (!IsIdling) //Walk -> Jog
				transition = 2;

			_animator.Set(MOVEMENT_STATE_PARAMETER, transition);
			_animator.Set(CROUCH_PARAMETER, Character.IsCrouching ? 1 : 0);

			float targetStrafeTilt = 0;

			if (Character.ControlLockoutData == null && Character.Controller.MovementAxis != Vector2.Zero)
			{
				float targetDirection = new Vector2(Character.GetStrafeInputValue(), -Mathf.Abs(Character.GetMovementInputValue())).AngleTo(Vector2.Up);
				targetStrafeTilt = -Mathf.Clamp((targetDirection - Rotation.y) / Mathf.Pi * .5f, -1, 1);
			}
			strafeTilt = ExtensionMethods.SmoothDamp(strafeTilt, targetStrafeTilt, ref strafeVelocity, .2f);

			float moveAnimationSpeed = 1f;

			if (Character.IsFreeMovementActive)
				moveAnimationSpeed = Character.SpeedRatio;
			else if (Character.SpeedRatio < 0)
				moveAnimationSpeed = .8f * Mathf.Abs(Character.backstepSettings.GetSpeedRatio(Character.MoveSpeed));
			else if (!IsIdling)
				moveAnimationSpeed = Mathf.Max(Character.SpeedRatio, Mathf.Abs(Character.runningStrafeSettings.GetSpeedRatioClamped(Character.StrafeSpeed)));

			_animator.Set("parameters/ground_state/Jog/blend_position", Character.SpeedRatio);//new Vector2(strafeTilt, moveAnimationSpeed));
			_animator.Set("parameters/ground_state/Run/blend_position", strafeTilt);


			moveAnimationSpeed = Mathf.Clamp((moveAnimationSpeed - .5f) / .5f, 0f, 1f);
			_animator.Set("parameters/ground_state/MoveSpeed/scale", Mathf.Lerp(1.2f, 2f, moveAnimationSpeed));
			*/
		}

		private void AirAnimations()
		{
			bool isJumpDashing = Character.ActionState == CharacterController.ActionStates.JumpDash || Character.ActionState == CharacterController.ActionStates.AccelJump;
			animator.Set(JUMPDASH_PARAMETER, isJumpDashing ? 1 : 0);

			if ((int)animator.Get(JUMPING_PARAMETER) == 1)
			{
				if (Character.ActionState != CharacterController.ActionStates.Jumping || Character.VerticalSpd <= 5f)
					FallAnimation();
			}
		}
		#endregion
	}
}